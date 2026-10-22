using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class playercontroller : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 2f;
    [SerializeField] private float fallResetY = -20f;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField, Min(0f)] private float animationDampTime = 0.1f;
    private int speedParameterHash;
    private bool hasSpeedParameter;
    private bool isReloading;
    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation;
    public Vector3 CheckpointPosition => checkpointPosition;
    public Quaternion CheckpointRotation => checkpointRotation;

    private CharacterController controller;
    private float verticalSpeed;
    private bool inputLocked;
    private FriendController followFriend;
    private int followPoint;
    private float followSpeed;
    private float followDistance;

    public void LockInput() { inputLocked = true; }

    public void BeginFollowing(FriendController target, float speed, float distance)
    {
        inputLocked = true;
        followFriend = target;
        followPoint = 0;
        followSpeed = speed;
        followDistance = distance;
    }

    private void Awake()
    {
        checkpointPosition = transform.position;
        checkpointRotation = transform.rotation;
        controller = GetComponent<CharacterController>();

        // Handle objects that already had this script before RequireComponent was added.
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            // 移動はCharacterControllerで行い、アニメーションは見た目だけに使う。
            animator.applyRootMotion = false;
            speedParameterHash = Animator.StringToHash(speedParameter);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == speedParameterHash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasSpeedParameter = true;
                    break;
                }
            }
            if (!hasSpeedParameter)
                Debug.LogWarning("Animator needs a Float parameter named " + speedParameter, this);
        }
    }

    private void Start()
    {
        SetCursorLocked(true);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
        if (animator != null && hasSpeedParameter) animator.SetFloat(speedParameterHash, 0f);
    }

    private void RestartAfterFall()
    {
        foreach (GameProgressManager progress in FindObjectsByType<GameProgressManager>(FindObjectsSortMode.None))
        {
            if (progress.isActiveAndEnabled && progress.gameObject.scene == gameObject.scene
                && progress.RespawnFromCheckpoint())
            {
                isReloading = true;
                return;
            }
        }

        if (!Application.CanStreamedLevelBeLoaded("MainScene"))
        {
            Debug.LogError("MainScene is not registered in the build scene list.", this);
            enabled = false;
            return;
        }

        isReloading = true;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Single);
    }

    public void RestoreCheckpoint(Vector3 position, Quaternion rotation)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;
        verticalSpeed = 0f;
        inputLocked = false;
        followFriend = null;
        isReloading = false;
        checkpointPosition = position;
        checkpointRotation = rotation;
    }

    void Update()
    {
        if (isReloading) return;
        if (transform.position.y < fallResetY)
        {
            RestartAfterFall();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
        else if (!inputLocked && Input.GetMouseButtonDown(0)) SetCursorLocked(true);

        if (!inputLocked && Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f, Space.World);
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (!inputLocked)
        {
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) horizontal += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) vertical -= 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) vertical += 1f;
        }

        // 移動方向もプレイヤーの向きに合わせる。
        Vector3 direction = transform.right * horizontal + transform.forward * vertical;
        direction.y = 0f;
        direction.Normalize();
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        if (inputLocked)
        {
            direction = Vector3.zero;
            currentSpeed = followSpeed;
            if (followFriend != null)
            {
                if (followFriend.HasRoute)
                {
                    // 周回を含めた通過順で追う。フレンドが周回しても途中を飛ばさない。
                    for (int checkedPoints = 0; checkedPoints < followFriend.Checkpoints.Length; checkedPoints++)
                    {
                        Vector3 reachedOffset = followFriend.GetRoutePoint(followPoint).position - transform.position;
                        reachedOffset.y = 0f;
                        if (reachedOffset.magnitude > 0.2f || followPoint >= followFriend.RouteStep) break;
                        followPoint++;
                    }
                    Vector3 gap = followFriend.transform.position - transform.position;
                    gap.y = 0f;
                    if (gap.magnitude > followDistance)
                    {
                        Vector3 offset = followFriend.GetRoutePoint(followPoint).position - transform.position;
                        offset.y = 0f;
                        direction = offset.normalized;
                        currentSpeed = Mathf.Min(followSpeed,
                            Mathf.Min(offset.magnitude, gap.magnitude - followDistance)
                            / Mathf.Max(Time.deltaTime, 0.0001f));
                        if (direction.sqrMagnitude > 0.001f)
                            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                Quaternion.LookRotation(direction), 360f * Time.deltaTime);
                    }
                }
            }
        }
        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }

        verticalSpeed += gravity * Time.deltaTime;
        Vector3 velocity = direction * currentSpeed;
        velocity.y = verticalSpeed;

        CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
        if (controller.isGrounded && !inputLocked && transform.position.y >= fallResetY)
        {
            checkpointPosition = transform.position;
            checkpointRotation = transform.rotation;
        }
        UpdateAnimation();
        if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }
        else if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
        {
            verticalSpeed = 0f;
        }

    }

    private void UpdateAnimation()
    {
        if (animator == null || !hasSpeedParameter) return;

        Vector3 horizontalVelocity = controller.velocity;
        horizontalVelocity.y = 0f;
        float runSpeed = Mathf.Max(0.01f, moveSpeed * sprintMultiplier);
        float normalizedSpeed = Mathf.Clamp01(horizontalVelocity.magnitude / runSpeed);
        if (horizontalVelocity.magnitude < 0.05f) normalizedSpeed = 0f;
        animator.SetFloat(speedParameterHash, normalizedSpeed, animationDampTime, Time.deltaTime);
    }
}
