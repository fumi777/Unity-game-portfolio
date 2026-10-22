using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class ScreenFade : MonoBehaviour
{
    [SerializeField] private Image fadeImage;
    [SerializeField, Range(0f, 1f)] private float initialAlpha;
    private static string pendingScene;
    private static float pendingDuration;
    private bool fadeInOnStart;
    private float sceneFadeDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSceneFade()
    {
        pendingScene = null;
        pendingDuration = 0f;
    }

    public static void RequestSceneFadeIn(string sceneName, float duration)
    {
        pendingScene = sceneName;
        pendingDuration = Mathf.Max(0f, duration);
    }
    public bool IsReady => fadeImage != null && fadeImage.gameObject.activeInHierarchy;
    private void Awake()
    {
        if (fadeImage == null) fadeImage = GetComponent<Image>();
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = false;
            // 遷移先は最初の描画前から黒くする。通常起動時は既存の設定を使う。
            fadeInOnStart = pendingScene == gameObject.scene.name;
            if (fadeInOnStart)
            {
                sceneFadeDuration = pendingDuration;
                pendingScene = null;
            }
            SetAlpha(fadeInOnStart ? 1f : initialAlpha);
        }
    }
    private IEnumerator Start()
    {
        if (fadeInOnStart)
            yield return FadeTo(0f, sceneFadeDuration);
    }
    public void SetAlpha(float alpha)
    {
        if (fadeImage == null) fadeImage = GetComponent<Image>();
        if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }
    public IEnumerator FadeTo(float alpha, float duration)
    {
        float initial = fadeImage.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f,
                Mathf.Lerp(initial, alpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, alpha);
    }
}
