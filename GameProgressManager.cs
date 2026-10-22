using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameProgressManager : MonoBehaviour
{
    public enum Stage
    {
        FirstFriend, CollectBoxes, FriendAfterBoxes, RobotAfterBoxes,
        CollectRedButton, RobotAfterButton, FriendAfterButton, FindHideman,
        FriendAfterHideman, CollectMedicine, FinalFriend, Escape
    }
    [SerializeField] private friendDialogue friend;
    [SerializeField] private robotDialogue robot;
    [SerializeField] private hidemanDialogue hideman;
    [SerializeField] private GameObject friendTalkMarker;
    [SerializeField] private GameObject robotTalkMarker;
    [SerializeField] private GameObject hidemanTalkMarker;
    [SerializeField] private FriendEscape friendEscape;
    [SerializeField] private playercontroller player;
    [Header("Mission UI")]
    [SerializeField] private GameObject missionPanel;
    [SerializeField] private TMP_Text missionText2;
    [Serializable]
    private class MissionDescription
    {
        public Stage stage;
        [TextArea(2, 4)] public string message;
        public MissionDescription(Stage stage, string message)
        {
            this.stage = stage;
            this.message = message;
        }
    }
    [SerializeField] private MissionDescription[] missionDescriptions =
    {
        new MissionDescription(Stage.FirstFriend, "フレンドに話しかけよう"),
        new MissionDescription(Stage.CollectBoxes, "黄色い箱を集めよう（残り{boxes}個）"),
        new MissionDescription(Stage.FriendAfterBoxes, "箱を集めたことをフレンドに報告しよう"),
        new MissionDescription(Stage.RobotAfterBoxes, "ロボットに話しかけよう"),
        new MissionDescription(Stage.CollectRedButton, "赤いボタンを探して押そう"),
        new MissionDescription(Stage.RobotAfterButton, "ロボットに報告しよう"),
        new MissionDescription(Stage.FriendAfterButton, "フレンドに話しかけよう"),
        new MissionDescription(Stage.FindHideman, "隠れている男を探して話しかけよう"),
        new MissionDescription(Stage.FriendAfterHideman, "フレンドに報告しよう"),
        new MissionDescription(Stage.CollectMedicine, "青い薬を探して手に入れよう"),
        new MissionDescription(Stage.FinalFriend, "青い薬を持ってフレンドに話しかけよう")
    };
    private const string SaveKey = "GameProgress.AutoSave.v1";
    [Serializable]
    private class SaveData
    {
        public int version = 1;
        public string scene;
        public Stage stage;
        public Vector3 position;
        public Quaternion rotation;
        public string[] collected;
    }
    private readonly HashSet<string> collectedIds = new HashSet<string>();
    private readonly Dictionary<Component, string> itemIds = new Dictionary<Component, string>();
    private readonly List<Action> unsubscribe = new List<Action>();
    private SaveData loadedSave;
    private string lastSavedJson;
    private bool restoring;
    private bool reloading;
    private bool autoSaveReady;
    private Stage lastRefreshedStage;
    private static string pendingRestore;
    [SerializeField, Min(1)] private int requiredBoxes = 5;
    [SerializeField] private Stage currentStage = Stage.FirstFriend;
    public Stage CurrentStage => currentStage;
    public int BoxesCollected { get; private set; }
    public int BoxesRemaining => Mathf.Max(0, requiredBoxes - BoxesCollected);
    public int RedButtonsRemaining { get; private set; }
    public int MedicineRemaining { get; private set; }
    private int totalButtons, totalMedicine;
    private readonly List<PickUpItem1> boxes = new List<PickUpItem1>();
    private readonly List<PickUpItem2> buttons = new List<PickUpItem2>();
    private readonly List<PickUpItem3> medicines = new List<PickUpItem3>();

    private void Awake()
    {
        currentStage = Stage.FirstFriend;
        SetMarkers(null);
        if (friend == null || robot == null || hideman == null
            || friend == robot || friend == hideman)
        {
            Debug.LogError("GameProgressManager: assign the three NPC dialogue components.", this);
            enabled = false;
            return;
        }
        friend.BindProgress(this);
        robot.BindProgress(this);
        hideman.BindProgress(this);
        if (friendEscape == null) friendEscape = friend.GetComponentInParent<FriendEscape>();
        if (player == null)
            foreach (playercontroller candidate in FindObjectsByType<playercontroller>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene) { player = candidate; break; }
        if (player == null)
            Debug.LogError("GameProgressManager: assign Player to enable autosave and checkpoint placement.", this);
        foreach (PickUpItem1 item in FindObjectsByType<PickUpItem1>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item.gameObject.scene == gameObject.scene)
            {
                boxes.Add(item);
                string id = RegisterItem(item);
                Action handler = () => { if (collectedIds.Add(id)) OnBox(); };
                item.Collected += handler;
                unsubscribe.Add(() => { if (item != null) item.Collected -= handler; });
            }
        foreach (PickUpItem2 item in FindObjectsByType<PickUpItem2>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item.gameObject.scene == gameObject.scene)
            {
                buttons.Add(item);
                string id = RegisterItem(item);
                Action handler = () => { if (collectedIds.Add(id)) OnButton(); };
                item.Collected += handler;
                unsubscribe.Add(() => { if (item != null) item.Collected -= handler; });
            }
        foreach (PickUpItem3 item in FindObjectsByType<PickUpItem3>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item.gameObject.scene == gameObject.scene)
            {
                medicines.Add(item);
                string id = RegisterItem(item);
                Action handler = () => { if (collectedIds.Add(id)) OnMedicine(); };
                item.Collected += handler;
                unsubscribe.Add(() => { if (item != null) item.Collected -= handler; });
            }
        totalButtons = RedButtonsRemaining = buttons.Count;
        totalMedicine = MedicineRemaining = medicines.Count;
        if (boxes.Count < requiredBoxes || totalButtons == 0 || totalMedicine == 0)
            Debug.LogWarning("Not enough quest items in this scene. Check Required Boxes and Item2/Item3 placement.", this);
        LoadCheckpoint();
        Refresh();
    }
    private IEnumerator Start()
    {
        if (loadedSave != null && player != null)
            player.RestoreCheckpoint(loadedSave.position, loadedSave.rotation);
        autoSaveReady = true;
        if (loadedSave == null) AutoSave();

        if (currentStage == Stage.Escape)
        {
            // Wait for camera/BGM initialization before replaying the escape sequence.
            yield return null;
            if (friendEscape == null) friendEscape = friend.EscapeController;
            if (friendEscape == null || !friendEscape.BeginEscape())
            {
                Debug.LogError("Could not resume escape. Returning to the final conversation for retry.", this);
                currentStage = Stage.FinalFriend;
                Refresh();
            }
        }
    }
    private void OnBox() { BoxesCollected++; Refresh(true); }
    private void OnButton() { RedButtonsRemaining--; Refresh(); }
    private void OnMedicine() { MedicineRemaining--; Refresh(); }

    public friendDialogue ExpectedSpeaker
    {
        get
        {
            switch (currentStage)
            {
                case Stage.FirstFriend:
                case Stage.FriendAfterBoxes:
                case Stage.FriendAfterButton:
                case Stage.FriendAfterHideman:
                case Stage.FinalFriend: return friend;
                case Stage.RobotAfterBoxes:
                case Stage.RobotAfterButton: return robot;
                case Stage.FindHideman: return hideman;
                default: return null;
            }
        }
    }
    // The token prevents an old/open conversation from completing a newer stage.
    public bool CompleteConversation(friendDialogue speaker, Stage openedStage)
    {
        if (!isActiveAndEnabled || openedStage != currentStage || speaker != ExpectedSpeaker) return true;
        switch (currentStage)
        {
            case Stage.FirstFriend: currentStage = Stage.CollectBoxes; break;
            case Stage.FriendAfterBoxes: currentStage = Stage.RobotAfterBoxes; break;
            case Stage.RobotAfterBoxes: currentStage = Stage.CollectRedButton; break;
            case Stage.RobotAfterButton: currentStage = Stage.FriendAfterButton; break;
            case Stage.FriendAfterButton: currentStage = Stage.FindHideman; break;
            case Stage.FindHideman: currentStage = Stage.FriendAfterHideman; break;
            case Stage.FriendAfterHideman: currentStage = Stage.CollectMedicine; break;
            case Stage.FinalFriend:
                if (friendEscape == null) friendEscape = friend.EscapeController;
                if (friendEscape == null || !friendEscape.BeginEscape())
                {
                    Debug.LogError("Assign and configure FriendEscape before finishing the final conversation.", this);
                    return false;
                }
                currentStage = Stage.Escape;
                break;
        }
        Refresh();
        return true;
    }
    private void Refresh(bool savePickup = false)
    {
        // Only collection stages advance automatically. Conversations are never skipped.
        if (currentStage == Stage.CollectBoxes && BoxesRemaining == 0)
            currentStage = Stage.FriendAfterBoxes;
        if (currentStage == Stage.CollectRedButton && totalButtons > 0 && RedButtonsRemaining == 0)
            currentStage = Stage.RobotAfterButton;
        if (currentStage == Stage.CollectMedicine && totalMedicine > 0 && MedicineRemaining == 0)
            currentStage = Stage.FinalFriend;

        int friendPage = 0;
        if (currentStage >= Stage.FinalFriend) friendPage = 4;
        else if (currentStage >= Stage.FriendAfterHideman) friendPage = 3;
        else if (currentStage >= Stage.FriendAfterButton) friendPage = 2;
        else if (currentStage >= Stage.FriendAfterBoxes) friendPage = 1;
        friend.SelectConversation(friendPage);
        robot.SelectConversation(currentStage >= Stage.RobotAfterButton ? 2 :
            currentStage >= Stage.RobotAfterBoxes ? 1 : 0);
        hideman.SelectConversation(currentStage >= Stage.FindHideman ? 1 : 0);
        SetMarkers(ExpectedSpeaker);
        RefreshMission();
        RefreshItemVisibility(boxes, currentStage >= Stage.CollectBoxes);
        RefreshItemVisibility(buttons, currentStage >= Stage.CollectRedButton);
        RefreshItemVisibility(medicines, currentStage >= Stage.CollectMedicine);
        bool stageChanged = currentStage != lastRefreshedStage;
        lastRefreshedStage = currentStage;
        if (autoSaveReady && (stageChanged || savePickup)) AutoSave();
    }
    private void RefreshMission()
    {
        bool visible = currentStage != Stage.Escape;
        if (missionPanel != null) missionPanel.SetActive(visible);
        if (missionText2 == null) return;
        missionText2.gameObject.SetActive(visible);
        if (!visible) return;

        string message = currentStage.ToString();
        if (missionDescriptions != null)
            foreach (MissionDescription description in missionDescriptions)
                if (description != null && description.stage == currentStage)
                {
                    message = description.message ?? "";
                    break;
                }
        missionText2.text = message.Replace("{boxes}", BoxesRemaining.ToString())
            .Replace("{buttons}", RedButtonsRemaining.ToString())
            .Replace("{medicine}", MedicineRemaining.ToString());
    }

    private void RefreshItemVisibility<T>(List<T> items, bool unlocked) where T : Component
    {
        foreach (T item in items)
        {
            if (item == null) continue;
            bool visible = unlocked && !collectedIds.Contains(itemIds[item]);
            if (item.gameObject.activeSelf != visible) item.gameObject.SetActive(visible);
        }
    }
    private void SetMarkers(friendDialogue speaker)
    {
        if (friendTalkMarker != null) friendTalkMarker.SetActive(speaker != null && speaker == friend);
        if (robotTalkMarker != null) robotTalkMarker.SetActive(speaker != null && speaker == robot);
        if (hidemanTalkMarker != null) hidemanTalkMarker.SetActive(false);
    }
    private void OnDestroy()
    {
        foreach (Action removeHandler in unsubscribe) removeHandler();
    }

    private string RegisterItem(Component item)
    {
        // Capture before any pickups are destroyed, since sibling indexes can change at runtime.
        string path = "";
        for (Transform node = item.transform; node != null; node = node.parent)
            path = "/" + node.name + "[" + node.GetSiblingIndex() + "]" + path;
        string id = item.GetType().Name + path;
        itemIds.Add(item, id);
        return id;
    }

    private void AutoSave()
    {
        if (restoring || reloading || player == null) return;
        SaveData data = new SaveData
        {
            scene = gameObject.scene.path,
            stage = currentStage,
            position = player.CheckpointPosition,
            rotation = player.CheckpointRotation,
            collected = new List<string>(collectedIds).ToArray()
        };
        lastSavedJson = JsonUtility.ToJson(data);
        try
        {
            PlayerPrefs.SetString(SaveKey, lastSavedJson);
            PlayerPrefs.Save();
        }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void LoadCheckpoint()
    {
        string json = pendingRestore ?? PlayerPrefs.GetString(SaveKey, "");
        pendingRestore = null;
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version != 1 || data.scene != gameObject.scene.path
                || data.stage < Stage.FirstFriend || data.stage > Stage.Escape || data.collected == null
                || !Finite(data.position.x) || !Finite(data.position.y) || !Finite(data.position.z)
                || !Finite(data.rotation.x) || !Finite(data.rotation.y) || !Finite(data.rotation.z)
                || !Finite(data.rotation.w))
                throw new InvalidOperationException("Invalid or incompatible autosave.");
            HashSet<string> known = new HashSet<string>(itemIds.Values);
            foreach (string id in data.collected)
                if (!known.Contains(id)) throw new InvalidOperationException("Saved item no longer exists in the scene.");

            restoring = true;
            collectedIds.UnionWith(data.collected);
            foreach (PickUpItem1 item in boxes)
                if (collectedIds.Contains(itemIds[item])) { BoxesCollected++; item.gameObject.SetActive(false); }
            foreach (PickUpItem2 item in buttons)
                if (collectedIds.Contains(itemIds[item])) { RedButtonsRemaining--; item.gameObject.SetActive(false); }
            foreach (PickUpItem3 item in medicines)
                if (collectedIds.Contains(itemIds[item])) { MedicineRemaining--; item.gameObject.SetActive(false); }
            currentStage = data.stage;
            loadedSave = data;
            lastSavedJson = json;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Autosave could not be restored; starting from the beginning. " + exception.Message, this);
        }
        finally { restoring = false; }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public bool RespawnFromCheckpoint()
    {
        if (reloading) return true;
        if (!Application.CanStreamedLevelBeLoaded(gameObject.scene.path)) return false;
        reloading = true;
        // Empty means no checkpoint yet: restart at the scene's original spawn.
        pendingRestore = lastSavedJson ?? "";
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
        return true;
    }

    [ContextMenu("Clear Auto Save")]
    private void ClearAutoSaveInEditor() { ClearAutoSave(); }

    public static void ClearAutoSave()
    {
        pendingRestore = null;
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }
}
