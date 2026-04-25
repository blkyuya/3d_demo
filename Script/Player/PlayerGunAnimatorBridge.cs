using UnityEngine;

// 武器动画桥接：将武器槽位、右键瞄准、射击与换弹同步到 Animator。
// 需要 Animator 控制器包含 GunCombat 层，以及 GunSlot(int)、GunAiming(bool) 参数。
// 射击和换弹通过订阅 HitscanShooter 的 C# event 触发 Trigger，不依赖 Update 轮询。
// 挂载：PlayerArmature 根节点，与 WeaponHolder / HitscanShooter 同层。
[RequireComponent(typeof(Animator))]
public class PlayerGunAnimatorBridge : MonoBehaviour
{
    [Header("引用")]
    public WeaponHolder weaponHolder;
    public PlayerAimState aimState;
    public HitscanShooter hitscanShooter;

    [Header("Animator")]
    [Tooltip("留空则自动取子物体 Animator")]
    public Animator animator;

    [Tooltip("持枪动画层名称，默认 GunCombat")]
    public string gunLayerName = "GunCombat";

    [Tooltip("持枪层权重，通常保持 1")]
    [Range(0f, 1f)]
    public float gunLayerWeight = 1f;

    // Animator.StringToHash 在类加载时计算一次，比每帧传字符串快
    static readonly int GunSlot   = Animator.StringToHash("GunSlot");
    static readonly int GunAiming = Animator.StringToHash("GunAiming");
    const string GunFireParam   = "GunFire";
    const string GunReloadParam = "GunReload";

    int _gunLayerIndex = -1;
    RuntimeAnimatorController _cachedRuntimeController;
    bool _hasGunSlotParam;

    // 找 Animator，初始化层索引和参数缓存
    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        RefreshAnimatorCache();
    }

    // 控制器切换时（空手/持枪切换）重新找层索引和参数，防止用了错误的 Controller
    void RefreshAnimatorCache()
    {
        if (animator == null) return;
        if (animator.runtimeAnimatorController == _cachedRuntimeController) return;

        _cachedRuntimeController = animator.runtimeAnimatorController;
        _gunLayerIndex = animator.GetLayerIndex(gunLayerName);
        _hasGunSlotParam = false;
        foreach (var p in animator.parameters)
        {
            if (p.name == "GunSlot" && p.type == AnimatorControllerParameterType.Int)
            {
                _hasGunSlotParam = true;
                break;
            }
        }
    }

    // 激活时订阅射击和换弹事件
    void OnEnable()
    {
        if (hitscanShooter != null)
        {
            hitscanShooter.OnShotFired += OnShotFired;
            hitscanShooter.OnReloadStarted += OnReloadStarted;
        }
    }

    // 停用时取消订阅，防止组件被禁用后仍收到事件
    void OnDisable()
    {
        if (hitscanShooter != null)
        {
            hitscanShooter.OnShotFired -= OnShotFired;
            hitscanShooter.OnReloadStarted -= OnReloadStarted;
        }
    }

    // 触发射击动画（Trigger 类型，一次性）
    void OnShotFired()
    {
        if (animator != null && _gunLayerIndex >= 0)
            animator.SetTrigger(GunFireParam);
    }

    // 触发换弹动画
    void OnReloadStarted()
    {
        if (animator != null && _gunLayerIndex >= 0)
            animator.SetTrigger(GunReloadParam);
    }

    // 每帧同步武器槽位、瞄准状态和 GunCombat 层权重
    void Update()
    {
        if (animator == null || weaponHolder == null) return;

        RefreshAnimatorCache();

        // 空手且控制器没有 GunSlot 参数时（如 UnityChan Locomotion 控制器）直接跳过，不报错
        if (weaponHolder.IsUnarmed && !_hasGunSlotParam) return;

        if (weaponHolder.IsUnarmed)
        {
            // 空手：GunSlot=0，GunAiming=false，持枪层权重置 0
            animator.SetInteger(GunSlot, 0);
            animator.SetBool(GunAiming, false);
            if (_gunLayerIndex >= 0)
                animator.SetLayerWeight(_gunLayerIndex, 0f);
            return;
        }

        // 1=手枪，2=长枪；与 WeaponHolder.weapons 列表下标 +1 对应
        int slot = Mathf.Clamp(weaponHolder.CurrentWeaponIndex + 1, 1, 2);
        animator.SetInteger(GunSlot, slot);
        animator.SetBool(GunAiming, aimState != null && aimState.IsAiming);

        if (_gunLayerIndex >= 0)
            animator.SetLayerWeight(_gunLayerIndex, gunLayerWeight);
    }
}
