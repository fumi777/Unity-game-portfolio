using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
public class FriendEscape : MonoBehaviour
{
    [SerializeField] private FriendController friend;
    [SerializeField] private playercontroller player;
    [SerializeField] private Camera normalCamera;
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private Camera firstPersonCamera;
    [SerializeField] private ScreenFade screenFade;
    [SerializeField] private BGMManager bgmManager;
    [Header("Event instructions")]
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField, TextArea(3, 8)] private string[] instructionPages =
    {
        "フレンドを追いかけよう！\nGキーまたは左クリックで次へ",
        "Cキーでカメラモードを開き、Spaceキーでフレンドを撮影しよう！"
    };
    private bool ownsPause;
    [Header("Game clear")]
    [SerializeField] private GameObject gameClearPanel;
    [SerializeField] private TMP_Text gameClearText;
    [SerializeField, TextArea(3, 8)] private string[] gameClearPages =
    {
        "撮影成功！ゲームクリア！",
        "Gキーまたは左クリックでエンドロールへ進みます。"
    };
    private bool clearing;
    private float timeScaleBeforePause;
    [SerializeField, Min(0f)] private float chaseDelay = 1f;
    [SerializeField, Min(0.1f)] private float chaseSpeed = 5.5f;
    [SerializeField, Min(0.1f)] private float followDistance = 2f;
    [SerializeField, Min(0f)] private float fadeDuration = 1f;
    [Tooltip("Manual camera/input components to disable during the sequence. Do not assign this component or playercontroller.")]
    [SerializeField] private Behaviour[] disableDuringEscape;
    private bool started;
    private bool firstPerson;
    private bool firstPersonReady;
    public Camera FirstPersonCamera => firstPersonCamera;
    public bool CanUsePhotoMode => isActiveAndEnabled && firstPersonReady
        && firstPersonCamera != null && firstPersonCamera.isActiveAndEnabled
        && !normalCamera.isActiveAndEnabled && !cinematicCamera.isActiveAndEnabled;
    private Renderer[] friendRenderers;
    private Renderer[] playerRenderers;
    private readonly List<(Collider friendCollider, Collider playerCollider)> ignoredActorCollisions
        = new List<(Collider, Collider)>();
    private void Start()
    {
        if (started) return;
        if (instructionPanel != null) instructionPanel.SetActive(false);
        if (gameClearPanel != null) gameClearPanel.SetActive(false);
        if (bgmManager == null)
            foreach (BGMManager candidate in FindObjectsByType<BGMManager>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene) { bgmManager = candidate; break; }
        if (cinematicCamera != null) SetCamera(cinematicCamera, false);
        if (firstPersonCamera != null) SetCamera(firstPersonCamera, false);
    }
    public bool BeginEscape()
    {
        if (started) return true;
        if (!isActiveAndEnabled || friend == null || player == null || !friend.HasRoute
            || normalCamera == null || cinematicCamera == null || firstPersonCamera == null
            || normalCamera == cinematicCamera || normalCamera == firstPersonCamera
            || cinematicCamera == firstPersonCamera || screenFade == null || !screenFade.IsReady)
        {
            Debug.LogError("FriendEscape: assign actors, checkpoints, three distinct cameras and ScreenFade.", this);
            return false;
        }
        if (instructionPanel == null || instructionText == null || instructionPages == null
            || instructionPages.Length == 0 || transform.IsChildOf(instructionPanel.transform))
        {
            Debug.LogError("FriendEscape: assign a separate instruction panel, its text, and at least one page.", this);
            return false;
        }
        started = true;
        friendRenderers = friend.GetComponentsInChildren<Renderer>();
        playerRenderers = player.GetComponentsInChildren<Renderer>();
        player.LockInput();
        foreach (friendDialogue dialogue in FindObjectsByType<friendDialogue>(FindObjectsSortMode.None))
            dialogue.enabled = false;
        if (DialogueUI.Instance != null) DialogueUI.Instance.HideDialogue();
        if (disableDuringEscape != null)
            foreach (Behaviour component in disableDuringEscape)
                if (component != null && component != this && component != player
                    && component != friend && component != screenFade)
                    component.enabled = false;
        SetCamera(normalCamera, false);
        SetCamera(firstPersonCamera, false);
        SetCamera(cinematicCamera, true);
        if (bgmManager != null) bgmManager.PlayEventBGM();
        friend.BeginEscape();
        IgnoreActorCollisions();
        StartCoroutine(Sequence());
        return true;
    }
    private void IgnoreActorCollisions()
    {
        // BeginEscapeでCharacterControllerを有効にした後、二人の間だけ衝突を無効化する。
        foreach (Collider friendCollider in friend.GetComponentsInChildren<Collider>())
        {
            if (!friendCollider.enabled || friendCollider.isTrigger) continue;
            foreach (Collider playerCollider in player.GetComponentsInChildren<Collider>())
            {
                if (!playerCollider.enabled || playerCollider.isTrigger || friendCollider == playerCollider
                    || Physics.GetIgnoreCollision(friendCollider, playerCollider)) continue;
                Physics.IgnoreCollision(friendCollider, playerCollider, true);
                ignoredActorCollisions.Add((friendCollider, playerCollider));
            }
        }
    }

    private void RestoreActorCollisions()
    {
        foreach (var pair in ignoredActorCollisions)
            if (pair.friendCollider != null && pair.playerCollider != null)
                Physics.IgnoreCollision(pair.friendCollider, pair.playerCollider, false);
        ignoredActorCollisions.Clear();
    }

    private IEnumerator Sequence()
    {
        yield return new WaitForSeconds(chaseDelay);
        player.BeginFollowing(friend, chaseSpeed, followDistance);
        float outsideTime = 0f;
        while (outsideTime < 0.25f)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cinematicCamera);
            bool visible = IsVisible(planes, friendRenderers, friend.transform)
                || IsVisible(planes, playerRenderers, player.transform);
            outsideTime = visible ? 0f : outsideTime + Time.deltaTime;
            yield return null;
        }
        yield return screenFade.FadeTo(1f, fadeDuration);
        timeScaleBeforePause = Time.timeScale;
        ownsPause = true;
        Time.timeScale = 0f;
        instructionText.text = instructionPages[0];
        instructionPanel.SetActive(true);
        instructionText.gameObject.SetActive(true);
        yield return screenFade.FadeTo(0f, fadeDuration);

        int page = 0;
        // Wait until a new frame so the input that preceded the panel cannot skip page 1.
        while (true)
        {
            yield return null;
            if (!Input.GetKeyDown(KeyCode.G) && !Input.GetMouseButtonDown(0)) continue;
            page++;
            if (page >= instructionPages.Length) break;
            instructionText.text = instructionPages[page];
        }

        yield return screenFade.FadeTo(1f, fadeDuration);
        instructionPanel.SetActive(false);
        SetCamera(cinematicCamera, false);
        SetCamera(firstPersonCamera, true);
        firstPerson = true;
        AimCamera();
        yield return screenFade.FadeTo(0f, fadeDuration);
        ResumeTime();
        firstPersonReady = true;
    }
    public bool BeginGameClear(ScreenFade clearFade, string creditsSceneName, float duration)
    {
        if (clearing) return false;
        if (!CanUsePhotoMode || clearFade == null || !clearFade.IsReady
            || gameClearPanel == null || gameClearText == null
            || gameClearPages == null || gameClearPages.Length == 0
            || transform.IsChildOf(gameClearPanel.transform)
            || clearFade.transform.IsChildOf(gameClearPanel.transform)
            || string.IsNullOrWhiteSpace(creditsSceneName)
            || !Application.CanStreamedLevelBeLoaded(creditsSceneName))
        {
            Debug.LogError("FriendEscape: assign a separate Game Clear Panel, Game Clear Text, pages, fade and a valid credits scene.", this);
            return false;
        }
        clearing = true;
        if (bgmManager != null) bgmManager.StopEventBGM();
        firstPersonReady = false;
        timeScaleBeforePause = Time.timeScale;
        ownsPause = true;
        Time.timeScale = 0f;
        StartCoroutine(GameClearSequence(clearFade, creditsSceneName, duration));
        return true;
    }

    private IEnumerator GameClearSequence(ScreenFade clearFade, string creditsSceneName, float duration)
    {
        yield return clearFade.FadeTo(1f, duration);
        gameClearText.text = gameClearPages[0];
        gameClearPanel.SetActive(true);
        gameClearText.gameObject.SetActive(true);
        yield return clearFade.FadeTo(0f, duration);

        int page = 0;
        while (true)
        {
            yield return null;
            if (!Input.GetKeyDown(KeyCode.G) && !Input.GetMouseButtonDown(0)) continue;
            page++;
            if (page >= gameClearPages.Length) break;
            gameClearText.text = gameClearPages[page];
        }
        yield return clearFade.FadeTo(1f, duration);
        gameClearPanel.SetActive(false);
        // Restore global time before entering the next scene.
        ResumeTime();
        AsyncOperation loading = SceneManager.LoadSceneAsync(creditsSceneName, LoadSceneMode.Single);
        if (loading != null) yield return loading;
    }

    private void ResumeTime()
    {
        if (!ownsPause) return;
        Time.timeScale = timeScaleBeforePause;
        ownsPause = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestoreActorCollisions();
        firstPersonReady = false;
        if (ownsPause)
        {
            ResumeTime();
            if (instructionPanel != null) instructionPanel.SetActive(false);
            if (gameClearPanel != null) gameClearPanel.SetActive(false);
            if (screenFade != null) screenFade.SetAlpha(0f);
        }
    }
    private bool IsVisible(Plane[] planes, Renderer[] renderers, Transform actor)
    {
        bool hasRenderer = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            hasRenderer = true;
            if (GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) return true;
        }
        return !hasRenderer && GeometryUtility.TestPlanesAABB(planes,
            new Bounds(actor.position + Vector3.up, new Vector3(1f, 2f, 1f)));
    }
    private void LateUpdate() { if (firstPerson) AimCamera(); }
    private void AimCamera()
    {
        Vector3 direction = friend.transform.position + Vector3.up - firstPersonCamera.transform.position;
        if (direction.sqrMagnitude > 0.001f)
            firstPersonCamera.transform.rotation = Quaternion.LookRotation(direction);
    }
    private static void SetCamera(Camera camera, bool active)
    {
        if (active) camera.gameObject.SetActive(true);
        camera.enabled = active;
        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = active;
    }
}
