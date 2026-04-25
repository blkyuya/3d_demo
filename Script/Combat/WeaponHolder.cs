using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 武器持有器：管理多把武器和当前索引，数字键 / 滚轮切换，同步弹匣余量到 HitscanShooter。
// 武器模型实例化到右手骨骼下的 WeaponVisualSocket 挂点，LateUpdate 每帧刷新位置偏移，
// 所以 Inspector 里调整 WeaponHandOffset 参数时可以实时看到效果。
// 挂载：PlayerArmature 根节点，与 HitscanShooter、PlayerInventory 同层。
public class WeaponHolder : MonoBehaviour
{
    [Header("武器列表（顺序即 1、2 键）")]
    public List<WeaponDataSO> weapons = new List<WeaponDataSO>();

    [Header("引用")]
    [Tooltip("一般拖同物体上的 HitscanShooter")]
    public HitscanShooter hitscanShooter;

    [Header("手持模型")]
    [Tooltip("为 true 时模型挂在 Animator 右手骨骼（推荐 PlayerArmature）；false 时用下方挂点或 FirePoint")]
    public bool useRightHandBoneForVisuals = true;

    [Tooltip("直接拖入层级里 unitychan 骨架中的 Right_Hand 骨骼；拖了此项则忽略下方 Animator 自动查找")]
    public Transform rightHandBone;

    [Tooltip("备用：指定含骨骼的 Animator；rightHandBone 留空时才用此项自动查找")]
    public Animator characterAnimator;

    [Tooltip("仅在「不用右手骨骼」时生效；可指向空物体做自定义挂点")]
    public Transform weaponAttachPoint;

    [Tooltip("拖入整颗 FBX 或预制体，运行时实例化到挂点下；顺序须与 weapons 一致")]
    public GameObject[] weaponVisualPrefabs;

    [Header("每把武器手部偏移（与 weapons 下标一一对应）")]
    [Tooltip("每把武器相对右手骨骼的位置/旋转/缩放；运行中改数值立即生效")]
    public WeaponHandOffset[] weaponHandOffsets;

    [System.Serializable]
    public class WeaponHandOffset
    {
        [Tooltip("相对 Right_Hand 骨骼的本地位置")]
        public Vector3 socketLocalPosition = new Vector3(0.06f, 0.03f, 0.02f);
        [Tooltip("相对 Right_Hand 骨骼的本地旋转")]
        public Vector3 socketLocalEuler = new Vector3(0f, 0f, 0f);
        [Tooltip("枪模型自身的本地旋转（调整枪口朝向）")]
        public Vector3 visualLocalEuler = new Vector3(0f, 90f, 0f);
        [Tooltip("枪模型缩放，Kenney 模型通常 0.3～0.5")]
        [Min(0.01f)]
        public float visualScale = 0.4f;
    }

    [Header("输入")]
    public KeyCode weapon1Key = KeyCode.Alpha1;
    public KeyCode weapon2Key = KeyCode.Alpha2;

    [Header("空手")]
    [Tooltip("按键切换空手模式：隐藏枪模，关闭射击；再按一次恢复上一持枪状态")]
    public KeyCode unarmedToggleKey = KeyCode.Alpha3;

    [Header("动画（可选）")]
    [Tooltip("持枪时启用，空手时禁用；避免与 ThirdPersonController 抢 Speed 参数")]
    public PlayerAnimationBridge locomotionBridge;

    public bool enableMouseWheel = true;

    // 是否为空手（不显示武器、不射击），不影响移动层动画
    public bool IsUnarmed { get; private set; }

    // 当前武器在列表中的下标
    public int CurrentWeaponIndex { get; private set; }

    // 各武器弹匣内剩余子弹数，切枪时保存当前枪的余量
    int[] _roundsInMagazine;

    readonly List<GameObject> _weaponVisualInstances = new List<GameObject>();
    Transform _cachedHandSocket;

