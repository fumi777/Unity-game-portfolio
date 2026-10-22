using UnityEngine;

public class ControlsUI : MonoBehaviour
{
    [SerializeField] private GameObject controlRulePanel;
    [SerializeField] private GameObject controlsText;
    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private KeyCode toggleKey = KeyCode.Q;

    private bool isOpen;
    private bool isTitleScene;

    private void Awake()
    {
        // Prefab内の子を取得する。Inspectorで指定されていればそちらを使う。
        if (controlRulePanel == null)
        {
            Transform panel = transform.Find("ControlRulePanal");
            if (panel == null) panel = transform.Find("ControlRulePanel");
            if (panel != null) controlRulePanel = panel.gameObject;
        }
        if (controlsText == null)
        {
            Transform text = transform.Find("ControlsText");
            if (text != null) controlsText = text.gameObject;
        }

        isTitleScene = gameObject.scene.name == titleSceneName;
        isOpen = false;
        RefreshDisplay();

        if (controlRulePanel == null || controlsText == null)
            Debug.LogWarning("ControlsUI: assign Control Rule Panel and Controls Text.", this);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isOpen = !isOpen;
            RefreshDisplay();
        }
    }

    private void RefreshDisplay()
    {
        if (controlRulePanel != null) controlRulePanel.SetActive(isOpen);
        if (controlsText != null) controlsText.SetActive(isTitleScene || isOpen);
    }
}
