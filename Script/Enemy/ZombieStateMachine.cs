using UnityEngine;
using UnityEngine.AI;

// 僵尸状态机：把追击 AI 的 if/else 分支拆成独立状态类，状态切换逻辑更清晰，后期加新状态不动原状态。
// 状态流转：Idle──察觉──▶Chase──近身──▶Attack──远离──▶Chase，任何状态死亡都切到 Dead。
// 挂载：僵尸预制体根节点，和 ZombieChaseAI / ZombieAttack / ZombieHealth 同层。
// 挂上本脚本后可以禁用 ZombieChaseAI，两者互不干扰，本脚本优先。
[RequireComponent(typeof(NavMeshAgent))]
public class ZombieStateMachine : MonoBehaviour
{
    [Header("引用（可留空，自动查找）")]
    [Tooltip("玩家目标，留空则 Start 时按 Tag 查找")]
    public Transform playerTarget;

    [Tooltip("攻击组件，留空则自动 GetComponent")]
    public ZombieAttack zombieAttack;

    [Tooltip("血量组件，留空则自动 GetComponent")]
    public ZombieHealth zombieHealth;

    [Header("感知")]
    [Tooltip("进入追击的检测距离")]
    public float detectionRange = 12f;

    [Tooltip("OnDrawGizmosSelected 中绘制警戒圈颜色")]
    public Color gizmoDetectColor = Color.yellow;

    [Header("移动")]
    [Tooltip("追击速度（覆盖 NavMeshAgent.speed）")]
    public float moveSpeed = 2.2f;

    [Tooltip("转向速度（覆盖 NavMeshAgent.angularSpeed）")]
    public float angularSpeed = 360f;

    // 各状态类通过 machine 参数读这些属性驱动行为
    public NavMeshAgent Agent { get; private set; }
    public Transform PlayerTarget => playerTarget;
    public ZombieAttack Attack => zombieAttack;
    public ZombieHealth Health => zombieHealth;

    // 优先读 ZombieAttack.attackRange，没有攻击组件时用默认值
    public float AttackRange => zombieAttack != null ? zombieAttack.attackRange : 1.8f;

    // Inspector 实时监视当前状态名，调试用，readonly
    [Header("调试（只读）")]
    [SerializeField] private string _currentStateName = "—";

    IZombieState _currentState;

    // 状态类复用，不每帧 new，避免 GC 压力
    readonly ZombieIdleState _idle = new ZombieIdleState();
    readonly ZombieChaseState _chase = new ZombieChaseState();
    readonly ZombieAttackState _attack = new ZombieAttackState();
    readonly ZombieDeadState _dead = new ZombieDeadState();