    // 取当前武器的 ScriptableObject，列表为空时返回 null
    public WeaponDataSO CurrentWeapon => weapons != null && weapons.Count > 0 && CurrentWeaponIndex >= 0 && CurrentWeaponIndex < weapons.Count
        ? weapons[CurrentWeaponIndex]
        : null;

    // 当前武器变化时触发，HUD 武器图标等可订阅
    public event Action OnActiveWeaponChanged;

    // 缓存 HitscanShooter，Awake 里找，早于 Start
    void Awake()
    {
        if (hitscanShooter == null)
            hitscanShooter = GetComponent<HitscanShooter>();
    }

    // 初始化弹匣数组，实例化武器模型，查找右手骨骼，下一帧应用武器数据（等 Animator 初始化完毕）
    void Start()
    {
        if (weapons == null)
            weapons = new List<WeaponDataSO>();
        _roundsInMagazine = new int[weapons.Count];
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null)
                _roundsInMagazine[i] = weapons[i].magazineSize;
        }

        EnsureHandOffsetArraySize();
        CurrentWeaponIndex = Mathf.Clamp(CurrentWeaponIndex, 0, Mathf.Max(0, weapons.Count - 1));
        // Start 里 Animator 已初始化，此时找右手骨骼成功率高
        EnsureHandSocket();
        RebuildWeaponVisualInstances();
        StartCoroutine(CoApplyWeaponNextFrame());
        NotifyActiveWeaponChanged();

        if (locomotionBridge == null)
            locomotionBridge = GetComponentInChildren<PlayerAnimationBridge>(true);
        // 与 PlayerAnimationBridge 对齐，确保骨骼/挂点指向同一 Animator
        if (locomotionBridge != null && locomotionBridge.characterAnimator != null &&
            (characterAnimator == null || characterAnimator.gameObject == gameObject))
            characterAnimator = locomotionBridge.characterAnimator;
        ApplyLocomotionBridgeForCurrentState();
    }

    // 确保 weaponHandOffsets 数组长度与 weapons 一致，不够时补默认值
    void EnsureHandOffsetArraySize()
    {
        int need = weapons != null ? weapons.Count : 0;
        if (weaponHandOffsets == null)
            weaponHandOffsets = new WeaponHandOffset[need];
        if (weaponHandOffsets.Length != need)
        {
            var next = new WeaponHandOffset[need];
            for (int i = 0; i < need; i++)
                next[i] = (i < weaponHandOffsets.Length && weaponHandOffsets[i] != null)
                    ? weaponHandOffsets[i]
                    : new WeaponHandOffset();
            weaponHandOffsets = next;
        }
        for (int i = 0; i < need; i++)
        {
            if (weaponHandOffsets[i] == null)
                weaponHandOffsets[i] = new WeaponHandOffset();
        }
    }

    // 取当前武器对应的偏移，越界时返回默认值
    WeaponHandOffset CurrentOffset =>
        (weaponHandOffsets != null && CurrentWeaponIndex >= 0 && CurrentWeaponIndex < weaponHandOffsets.Length)
            ? weaponHandOffsets[CurrentWeaponIndex]
            : new WeaponHandOffset();

    // LateUpdate：动画骨骼位置已在 Update 里确定，在 LateUpdate 里挂枪保证不闪烁
    void LateUpdate()
    {
        // 骨骼还没找到时每帧重试（Animator 延迟初始化场景）
        if (useRightHandBoneForVisuals && _cachedHandSocket == null)
            EnsureHandSocket();

        ApplyCurrentWeaponPose();
    }

    // 把当前武器的偏移数据写到 Socket 和可视实例，Inspector 运行时调参即时可见
    void ApplyCurrentWeaponPose()
    {
        WeaponHandOffset off = CurrentOffset;

        if (useRightHandBoneForVisuals && _cachedHandSocket != null)
        {
            _cachedHandSocket.localPosition = off.socketLocalPosition;
            _cachedHandSocket.localRotation = Quaternion.Euler(off.socketLocalEuler);
        }

        if (_weaponVisualInstances == null || _weaponVisualInstances.Count == 0)
            return;

        // 只刷当前激活的那一把，其余已隐藏
        int cur = CurrentWeaponIndex;
        if (cur >= 0 && cur < _weaponVisualInstances.Count && _weaponVisualInstances[cur] != null)
        {
            Transform t = _weaponVisualInstances[cur].transform;
            t.localRotation = Quaternion.Euler(off.visualLocalEuler);
            t.localScale = Vector3.one * off.visualScale;
        }
    }

    // 销毁时清理所有武器模型实例，防止场景切换时留下孤立 GameObject
    void OnDestroy()
    {
        ClearWeaponVisualInstances();
    }

    // 逐一 Destroy 并清空实例列表
    void ClearWeaponVisualInstances()
    {
        for (int i = 0; i < _weaponVisualInstances.Count; i++)
        {
            if (_weaponVisualInstances[i] != null)
                Destroy(_weaponVisualInstances[i]);
        }
        _weaponVisualInstances.Clear();
    }

    // 在右手骨骼下找到或创建 WeaponVisualSocket 挂点；Start / LateUpdate 均可调用，幂等
    void EnsureHandSocket()
    {
        if (!useRightHandBoneForVisuals)
            return;

        Transform hand = null;

        // 优先级 1：Inspector 直接拖入的骨骼（最可靠）
        if (rightHandBone != null)
        {
            hand = rightHandBone;
        }
        else
        {
            // 优先级 2：通过 Animator.GetBoneTransform 找，需要 Humanoid Rig
            Animator anim = characterAnimator != null ? characterAnimator : GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
                hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            // 优先级 3：按名称递归查找，Generic 骨架的 fallback
            if (hand == null)
                hand = FindChildRecursiveByName(transform, "Right_Hand");
        }

        if (hand == null)
        {
            Debug.LogWarning("[WeaponHolder] 未找到右手骨骼！请在 Inspector 的 Right Hand Bone 字段直接拖入 unitychan 层级里的 Right_Hand Transform。", this);
            return;
        }

        const string socketName = "WeaponVisualSocket";
        _cachedHandSocket = hand.Find(socketName);
        if (_cachedHandSocket == null)
        {
            var go = new GameObject(socketName);
            _cachedHandSocket = go.transform;
            _cachedHandSocket.SetParent(hand, false);
        }

        WeaponHandOffset off = CurrentOffset;
        _cachedHandSocket.localPosition = off.socketLocalPosition;
        _cachedHandSocket.localRotation = Quaternion.Euler(off.socketLocalEuler);
    }

    // 递归按名称在子树里查找，名称完全匹配才返回
    static Transform FindChildRecursiveByName(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindChildRecursiveByName(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    // 输出 Transform 层级路径，调试挂点路径时用
    static string GetTransformPath(Transform t)
    {
        if (t == null) return "null";
        string path = t.name;
        Transform p = t.parent;
        int depth = 0;
        while (p != null && depth < 6)
        {
            path = p.name + "/" + path;
            p = p.parent;
            depth++;
        }
        return path;
    }

    // 取挂载点：右手骨骼 > weaponAttachPoint > firePoint，按优先级降序
    Transform ResolveVisualAttachTransform()
    {
        if (useRightHandBoneForVisuals && _cachedHandSocket != null)
            return _cachedHandSocket;
        if (weaponAttachPoint != null)
            return weaponAttachPoint;
        if (hitscanShooter != null && hitscanShooter.firePoint != null)
            return hitscanShooter.firePoint;
        return null;
    }

    // 销毁旧实例，按 weaponVisualPrefabs 逐一生成，激活状态由 RefreshWeaponVisuals 控制
    void RebuildWeaponVisualInstances()
    {
        ClearWeaponVisualInstances();
        if (weaponVisualPrefabs == null || weaponVisualPrefabs.Length == 0)
            return;

        EnsureHandSocket();
        Transform attach = ResolveVisualAttachTransform();
        if (attach == null)
        {
            Debug.LogError("[WeaponHolder] 没有找到任何可用挂载点，枪模型无法生成！", this);
            return;
        }

        int count = Mathf.Min(weapons.Count, weaponVisualPrefabs.Length);
        for (int i = 0; i < count; i++)
        {
            if (weaponVisualPrefabs[i] == null)
            {
                _weaponVisualInstances.Add(null);
                continue;
            }

            GameObject inst = Instantiate(weaponVisualPrefabs[i], attach);
            inst.transform.localPosition = Vector3.zero;
            WeaponHandOffset off = (weaponHandOffsets != null && i < weaponHandOffsets.Length && weaponHandOffsets[i] != null)
                ? weaponHandOffsets[i] : new WeaponHandOffset();
            inst.transform.localRotation = Quaternion.Euler(off.visualLocalEuler);
            inst.transform.localScale = Vector3.one * off.visualScale;
            inst.name = "WeaponVisual_" + i;
            _weaponVisualInstances.Add(inst);
        }

        RefreshWeaponVisuals();
    }

    // 根据当前索引和空手状态显隐各武器模型
    void RefreshWeaponVisuals()
    {
        for (int i = 0; i < _weaponVisualInstances.Count; i++)
        {
            if (_weaponVisualInstances[i] == null) continue;
            bool show = !IsUnarmed && i == CurrentWeaponIndex;
            _weaponVisualInstances[i].SetActive(show);
        }
    }

    // 等下一帧再应用武器数据，保证 Animator 和骨骼已初始化完毕
    IEnumerator CoApplyWeaponNextFrame()
    {
        yield return null;
        ApplyCurrentWeaponToShooter();
        RefreshWeaponVisuals();
    }

    // 每帧处理数字键和滚轮切枪，以及空手切换
    void Update()
    {
        if (weapons == null || weapons.Count == 0) return;
        if (hitscanShooter == null) return;
        if (!GameStateManager.IsGameplayPlaying) return;

        if (Input.GetKeyDown(unarmedToggleKey))
        {
            if (IsUnarmed) ExitUnarmed();
            else EnterUnarmed();
        }

        if (Input.GetKeyDown(weapon1Key)) TrySwitchWeapon(0);
        if (Input.GetKeyDown(weapon2Key)) TrySwitchWeapon(1);

        if (enableMouseWheel && weapons.Count > 1)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0.01f)
                TrySwitchWeapon((CurrentWeaponIndex + 1) % weapons.Count);
            else if (scroll < -0.01f)
                TrySwitchWeapon((CurrentWeaponIndex - 1 + weapons.Count) % weapons.Count);
        }
    }

    // 进入空手：保留当前武器索引和弹匣数据，仅隐藏模型并禁止射击
    public void EnterUnarmed()
    {
        if (IsUnarmed || weapons == null || weapons.Count == 0) return;
        SaveMagazineFromShooter();
        IsUnarmed = true;
        RefreshWeaponVisuals();
        NotifyActiveWeaponChanged();
        ApplyLocomotionBridgeForCurrentState();
    }

    // 退出空手：恢复当前索引对应武器和射击能力
    public void ExitUnarmed()
    {
        if (!IsUnarmed) return;
        IsUnarmed = false;
        ApplyCurrentWeaponToShooter();
        RefreshWeaponVisuals();
        ApplyCurrentWeaponPose();
        NotifyActiveWeaponChanged();
        ApplyLocomotionBridgeForCurrentState();
    }

    // 持枪时启用 PlayerAnimationBridge（防止 Speed 参数被覆盖），空手时禁用
    void ApplyLocomotionBridgeForCurrentState()
    {
        if (locomotionBridge == null) return;
        locomotionBridge.enabled = !IsUnarmed;
    }

    // 切换前把当前弹匣余量写回数组，再装备新武器；同索引且不空手时直接 return
    public void TrySwitchWeapon(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Count || weapons[index] == null) return;
        if (index == CurrentWeaponIndex && !IsUnarmed) return;

        IsUnarmed = false;
        SaveMagazineFromShooter();
        CurrentWeaponIndex = index;
        ApplyCurrentWeaponToShooter();
        RefreshWeaponVisuals();
        ApplyCurrentWeaponPose();
        NotifyActiveWeaponChanged();
        ApplyLocomotionBridgeForCurrentState();
    }

    // 发出武器变化事件，HUD 图标等订阅方会刷新
    void NotifyActiveWeaponChanged()
    {
        OnActiveWeaponChanged?.Invoke();
    }

    // 切枪前把当前弹匣余量存入数组，防止切回时弹匣重置为满弹
    void SaveMagazineFromShooter()
    {
        if (_roundsInMagazine == null || hitscanShooter == null) return;
        if (CurrentWeaponIndex >= 0 && CurrentWeaponIndex < _roundsInMagazine.Length)
            _roundsInMagazine[CurrentWeaponIndex] = hitscanShooter.currentAmmo;
    }

    // 把当前武器数据和弹匣余量同步给 HitscanShooter
    void ApplyCurrentWeaponToShooter()
    {
        WeaponDataSO w = CurrentWeapon;
        if (w == null || hitscanShooter == null) return;
        int mag = CurrentWeaponIndex < _roundsInMagazine.Length ? _roundsInMagazine[CurrentWeaponIndex] : w.magazineSize;
        hitscanShooter.ApplyWeaponData(w, mag);
    }

    // 供换弹等逻辑在扣备弹后刷新弹匣显示
    public void NotifyWeaponStateChanged()
    {
        ApplyCurrentWeaponToShooter();
    }

    // 存档：先同步当前弹匣到数组，再导出快照
    public int[] GetMagazineRoundsSnapshot()
    {
        SaveMagazineFromShooter();
        if (_roundsInMagazine == null || weapons == null) return null;
        var copy = new int[weapons.Count];
        int n = Mathf.Min(copy.Length, _roundsInMagazine.Length);
        for (int i = 0; i < n; i++) copy[i] = _roundsInMagazine[i];
        for (int i = n; i < copy.Length; i++) copy[i] = weapons[i] != null ? weapons[i].magazineSize : 0;
        return copy;
    }

    // 读档：恢复武器索引和各弹匣余量，然后刷新视觉和射击器数据
    public void RestoreFromSave(int activeWeaponIndex, int[] magazineRounds)
    {
        if (weapons == null || weapons.Count == 0) return;

        if (_roundsInMagazine == null || _roundsInMagazine.Length != weapons.Count)
        {
            _roundsInMagazine = new int[weapons.Count];
            for (int i = 0; i < weapons.Count; i++)
                _roundsInMagazine[i] = weapons[i] != null ? weapons[i].magazineSize : 0;
        }

        if (magazineRounds != null)
        {
            int n = Mathf.Min(magazineRounds.Length, _roundsInMagazine.Length);
            for (int i = 0; i < n; i++)
            {
                int cap = weapons[i] != null ? weapons[i].magazineSize : 0;
                _roundsInMagazine[i] = Mathf.Clamp(magazineRounds[i], 0, cap > 0 ? cap : 999);
            }
        }

        IsUnarmed = false;
        CurrentWeaponIndex = Mathf.Clamp(activeWeaponIndex, 0, weapons.Count - 1);
        ApplyCurrentWeaponToShooter();
        RefreshWeaponVisuals();
        ApplyCurrentWeaponPose();
        NotifyActiveWeaponChanged();
        ApplyLocomotionBridgeForCurrentState();
    }
}
