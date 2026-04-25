using UnityEngine;
using UnityEngine.AI;

// 僵尸动画桥接：把 NavMeshAgent 的实际移动速度映射到 Animator 的 Speed 参数，驱动 Blend Tree（Idle/Walk）。
// 位移完全由 Agent 负责，动画只做姿态；因此关闭 applyRootMotion，避免与 Agent 抢位置导致打滑。
// 挂载：僵尸预制体根节点，与 NavMeshAgent 同层。
public class ZombieAnimationBridge : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;

    [Header("与 Animator 混合树对齐")]
    [Tooltip("混合树 Walk 阈值（默认 2.5），速度映射上限")]
    [SerializeField] private float blendTreeMaxSpeed = 2.5f;

    [Tooltip("速度映射倍率，动画过快/过慢时微调")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("Speed 参数平滑时间，防止 Agent 速度抖动导致混合树在 Idle/Walk 间频繁跳变")]
    [SerializeField] private float speedSmoothTime = 0.08f;

    private ZombieHealth _zombieHealth;
    private float _smoothedSpeed;
    private float _speedSmoothVel;

    // 关闭根运动，缓存组件
    private void Awake()
    {
        _zombieHealth = GetComponent<ZombieHealth>();
        if (animator == null)
            animator = GetComponent<Animator>();
        // NavMesh 驱动位移，关闭根运动防止与 Agent 抢位、出现原地打滑
        if (animator != null)
            animator.applyRootMotion = false;
    }

    // Reset 时自动补引用，方便在 Inspector 添加组件后直接可用
    private void Reset()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    // 每帧从 Agent.velocity 读实际速度，SmoothDamp 平滑后写 Speed 参数
    private void Update()
    {
        if (_zombieHealth != null && _zombieHealth.IsDead) return;
        if (animator == null) return;

        // agent.velocity.magnitude 是当前实际移动速率，单位和 Blend Tree 阈值一致（米/秒）
        float rawSpeed = 0f;
        if (agent != null)
            rawSpeed = agent.velocity.magnitude * speedMultiplier;

        rawSpeed = Mathf.Clamp(rawSpeed, 0f, blendTreeMaxSpeed);

        // SmoothDamp 比直接 SetFloat 更稳，步态切换更自然
        if (speedSmoothTime > 0f)
            _smoothedSpeed = Mathf.SmoothDamp(_smoothedSpeed, rawSpeed, ref _speedSmoothVel, speedSmoothTime);
        else
            _smoothedSpeed = rawSpeed;

        animator.SetFloat("Speed", _smoothedSpeed);

        // 注意：不要修改 animator.speed（全局速度）；Locomotion 节奏应由 Blend Tree 各 Motion 的 Speed 控制
        animator.speed = 1f;
    }
}
