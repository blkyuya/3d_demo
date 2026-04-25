using UnityEngine;

// 位移估算 Speed 并写入 Animator，用于持枪时的行走/奔跑混合树驱动。
// 空手时由 WeaponHolder 临时禁用本组件，仅让 ThirdPersonController 写 Speed，防止两者竞争参数导致走路异常。
// Starter 混合树阈值为 0/2/6，所以 Speed 范围写到 animatorSpeedBlendMax=6，不用 0~1。
// 挂载：PlayerArmature 根节点。
public class PlayerAnimationBridge : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("新人物模型上的 Animator，比如 unitychan")]
    public Animator characterAnimator;

    [Tooltip("是否自动在子物体中查找 Animator")]
    public bool autoFindAnimator = true;

    [Header("动画参数")]
    [Tooltip("Animator 中控制移动速度的参数名")]
    public string speedParamName = "Speed";

    [Tooltip("Animator 中控制方向的参数名")]
    public string directionParamName = "Direction";

    [Tooltip("水平位移推算时的总系数")]
    public float speedMultiplier = 1.0f;

    [Tooltip("混合树 Speed 上限（阈值为 0/2/6）")]
    public float animatorSpeedBlendMax = 6f;

    [Tooltip("世界水平速度达到该值时 Speed 映射到 animatorSpeedBlendMax，建议和 ThirdPersonController.SprintSpeed 一致")]
    public float referenceWorldSpeed = 5.35f;

    [Tooltip("SmoothDamp 时间，越小响应越灵敏")]
    public float dampTime = 0.1f;

    Vector3 lastPosition;
    float currentSpeedVelocity;
    bool _hasSpeedParam;
    bool _hasDirectionParam;

    // 查找 Animator，初始化参数名存在性缓存，关闭根运动
    void Start()
    {
        if (autoFindAnimator && characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();

        lastPosition = transform.position;

        if (characterAnimator != null)
        {
            characterAnimator.applyRootMotion = false;
            _hasSpeedParam = HasParam(speedParamName);
            _hasDirectionParam = HasParam(directionParamName);
        }
    }

    // 遍历 Animator 参数列表判断参数是否存在，避免 SetFloat 找不到参数时每帧报错
    bool HasParam(string paramName)
    {
        if (string.IsNullOrEmpty(paramName) || characterAnimator == null)
            return false;
        foreach (AnimatorControllerParameter p in characterAnimator.parameters)
        {
            if (p.name == paramName)
                return true;
        }
        return false;
    }

    // 每帧更新动画参数
    void Update()
    {
        if (characterAnimator == null) return;
        UpdateMovementAnimation();
    }

    // 根据帧间位移估算世界速度，映射到 Starter 混合树的 0~6 范围，SmoothDamp 平滑过渡
    void UpdateMovementAnimation()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;

        float worldSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        // 映射到 [0, animatorSpeedBlendMax]，Starter 混合树用 0/2/6 而不是 0/0.5/1
        float refSpd = Mathf.Max(0.01f, referenceWorldSpeed);
        float t = Mathf.Clamp01(worldSpeed * speedMultiplier / refSpd);
        float targetSpeed = t * animatorSpeedBlendMax;

        float currentSpeed = characterAnimator.GetFloat(speedParamName);
        float smoothedSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref currentSpeedVelocity, dampTime);

        if (_hasSpeedParam)
            characterAnimator.SetFloat(speedParamName, smoothedSpeed);

        if (_hasDirectionParam)
            characterAnimator.SetFloat(directionParamName, 0f);

        lastPosition = transform.position;
    }
}
