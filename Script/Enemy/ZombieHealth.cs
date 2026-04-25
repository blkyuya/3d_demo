using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 僵尸血量：实现 IDamageable，受伤触发材质闪红，死亡后停止寻路和攻击，延迟销毁。
// 闪红用 MaterialPropertyBlock 写，不改 sharedMaterial，避免材质实例爆炸和合批问题。
// 挂载：僵尸预制体根节点，和 NavMeshAgent、ZombieStateMachine 同一物体。
[DisallowMultipleComponent]
public class ZombieHealth : MonoBehaviour, IDamageable
{
    [Header("生命")]
    [Tooltip("最大生命值")]
    public int maxHealth = 3;

    [Header("受击反馈")]
    [Tooltip("留空则自动收集子物体全部 Renderer")]
    public Renderer[] targetRenderers;

    public Color hitColor = Color.red;

    [Tooltip("受击闪色持续时间（秒）")]
    public float hitFlashTime = 0.1f;

    [Header("死亡")]
    [Tooltip("播放死亡动画的 Animator")]
    public Animator animator;

    [Tooltip("死亡动画播完后延迟多久销毁")]
    public float destroyDelayAfterDeath = 2.5f;

    private int _currentHealth;
    private MaterialPropertyBlock _mpb;
    private Renderer[] _cachedRenderers;

    // 最后一次受击时间，连续射击时每发刷新，停火后 hitFlashTime 秒必恢复
    private float _lastHitTime = -999f;
    private bool _hitFlashVisualActive;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // URP Lit shader 的发光色；不清这个可能残留偏色
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // 死亡后 ChaseAI / Attack 读这个提前 return，避免每帧还在寻路
    public bool IsDead { get; private set; }

    // 死亡时触发，HordeWaveManager 用它计数存活数
    public event Action OnDied;

    // 关掉根运动和刚体重力，防止和 NavMeshAgent 抢位置导致陷地
    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator != null)
            animator.applyRootMotion = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    // 初始化血量，缓存 Renderer 列表
    void Start()
    {
        _currentHealth = maxHealth;

        if (targetRenderers != null && targetRenderers.Length > 0)
            _cachedRenderers = targetRenderers;
        else
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    // 用 unscaledTime 计时，timeScale 变了闪红时长也不变；到时间自动清色
    void LateUpdate()
    {
        if (IsDead || !_hitFlashVisualActive || _cachedRenderers == null)
            return;

        if (Time.unscaledTime - _lastHitTime >= hitFlashTime)
        {
            ClearFlashAll();
            _hitFlashVisualActive = false;
        }
    }

    // 实现 IDamageable，扣血，触发闪红，血量归零调 Die
    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        _currentHealth -= damage;
        _lastHitTime = Time.unscaledTime;
        _hitFlashVisualActive = true;
        PlayHitFlash();

        if (_currentHealth <= 0)
            Die();
    }

    // 对所有缓存 Renderer 的所有材质槽写闪红色
    void PlayHitFlash()
    {
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            return;
        ApplyFlashColorAll(hitColor);
    }

    // 逐 Renderer 逐材质槽写 MPB，解决只有身体变红、衣服不变的问题
    void ApplyFlashColorAll(Color c)
    {
        if (_cachedRenderers == null)
            return;

        foreach (var r in _cachedRenderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) continue;

            for (int mi = 0; mi < mats.Length; mi++)
            {
                if (mats[mi] == null) continue;
                r.GetPropertyBlock(_mpb, mi);
                if (mats[mi].HasProperty(BaseColorId))
                    _mpb.SetColor(BaseColorId, c);
                else if (mats[mi].HasProperty(ColorId))
                    _mpb.SetColor(ColorId, c);
                r.SetPropertyBlock(_mpb, mi);
            }
        }
    }

    // 清闪色时把 sharedMaterial 上的默认色写回 MPB，确保一定能还原
    // 只清 MPB 在部分 URP/SRP Batcher 下可能不够用
    void ClearFlashAll()
    {
        if (_cachedRenderers == null)
            return;

        foreach (var r in _cachedRenderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                r.SetPropertyBlock(null);
                continue;
            }

            for (int mi = 0; mi < mats.Length; mi++)
            {
                var m = mats[mi];
                if (m == null) { r.SetPropertyBlock(null, mi); continue; }

                r.GetPropertyBlock(_mpb, mi);
                if (m.HasProperty(BaseColorId))
                    _mpb.SetColor(BaseColorId, m.GetColor(BaseColorId));
                if (m.HasProperty(ColorId))
                    _mpb.SetColor(ColorId, m.GetColor(ColorId));
                if (m.HasProperty(EmissionColorId))
                    _mpb.SetColor(EmissionColorId, m.GetColor(EmissionColorId));
                r.SetPropertyBlock(_mpb, mi);
            }
        }
    }

    // 标记死亡，停 NavMeshAgent，禁用攻击和追击组件，播死亡动画，延迟销毁
    void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        OnDied?.Invoke();

        _hitFlashVisualActive = false;
        ClearFlashAll();

        // isStopped 而不是 enabled=false，部分版本禁用 Agent 会导致 Transform 错位插地
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        foreach (var atk in GetComponents<ZombieAttack>())
            atk.enabled = false;
        foreach (var chase in GetComponents<ZombieChaseAI>())
            chase.enabled = false;

        var fsm = GetComponent<ZombieStateMachine>();
        if (fsm != null)
            fsm.enabled = false;

        var bridge = GetComponent<ZombieAnimationBridge>();
        if (bridge != null)
            bridge.enabled = false;

        if (animator != null)
        {
            // 关根运动，Mixamo 死亡动画带位移，叠在 NavMesh 上容易插地
            animator.applyRootMotion = false;
            animator.speed = 1f;
            animator.SetBool("IsDead", true);
            // Bool 触发偶尔有延迟，直接 CrossFade 双保险
            animator.CrossFade("Death", 0.12f, 0, 0f);
        }

        StartCoroutine(DestroyAfterDelay());
    }

    // 等死亡动画播完再销毁
    IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelayAfterDeath);
        Destroy(gameObject);
    }
}
