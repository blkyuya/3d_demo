using System.Collections;
using UnityEngine;

// 僵尸攻击：按固定间隔对玩家造成伤害，同时驱动 Animator 的 IsAttacking 参数。
// 伤害在 Attack() 里立即结算（逻辑帧），与动画时长解耦；
// 攻击动画通过协程延时还原 Bool，不依赖 AnimationEvent，伤害不会因动画长度变化而滞后。
// 挂载：僵尸预制体根节点，与 ZombieStateMachine、ZombieHealth 同节点。
public class ZombieAttack : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家目标，一般拖 PlayerArmature")]
    public Transform playerTarget;

    [Tooltip("玩家血量组件")]
    public PlayerHealth playerHealth;

    [Header("攻击参数")]
    [Tooltip("攻击触发范围（米），和 ZombieStateMachine.AttackRange 保持一致）")]
    public float attackRange = 2.0f;

    [Tooltip("每次攻击造成的伤害")]
    public int damage = 1;

    [Tooltip("攻击间隔（秒）")]
    public float attackCooldown = 1.2f;

    [Tooltip("IsAttacking 保持 true 的时长（秒），应小于 attackCooldown，通常设为攻击动画时长 × 0.8")]
    [SerializeField] private float attackAnimDuration = 0.8f;

    private float lastAttackTime;
    private Coroutine _attackAnimCoroutine;
    private ZombieHealth _zombieHealth;
    private Animator _animator;

    // 缓存同节点组件，Animator 可能在子层级（部分模型结构不同）
    void Awake()
    {
        _zombieHealth = GetComponent<ZombieHealth>();
        _animator = GetComponent<Animator>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    // 按 Tag 查找玩家和血量组件，拖引用更稳，这里做 fallback
    void Start()
    {
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                playerTarget = player.transform;
        }

        if (playerHealth == null && playerTarget != null)
            playerHealth = playerTarget.GetComponent<PlayerHealth>();
    }

    // 每帧检查死亡状态和攻击冷却
    void Update()
    {
        if (_zombieHealth != null && _zombieHealth.IsDead)
        {
            // 死亡后确保攻击动画参数归位，避免 IsAttacking 残留 true
            SetIsAttacking(false);
            return;
        }

        TryAttack();
    }

    // 距离在攻击范围内且冷却结束才发起攻击
    void TryAttack()
    {
        if (playerTarget == null || playerHealth == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
            Attack();
    }

    // 播放音效，立即结算伤害，启动攻击动画协程
    void Attack()
    {
        lastAttackTime = Time.time;

        if (AudioManager.Instance != null)
        {
            string key = Random.value < 0.5f ? AudioKeys.ZombieGroan : AudioKeys.ZombieMoan;
            AudioManager.Instance.PlaySfx3D(key, transform.position);
        }

        // 伤害立即结算（逻辑层），不等动画
        playerHealth.TakeDamage(damage);

        // 上次动画协程还在跑（极短 cooldown 场景）先停掉，避免 true 被提前置 false
        if (_attackAnimCoroutine != null)
            StopCoroutine(_attackAnimCoroutine);
        _attackAnimCoroutine = StartCoroutine(AttackAnimRoutine());
    }

    // IsAttacking = true → 等 attackAnimDuration 秒 → 还原 false
    // Animator 状态机里配套：Locomotion──[IsAttacking=true]→Attack──[ExitTime 85%]→Locomotion
    IEnumerator AttackAnimRoutine()
    {
        SetIsAttacking(true);
        yield return new WaitForSeconds(attackAnimDuration);
        SetIsAttacking(false);
        _attackAnimCoroutine = null;
    }

    // 写 Animator 参数前先确认组件有效，避免销毁时报错
    void SetIsAttacking(bool value)
    {
        if (_animator != null && _animator.isActiveAndEnabled)
            _animator.SetBool("IsAttacking", value);
    }

    // Scene 视图绘制攻击范围，方便和 detectionRange 对比调整
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
