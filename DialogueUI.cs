using UnityEngine;
using TMPro;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text messageText;

    public bool IsOpen => dialoguePanel != null && dialoguePanel.activeSelf;

    private void Awake()
    {
        if (dialoguePanel == null || nameText == null || messageText == null)
        {
            Debug.LogError("DialogueUI references are not assigned in the Inspector.", this);
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one DialogueUI exists in the scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        HideDialogue();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowDialogue(string speakerName, string message)
    {
        if (dialoguePanel == null || nameText == null || messageText == null)
        {
            Debug.LogError("DialogueUI references are not assigned in the Inspector.");
            return;
        }

        nameText.text = speakerName;
        messageText.text = message;
        dialoguePanel.SetActive(true);
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }
}
