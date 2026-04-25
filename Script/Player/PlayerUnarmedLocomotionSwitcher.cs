using UnityEngine;

// 空手/持枪 Animator 切换器：
// - 空手时切换到 UnityChanLocomotions（或指定空手控制器）；
// - 持枪时切回 StarterAssetsThirdPerson_Gun。
// LateUpdate 里对 UnityChan 控制器做水平速度→Speed 映射（0～0.8），
// StarterAssets 控制器由 ThirdPersonController 自行写 Speed，此处不覆写以免缺参报错。
// 挂载：与 WeaponHolder 同物体；Animator 可在子物体，CharacterController 可在父级胶囊体。
public class PlayerUnarmedLocomotionSwitcher : MonoBehaviour
{
    [Header("引用")]
    public WeaponHolder weaponHolder;

    [Tooltip("留空则同物体上获取")]
    public CharacterController characterController;

    [Header("Animator Controller")]
    [Tooltip("持枪时：如 StarterAssetsThirdPerson_Gun")]
    public RuntimeAnimatorController armedController;

    [Tooltip("空手时：UnityChanLocomotions，或与 ThirdPerson 一致的 StarterAssetsThirdPerson（Locomotion--Run_N 等由混合树驱动）")]
    public RuntimeAnimatorController unarmedController;

    [Header("速度映射")]
    [Tooltip("仅用于 UnityChan 控制器：水平速度达到该值时 Speed 映射到 0.8（与跑速接近即可）")]
    public float maxHorizontalSpeedReference = 5.35f;

    Animator _anim;
    RuntimeAnimatorController _lastApplied;
    bool _lastUnarmed;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int DirectionId = Animator.StringToHash("Direction");

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();
    }

    // WeaponHolder / CC 常挂在 PlayerArmature，也可能在父级；引用留空时自动查找
    void ResolveReferences()
    {
        var bridge = GetComponent<PlayerAnimationBridge>() ?? GetComponentInParent<PlayerAnimationBridge>() ??
                     GetComponentInChildren<PlayerAnimationBridge>(true);
        if (bridge != null && bridge.characterAnimator != null)
            _anim = bridge.characterAnimator;
        else
        {
            _anim = GetComponent<Animator>();
            if (_anim == null)
                _anim = GetComponentInChildren<Animator>(true);
        }

        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();
        if (characterController == null)
            characterController = GetComponentInChildren<CharacterController>(true);

        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (weaponHolder == null)
            weaponHolder = GetComponentInParent<WeaponHolder>();
        if (weaponHolder == null)
            weaponHolder = GetComponentInChildren<WeaponHolder>(true);
    }

    void Start()
    {
        ResolveReferences();

        if (weaponHolder == null || armedController == null || unarmedController == null)
        {
            Debug.LogError(
                "PlayerUnarmedLocomotionSwitcher: 请指定 Weapon Holder、Armed Controller、Unarmed Controller；WeaponHolder 须与数字键武器逻辑在同一角色上。",
                this);
            enabled = false;
            return;
        }

        if (characterController == null)
        {
            Debug.LogWarning(
                "PlayerUnarmedLocomotionSwitcher: 未找到 CharacterController（一般在父级胶囊体）；空手速度映射不可用。",
                this);
            enabled = false;
            return;
        }

        if (armedController != null && armedController.name.IndexOf("Gun", System.StringComparison.OrdinalIgnoreCase) < 0)
        {
            Debug.LogWarning(
                "PlayerUnarmedLocomotionSwitcher: 「持枪控制器」建议使用 StarterAssetsThirdPerson_Gun（含 GunCombat 层），当前为：" +
                armedController.name,
                this);
        }

        _lastUnarmed = weaponHolder.IsUnarmed;
        ApplyController(_lastUnarmed);
    }

    void Update()
    {
        if (weaponHolder == null || _anim == null)
            ResolveReferences();
        if (weaponHolder == null || armedController == null || unarmedController == null)
            return;

        bool unarmed = weaponHolder.IsUnarmed;
        if (unarmed != _lastUnarmed)
        {
            _lastUnarmed = unarmed;
            ApplyController(unarmed);
        }
    }

    void ApplyController(bool unarmed)
    {
        var target = unarmed ? unarmedController : armedController;
        if (target == null || _anim == null)
            return;
        if (_anim.runtimeAnimatorController == target)
        {
            _lastApplied = target;
            return;
        }

        _anim.runtimeAnimatorController = target;
        _lastApplied = target;
    }

    void LateUpdate()
    {
        if (weaponHolder == null || !weaponHolder.IsUnarmed)
            return;
        if (characterController == null || _anim == null || unarmedController == null)
            return;
        if (_anim.runtimeAnimatorController != unarmedController)
            return;

        // Starter Assets 第三人称：Speed 由 ThirdPersonController 写入（约 0～6），无 Direction 参数；此处勿覆写以免报错
        if (unarmedController.name.IndexOf("StarterAssetsThirdPerson", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        // UnityChan Locomotion 外层混合树 Speed 约为 0～0.8
        Vector3 v = characterController.velocity;
        v.y = 0f;
        float mag = v.magnitude;
        float t = Mathf.Clamp01(mag / Mathf.Max(0.01f, maxHorizontalSpeedReference));
        float unityChanSpeed = t * 0.8f;
        _anim.SetFloat(SpeedId, unityChanSpeed);
        if (AnimatorHasFloatParameter(_anim, DirectionId))
            _anim.SetFloat(DirectionId, 0f);
    }

    // 检查 Animator 是否有指定浮点参数，避免对无 Direction 参数的控制器 SetFloat 时产生警告
    static bool AnimatorHasFloatParameter(Animator anim, int parameterHash)
    {
        foreach (var p in anim.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Float)
                continue;
            if (Animator.StringToHash(p.name) == parameterHash)
                return true;
        }
        return false;
    }
}
