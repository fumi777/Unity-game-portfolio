using UnityEngine;
[RequireComponent(typeof(CharacterController))]
public class FriendController : MonoBehaviour
{
    [SerializeField] private Transform[] checkpoints;
    [SerializeField, Min(0), Tooltip("最後のポイントの次に向かうCheckpointsの番号。0が先頭です。")]
    private int loopStartIndex;
    private int activeLoopStartIndex;
    [SerializeField, Min(0.1f)] private float runSpeed = 5f;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string runningParameter = "IsRunning";
    private int speedHash;
    private int runningHash;
    private bool hasSpeed;
    private bool hasRunning;
    private CharacterController controller;
    private float verticalSpeed;
    private bool escaping;
    public int PointIndex { get; private set; }
    // 周回しても戻さない通過順。プレイヤーとの進み具合の比較に使う。
    public int RouteStep { get; private set; }
    public Transform[] Checkpoints => checkpoints;
    public bool HasRoute => checkpoints != null && checkpoints.Length > 0
        && System.Array.TrueForAll(checkpoints, point => point != null);
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null) controller = gameObject.AddComponent<CharacterController>();
        // 通常時は移動用の当たり判定を無効にし、会話用Triggerだけを使う。
        controller.enabled = false;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.applyRootMotion = false;
            speedHash = Animator.StringToHash(speedParameter);
            runningHash = Animator.StringToHash(runningParameter);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float)
                    hasSpeed = true;
                if (parameter.nameHash == runningHash && parameter.type == AnimatorControllerParameterType.Bool)
                    hasRunning = true;
            }
            if (!hasSpeed)
                Debug.LogWarning("Friend Animator needs a Float parameter named " + speedParameter, this);
            UpdateAnimation(0f);
        }
    }
    public void BeginEscape()
    {
        if (!HasRoute) return;
        activeLoopStartIndex = Mathf.Clamp(loopStartIndex, 0, checkpoints.Length - 1);
        controller.enabled = true;
        verticalSpeed = 0f;
        PointIndex = 0;
        RouteStep = 0;
        escaping = true;
        if (animator != null) animator.applyRootMotion = false;
    }
    private int GetPointIndex(int routeStep)
    {
        if (routeStep < checkpoints.Length) return routeStep;
        int loopLength = checkpoints.Length - activeLoopStartIndex;
        return activeLoopStartIndex + (routeStep - checkpoints.Length) % loopLength;
    }

    public Transform GetRoutePoint(int routeStep)
    {
        return checkpoints[GetPointIndex(routeStep)];
    }

    private void Update()
    {
        if (!escaping) return;
        Vector3 direction = Vector3.zero;
        // 重なったポイントや1点だけのルートでも無限ループしない。
        for (int checkedPoints = 0; checkedPoints < checkpoints.Length; checkedPoints++)
        {
            direction = checkpoints[PointIndex].position - transform.position;
            direction.y = 0f;
            if (direction.magnitude > 0.2f) break;
            RouteStep++;
            PointIndex = GetPointIndex(RouteStep);
            direction = Vector3.zero;
        }
        bool moving = direction.sqrMagnitude > 0.001f;
        if (moving) transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction), 360f * Time.deltaTime);
        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += Physics.gravity.y * Time.deltaTime;
        Vector3 step = Vector3.ClampMagnitude(direction, runSpeed * Time.deltaTime);
        step.y = verticalSpeed * Time.deltaTime;
        controller.Move(step);
        Vector3 horizontalVelocity = controller.velocity;
        horizontalVelocity.y = 0f;
        UpdateAnimation(horizontalVelocity.magnitude);
    }

    private void UpdateAnimation(float speed)
    {
        if (animator == null) return;
        if (speed < 0.05f) speed = 0f;
        // 実際の水平速度を渡す。0～1への正規化や補間は行わない。
        if (hasSpeed) animator.SetFloat(speedHash, speed);
        if (hasRunning) animator.SetBool(runningHash, speed > 0f);
    }
}
