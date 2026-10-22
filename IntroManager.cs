using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class IntroManager : MonoBehaviour
{
    [SerializeField] private TMP_Text introText;
    [SerializeField] private ScreenFade screenFade;
    [SerializeField, Min(0f)] private float fadeDuration = 1f;
    [SerializeField, TextArea(3, 10)] private string[] messages =
        { "ここにゲームの状況やストーリーを入力してください。" };
    private int currentPage;
    private bool isLoading;

    private void Start()
    {
        if (introText == null || messages == null || messages.Length == 0)
        {
            Debug.LogError("IntroManager: assign Intro Text and at least one Messages page.", this);
            enabled = false;
            return;
        }
        currentPage = 0;
        introText.gameObject.SetActive(true);
        introText.text = messages[currentPage];
    }

    private void Update()
    {
        if (isLoading || !(Input.GetKeyDown(KeyCode.G) || Input.GetMouseButtonDown(0))) return;

        if (currentPage < messages.Length - 1)
        {
            currentPage++;
            introText.text = messages[currentPage];
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded("MainScene"))
        {
            Debug.LogError("MainScene is not registered in the build scene list.", this);
            return;
        }
        if (screenFade == null || !screenFade.isActiveAndEnabled || !screenFade.IsReady)
        {
            Debug.LogError("IntroManager: assign an active ScreenFade for the scene transition.", this);
            return;
        }
        isLoading = true;
        StartCoroutine(LoadMainScene());
    }

    private IEnumerator LoadMainScene()
    {
        Time.timeScale = 1f;
        yield return screenFade.FadeTo(1f, fadeDuration);
        ScreenFade.RequestSceneFadeIn("MainScene", fadeDuration);
        yield return SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Single);
    }
}