    // 缓存 Agent 和子组件引用
    void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        if (zombieAttack == null)
            zombieAttack = GetComponent<ZombieAttack>();
        if (zombieHealth == null)
            zombieHealth = GetComponent<ZombieHealth>();
    }

    // 查找玩家，配置 Agent 参数，订阅死亡事件，进入初始 Idle 状态
    void Start()
    {
        if (playerTarget == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) playerTarget = player.transform;
        }

        if (Agent != null)
        {
            Agent.speed = moveSpeed;
            Agent.angularSpeed = angularSpeed;
        }

        // 订阅死亡事件，无论当前处于哪个状态都能立即切换到 Dead
        if (zombieHealth != null)
            zombieHealth.OnDied += OnZombieDied;

        ChangeState(_idle);
    }

    // 物体销毁时取消订阅，防止 delegate 持有已销毁引用报错
    void OnDestroy()
    {
        if (zombieHealth != null)
            zombieHealth.OnDied -= OnZombieDied;
    }

    // 每帧驱动当前状态的 OnUpdate
    void Update()
    {
        _currentState?.OnUpdate(this);
    }

    // 切换状态：调用旧状态 OnExit → 切换引用 → 调用新状态 OnEnter
    public void ChangeState(IZombieState next)
    {
        _currentState?.OnExit(this);
        _currentState = next;
        _currentStateName = next?.GetType().Name ?? "null";
        _currentState?.OnEnter(this);
    }

    // 供状态类用的快捷属性，避免在状态里重复实例化
    public IZombieState IdleState => _idle;
    public IZombieState ChaseState => _chase;
    public IZombieState AttackState => _attack;
    public IZombieState DeadState => _dead;

    // ZombieHealth.OnDied 触发时调这里，统一切换到死亡状态
    void OnZombieDied()
    {
        ChangeState(_dead);
    }

    // Scene 视图中显示检测范围（黄）和攻击范围（红），方便调整参数
    void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoDetectColor;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }

    // ─────────────────────────────────────────────────────
    //  具体状态类：嵌套在 ZombieStateMachine 里，直接访问 machine 字段
    // ─────────────────────────────────────────────────────

    // 待机状态：Agent 停止，每帧检测玩家距离，进入检测范围后切换到 Chase
    class ZombieIdleState : IZombieState
    {
        public void OnEnter(ZombieStateMachine m)
        {
            if (m.Agent != null)
            {
                m.Agent.isStopped = true;
                m.Agent.ResetPath();
            }
        }

        // 每帧量一次距离，超出触发范围就切追击
        public void OnUpdate(ZombieStateMachine m)
        {
            if (m.PlayerTarget == null) return;
            float dist = Vector3.Distance(m.transform.position, m.PlayerTarget.position);
            if (dist <= m.detectionRange)
                m.ChangeState(m.ChaseState);
        }

        public void OnExit(ZombieStateMachine m) { }
    }

    // 追击状态：持续向玩家 SetDestination
    // 玩家超出 detectionRange → 回 Idle，进入 AttackRange → 切 Attack
    class ZombieChaseState : IZombieState
    {
        public void OnEnter(ZombieStateMachine m)
        {
            if (m.Agent != null)
            {
                m.Agent.isStopped = false;
                m.Agent.stoppingDistance = m.AttackRange;
            }
        }

        // 每帧刷新目标位置，防止玩家跑动时走老路
        public void OnUpdate(ZombieStateMachine m)
        {
            if (m.PlayerTarget == null) return;
            float dist = Vector3.Distance(m.transform.position, m.PlayerTarget.position);

            if (dist <= m.AttackRange)
            {
                m.ChangeState(m.AttackState);
                return;
            }

            if (dist > m.detectionRange)
            {
                m.ChangeState(m.IdleState);
                return;
            }

            if (m.Agent != null && m.Agent.isOnNavMesh)
                m.Agent.SetDestination(m.PlayerTarget.position);
        }

        public void OnExit(ZombieStateMachine m) { }
    }

    // 攻击状态：Agent 原地停止，朝向玩家
    // 伤害逻辑由 ZombieAttack 组件自己计时处理，状态机不重复实现
    // 玩家离开 AttackRange * 1.2f 缓冲后回 Chase
    class ZombieAttackState : IZombieState
    {
        public void OnEnter(ZombieStateMachine m)
        {
            if (m.Agent != null)
            {
                m.Agent.isStopped = true;
                m.Agent.ResetPath();
            }
        }

        public void OnUpdate(ZombieStateMachine m)
        {
            if (m.PlayerTarget == null) return;
            FacePlayer(m);
            float dist = Vector3.Distance(m.transform.position, m.PlayerTarget.position);
            // 加 1.2 倍缓冲，防止玩家在攻击边缘反复触发状态切换（抖动）
            if (dist > m.AttackRange * 1.2f)
                m.ChangeState(m.ChaseState);
        }

        public void OnExit(ZombieStateMachine m) { }

        // Slerp 朝向，angularSpeed 用 Deg2Rad 换算保证单位一致
        static void FacePlayer(ZombieStateMachine m)
        {
            Vector3 dir = m.PlayerTarget.position - m.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion target = Quaternion.LookRotation(dir.normalized);
            m.transform.rotation = Quaternion.Slerp(
                m.transform.rotation, target,
                m.angularSpeed * Time.deltaTime * Mathf.Deg2Rad);
        }
    }

    // 死亡状态：停 Agent，禁用攻击组件，不再驱动任何行为
    // 动画播放和 GameObject 销毁由 ZombieHealth 负责，这里只让 FSM 静止
    class ZombieDeadState : IZombieState
    {
        public void OnEnter(ZombieStateMachine m)
        {
            if (m.Agent != null)
            {
                m.Agent.isStopped = true;
                m.Agent.ResetPath();
            }
            // ZombieHealth.Die 也会禁用 Attack，这里是双保险
            if (m.zombieAttack != null)
                m.zombieAttack.enabled = false;
        }

        public void OnUpdate(ZombieStateMachine m) { }
        public void OnExit(ZombieStateMachine m) { }
    }
}
