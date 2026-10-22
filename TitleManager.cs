using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    private bool isLoading;

    // StartボタンのOn Click()に登録する。
    public void StartGame()
    {
        if (isLoading) return;

        if (!Application.CanStreamedLevelBeLoaded("IntroScene"))
        {
            Debug.LogError("IntroScene is not registered in the build scene list.", this);
            return;
        }

        isLoading = true;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync("IntroScene", LoadSceneMode.Single);
    }
}
