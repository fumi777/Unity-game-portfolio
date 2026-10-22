using System.Collections.Generic;
using UnityEngine;

public class friendDialogue : MonoBehaviour
{
    [SerializeField] private FriendEscape friendEscape;
    [SerializeField, InspectorName("Speaker Name")] protected string friendName = "Friend";
    // Keep these field names so existing Inspector dialogue is preserved.
    [SerializeField, TextArea(2, 5)] private string[] messages = { "Hello!" };
    [SerializeField, TextArea(2, 5)] private string[] allItemsCollectedMessages = { "箱を集めてくれてありがとう！" };
    [SerializeField, TextArea(2, 5)] private string[] allItem1And2CollectedMessages = { "赤いボタンを見つけたんだね！" };
    [SerializeField, TextArea(2, 5)] private string[] afterHidemanMessages = { "hidemanに会えたんだね！" };
    [SerializeField, TextArea(2, 5)] private string[] afterHidemanAndItem3Messages = { "青い薬を持ってきてくれたんだね！" };
    private GameProgressManager progress;
    private int conversation;
    private string[] displayedMessages;
    private int currentPage;
    private GameProgressManager.Stage openedStage;
    private static friendDialogue activeSpeaker;
    private static int lastInputFrame = -1;
    private readonly HashSet<Collider> nearbyPlayers = new HashSet<Collider>();
    public FriendEscape EscapeController => friendEscape;
    public void BindProgress(GameProgressManager manager) { progress = manager; }
    public void SelectConversation(int index) { conversation = index; }
    private string[] CurrentMessages
    {
        get
        {
            switch (conversation)
            {
                case 1: return allItemsCollectedMessages;
                case 2: return allItem1And2CollectedMessages;
                case 3: return afterHidemanMessages;
                case 4: return afterHidemanAndItem3Messages;
                default: return messages;
            }
        }
    }
    protected virtual void Update()
    {
        if (DialogueUI.Instance == null || lastInputFrame == Time.frameCount) return;
        bool leftClick = Input.GetMouseButtonDown(0);
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (activeSpeaker == this) { lastInputFrame = Time.frameCount; CloseDialogue(true); }
            else if (activeSpeaker == null && IsClosest())
            {
                lastInputFrame = Time.frameCount;
                OpenDialogue();
            }
            return;
        }
        // 会話を開いたクリックで、同時にページが進まないようにする。
        if (leftClick && activeSpeaker == null && IsClosest())
        {
            lastInputFrame = Time.frameCount;
            OpenDialogue();
            return;
        }
        if (activeSpeaker != this || displayedMessages == null) return;
        if (Input.GetKeyDown(KeyCode.G) || leftClick)
        {
            lastInputFrame = Time.frameCount;
            if (currentPage == displayedMessages.Length - 1) CloseDialogue(true);
            else { currentPage++; ShowPage(); }
        }
        else if (Input.GetKeyDown(KeyCode.V) && currentPage > 0)
        {
            lastInputFrame = Time.frameCount;
            currentPage--;
            ShowPage();
        }
    }
    private bool IsClosest()
    {
        float distance = DistanceToPlayer();
        if (float.IsPositiveInfinity(distance)) return false;
        foreach (friendDialogue other in FindObjectsByType<friendDialogue>(FindObjectsSortMode.None))
        {
            if (other == this || !other.isActiveAndEnabled) continue;
            float otherDistance = other.DistanceToPlayer();
            if (otherDistance < distance || (otherDistance == distance && other.GetInstanceID() < GetInstanceID()))
                return false;
        }
        return true;
    }
    private float DistanceToPlayer()
    {
        float distance = float.PositiveInfinity;
        foreach (Collider player in nearbyPlayers)
            if (player != null && player.enabled && player.gameObject.activeInHierarchy)
                distance = Mathf.Min(distance, (player.transform.position - transform.position).sqrMagnitude);
        return distance;
    }
    private void OpenDialogue()
    {
        if (progress == null || !progress.isActiveAndEnabled)
        {
            Debug.LogWarning("Assign this NPC to GameProgressManager.", this);
            return;
        }
        displayedMessages = CurrentMessages;
        if (displayedMessages == null || displayedMessages.Length == 0)
        {
            Debug.LogWarning("Configure the messages for this conversation stage.", this);
            return;
        }
        openedStage = progress.CurrentStage;
        activeSpeaker = this;
        currentPage = 0;
        ShowPage();
    }
    private void ShowPage() { DialogueUI.Instance.ShowDialogue(friendName, displayedMessages[currentPage]); }
    private void CloseDialogue(bool allowCompletion)
    {
        bool complete = allowCompletion && displayedMessages != null
            && currentPage == displayedMessages.Length - 1;
        if (complete && progress != null && !progress.CompleteConversation(this, openedStage)) return;
        if (DialogueUI.Instance != null) DialogueUI.Instance.HideDialogue();
        if (activeSpeaker == this) activeSpeaker = null;
    }
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) nearbyPlayers.Add(other);
    }
    protected virtual void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player")) nearbyPlayers.Add(other);
    }
    protected virtual void OnTriggerExit(Collider other)
    {
        nearbyPlayers.Remove(other);
        if (activeSpeaker == this && float.IsPositiveInfinity(DistanceToPlayer())) CloseDialogue(false);
    }
    protected virtual void OnDisable()
    {
        if (activeSpeaker == this)
        {
            if (DialogueUI.Instance != null) DialogueUI.Instance.HideDialogue();
            activeSpeaker = null;
        }
        nearbyPlayers.Clear();
    }
}
