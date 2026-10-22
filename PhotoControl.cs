using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotoControl : MonoBehaviour
{
    [SerializeField] private FriendEscape friendEscape;
    [SerializeField] private GameObject photoPanel;
    [SerializeField] private GameObject controlsText;
    [SerializeField] private GameObject instructionText;
    [SerializeField, Min(0f)] private float instructionDisplayDuration = 1f;
    private float instructionHideTime;
    [SerializeField] private KeyCode toggleKey = KeyCode.C;
    [SerializeField] private KeyCode shutterKey = KeyCode.Space;
    [SerializeField] private Transform friendRoot;
    [Tooltip("Place an empty child at the friend's chest.")]
    [SerializeField] private Transform photoTarget;
    [Tooltip("Player root, used to ignore the player's own colliders.")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField] private ScreenFade screenFade;
    [SerializeField] private string creditsSceneName = "CreditsScene";
    [SerializeField, Min(0f)] private float fadeDuration = 1f;
    private bool photoMode;
    private bool capturing;
    private bool transitioning;
    private bool Available => !transitioning && friendEscape != null && friendEscape.CanUsePhotoMode;

    private void Awake()
    {
        SetMode(false);
        if (controlsText != null) controlsText.SetActive(false);
        if (playerRoot == null)
        {
            playercontroller player = GetComponentInParent<playercontroller>();
            if (player != null) playerRoot = player.transform;
        }
    }
    private void Update()
    {
        bool available = Available;
        if (controlsText != null) controlsText.SetActive(available && !photoMode);
        if (!available) { if (photoMode) SetMode(false); return; }
        if (Input.GetKeyDown(toggleKey)) SetMode(!photoMode);
        if (photoMode && instructionText != null && Time.unscaledTime >= instructionHideTime)
            instructionText.SetActive(false);
        if (photoMode && !capturing && Input.GetKeyDown(shutterKey))
        {
            capturing = true;
            StartCoroutine(TakePhoto());
        }
    }
    private void SetMode(bool active)
    {
        photoMode = active && photoPanel != null;
        if (photoPanel != null) photoPanel.SetActive(photoMode);
        if (controlsText != null) controlsText.SetActive(Available && !photoMode);
        if (instructionText != null)
        {
            instructionText.SetActive(photoMode);
            if (photoMode)
                instructionHideTime = Time.unscaledTime + instructionDisplayDuration;
        }
    }
    private bool FriendIsVisible(Camera camera)
    {
        if (friendRoot == null || photoTarget == null || playerRoot == null
            || !friendRoot.gameObject.activeInHierarchy || !photoTarget.IsChildOf(friendRoot))
        {
            Debug.LogError("PhotoControl: assign Friend Root, its chest Photo Target, and Player Root.", this);
            return false;
        }
        Vector3 viewport = camera.WorldToViewportPoint(photoTarget.position);
        if (viewport.z < camera.nearClipPlane || viewport.z > camera.farClipPlane
            || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            return false;
        bool rendered = false;
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        foreach (Renderer renderer in friendRoot.GetComponentsInChildren<Renderer>())
            if (renderer.enabled && !renderer.forceRenderingOff
                && renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0
                && GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                rendered = true;
        if (!rendered) return false;

        Physics.SyncTransforms();
        Vector3 offset = photoTarget.position - camera.transform.position;
        foreach (RaycastHit hit in Physics.RaycastAll(camera.transform.position, offset.normalized,
            offset.magnitude, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform.IsChildOf(friendRoot) || hitTransform.IsChildOf(playerRoot)) continue;
            return false;
        }
        return true;
    }
    private IEnumerator TakePhoto()
    {
        yield return new WaitForEndOfFrame();
        if (!Available || !photoMode || !FriendIsVisible(friendEscape.FirstPersonCamera))
        {
            capturing = false;
            yield break;
        }
        if (screenFade == null || !screenFade.IsReady
            || string.IsNullOrWhiteSpace(creditsSceneName) || !Application.CanStreamedLevelBeLoaded(creditsSceneName))
        {
            Debug.LogError("PhotoControl: assign ScreenFade and register the credits scene in the build scene list.", this);
            capturing = false;
            yield break;
        }
        if (!friendEscape.BeginGameClear(screenFade, creditsSceneName, fadeDuration))
        {
            capturing = false;
            yield break;
        }
        transitioning = true;
        SetMode(false);
        if (controlsText != null) controlsText.SetActive(false);
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        capturing = false;
        SetMode(false);
        if (controlsText != null) controlsText.SetActive(false);
    }
}
