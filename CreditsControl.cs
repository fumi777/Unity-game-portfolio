using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreditsControl : MonoBehaviour
{
    [SerializeField] private ScreenFade screenFade;
    [SerializeField] private BGMManager bgmManager;
    [SerializeField] private RectTransform creditsViewport;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private Image slideshowImage;
    [SerializeField] private Sprite[] slides;
    [SerializeField] private TMP_Text thankYouText;
    [SerializeField] private GameObject restartButton;
    [SerializeField] private string thankYouMessage = "Thank you for playing!";
    [SerializeField, Min(1f)] private float scrollSpeed = 40f;
    [SerializeField, Min(0.1f)] private float slideDuration = 4f;
    [SerializeField, Min(0f)] private float fadeDuration = 1f;
    [SerializeField, Min(0f)] private float padding = 20f;
    private bool isRestarting;

    private void Awake()
    {
        // クリア後はエンドロール途中で終了しても、次回は最初から開始する。
        GameProgressManager.ClearAutoSave();
        if (restartButton != null) restartButton.SetActive(false);
    }

    // RestartButtonのOn Click()に登録する。
    public void RestartGame()
    {
        if (isRestarting) return;

        if (!Application.CanStreamedLevelBeLoaded("MainScene"))
        {
            Debug.LogError("MainScene is not registered in the build scene list.", this);
            return;
        }

        isRestarting = true;
        GameProgressManager.ClearAutoSave();
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Single);
    }

    private IEnumerator Start()
    {
        if (screenFade == null || !screenFade.IsReady || creditsViewport == null
            || creditsText == null || creditsText.transform.parent != creditsViewport)
        {
            Debug.LogError("CreditsControl: assign ScreenFade, CreditsViewport and its direct child CreditsText.", this);
            yield break;
        }
        // Start runs after all Awake calls, before the first rendered frame.
        if (bgmManager == null)
            foreach (BGMManager candidate in FindObjectsByType<BGMManager>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene) { bgmManager = candidate; break; }
        if (bgmManager != null) bgmManager.PlayCreditsBGM();
        screenFade.SetAlpha(1f);
        if (thankYouText != null) thankYouText.gameObject.SetActive(false);
        creditsText.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        RectTransform textRect = creditsText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0f);
        textRect.anchorMax = new Vector2(0.5f, 0f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;
        float width = Mathf.Max(1f, creditsViewport.rect.width - padding * 2f);
        creditsText.enableAutoSizing = false;
        creditsText.overflowMode = TextOverflowModes.Overflow;
        float height = Mathf.Max(1f, creditsText.GetPreferredValues(creditsText.text, width, Mathf.Infinity).y);
        textRect.sizeDelta = new Vector2(width, height);
        textRect.anchoredPosition = new Vector2(0f, -padding);
        if (slideshowImage != null)
        {
            slideshowImage.preserveAspect = true;
            ShowSlide(0);
        }
        yield return screenFade.FadeTo(0f, fadeDuration);
        float elapsed = 0f;
        float finishY = creditsViewport.rect.height + height + padding;
        while (textRect.anchoredPosition.y < finishY)
        {
            float delta = Time.unscaledDeltaTime;
            textRect.anchoredPosition += Vector2.up * Mathf.Max(1f, scrollSpeed) * delta;
            elapsed += delta;
            if (slides != null && slides.Length > 0)
                ShowSlide(Mathf.FloorToInt(elapsed / Mathf.Max(0.1f, slideDuration)) % slides.Length);
            yield return null;
        }
        if (bgmManager != null) bgmManager.StopCreditsBGM();
        creditsText.gameObject.SetActive(false);
        if (thankYouText != null)
        {
            thankYouText.text = thankYouMessage;
            thankYouText.gameObject.SetActive(true);
        }
        else
        {
            // If no separate message was assigned, show it within the left viewport.
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = creditsViewport.rect.size;
            creditsText.text = thankYouMessage;
            creditsText.alignment = TextAlignmentOptions.Center;
            creditsText.gameObject.SetActive(true);
        }
        if (restartButton != null) restartButton.SetActive(true);
        // Keep the background, last slide and thank-you message on screen.
    }
    private void ShowSlide(int index)
    {
        if (slideshowImage == null || slides == null || slides.Length == 0) return;
        slideshowImage.sprite = slides[index];
        slideshowImage.enabled = slides[index] != null;
    }
}
