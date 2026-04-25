using UnityEngine;
using System;

// Hitscan 射击器：从屏幕中心发射线，命中即判伤，支持手枪单发和霰弹多弹丸散射。
// 武器参数由 WeaponDataSO 提供，切换武器时 WeaponHolder 调 ApplyWeaponData 同步，射击逻辑本身不动。
// 弹匣存在这里，备弹存在 PlayerInventory；换弹时从备弹里取，取满为止。
// 挂载：PlayerArmature，与 WeaponHolder、PlayerInventory 同节点。
public class HitscanShooter : MonoBehaviour
{
    [Header("引用")]
    public Camera mainCamera;
    public Transform firePoint;
    public PlayerHealth playerHealth;
    public PlayerInventory playerInventory;

    [Header("武器持有器（多武器时赋值）")]
    [Tooltip("有多把枪时拖 WeaponHolder；单枪用 Default Weapon Data 即可，两者并存时 Holder 优先")]
    public WeaponHolder weaponHolder;

    [Tooltip("没有 WeaponHolder 时的单武器数据")]
    public WeaponDataSO defaultWeaponData;

    [Header("UI（可选）")]
    [Tooltip("背包开着时屏蔽射击和换弹，防止和背包右键用药包冲突")]
    public PlayerInventoryBagUI inventoryBagUI;

    [Header("没有武器数据时的 fallback 参数")]
    public float shootDistance = 100f;
    public int damage = 1;

    [Header("弹匣参数（无 WeaponData 时 fallback）")]
    public int magazineSize = 8;
    public int currentAmmo = 8;
    public float reloadTime = 1.2f;

    // 换弹中；PlayerHealAction 等会读这个来做互斥
    public bool IsReloading { get; private set; }

    UnityEngine.Coroutine _reloadCoroutine;

    // 弹匣或备弹变化时触发（当前弹匣数, 备弹数），AmmoUI 订阅这个刷新显示
    public event Action<int, int> OnAmmoChanged;

    // 射出去一发时触发（弹匣已扣减），枪械动画层订阅
    public event Action OnShotFired;

    // 换弹协程刚开始时触发一次，动画层订阅
    public event Action OnReloadStarted;

    // WeaponHolder 的当前武器 > defaultWeaponData，都没有就返回 null
    public WeaponDataSO ActiveWeaponData
    {
        get
        {
            if (weaponHolder != null && weaponHolder.CurrentWeapon != null)
                return weaponHolder.CurrentWeapon;
            return defaultWeaponData;
        }
    }

    // 补引用，初始化弹匣数量
    void Start()
    {
        if (inventoryBagUI == null && UIManager.Instance != null)
            inventoryBagUI = UIManager.Instance.inventoryBagUI;
        if (inventoryBagUI == null)
            inventoryBagUI = FindObjectOfType<PlayerInventoryBagUI>();

        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();

        if (weaponHolder == null)
        {
            if (defaultWeaponData != null)
                ApplyWeaponData(defaultWeaponData, defaultWeaponData.magazineSize);
            else
            {
                currentAmmo = magazineSize;
                NotifyAmmoChanged();
            }
        }
    }

    // 每帧检测 R 键换弹和鼠标左键射击，各种屏蔽条件在这里统一 return
    void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
            return;
        if (!GameStateManager.IsGameplayPlaying)
            return;
        if (inventoryBagUI != null && inventoryBagUI.IsBagOpen)
            return;
        // 密码键盘输入期间不射击，防止点数字键触发 Raycast
        if (PasswordDoorSession.IsActive)
            return;
        // 空手模式不射击不换弹
        if (weaponHolder != null && weaponHolder.IsUnarmed)
            return;
        if (IsReloading)
            return;

        if (Input.GetKeyDown(KeyCode.R))
            TryReload();

        HandleShootInput();
    }

    // WeaponHolder 切枪时调这里，把 SO 数据和当前弹匣余量同步到本脚本
    public void ApplyWeaponData(WeaponDataSO data, int roundsInMagazine)
    {
        if (data == null)
            return;
        shootDistance = data.shootDistance;
        damage = data.damage;
        magazineSize = data.magazineSize;
        reloadTime = data.reloadTime;
        currentAmmo = Mathf.Clamp(roundsInMagazine, 0, magazineSize);
        NotifyAmmoChanged();
    }

    // 监听鼠标左键；弹匣空了发空枪事件给音效，不产生射线
    void HandleShootInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (currentAmmo <= 0)
            {
                EventCenter.Publish(DryFireEvent.Default);
                return;
            }
            Shoot();
        }
    }

    // 从屏幕中心发射线，霰弹时发多条并随机散布
    void Shoot()
    {
        if (mainCamera == null)
        {
            Debug.LogWarning("HitscanShooter: Main Camera 未赋值。");
            return;
        }

        WeaponDataSO w = ActiveWeaponData;
        int pelletCount = w != null ? Mathf.Max(1, w.pelletCount) : 1;
        float spread = w != null ? w.spreadHalfAngleDegrees : 0f;
        int dmg = w != null ? w.damage : damage;
        float maxDist = w != null ? w.shootDistance : shootDistance;

        currentAmmo--;
        NotifyAmmoChanged();
        OnShotFired?.Invoke();

        bool isShotgun = w != null && w.pelletCount > 1;
        EventCenter.Publish(isShotgun ? ShotFiredEvent.Shotgun : ShotFiredEvent.Pistol);

        // 从 viewport(0.5, 0.5) 发出主射线，保证准星对准射击方向
        Ray baseRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 origin = baseRay.origin;
        Vector3 forward = baseRay.direction.normalized;

        // 算出垂直于 forward 的两个基向量，用于霰弹随机偏转
        Vector3 right = Vector3.Cross(forward, Vector3.up);
        if (right.sqrMagnitude < 1e-4f)
            right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(right, forward).normalized;

        float spreadRad = spread * Mathf.Deg2Rad;

        for (int p = 0; p < pelletCount; p++)
        {
            Vector3 dir = forward;
            if (pelletCount > 1 && spreadRad > 1e-4f)
            {
                float rx = UnityEngine.Random.Range(-spreadRad, spreadRad);
                float ry = UnityEngine.Random.Range(-spreadRad, spreadRad);
                dir = (forward + right * Mathf.Tan(rx) * 0.1f + up * Mathf.Tan(ry) * 0.1f).normalized;
            }

            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // 碰撞体可能挂在子骨骼上，往父级找一次
                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                if (damageable == null)
                    damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(dmg);
            }
        }
    }

    // 检查各种换弹前置条件，都过了才起协程
    void TryReload()
    {
        if (playerInventory == null)
        {
            Debug.LogWarning("HitscanShooter: PlayerInventory 未赋值，无法换弹。");
            return;
        }
        if (currentAmmo >= magazineSize)
            return;

        WeaponDataSO w = ActiveWeaponData;
        AmmoType ammoType = w != null ? w.ammoType : AmmoType.Pistol9mm;

        if (playerInventory.GetReserveAmmo(ammoType) <= 0)
            return;

        if (_reloadCoroutine != null)
            StopCoroutine(_reloadCoroutine);
        _reloadCoroutine = StartCoroutine(ReloadCoroutine());
    }

    // 读档时如果换弹协程还在跑就强制停掉，防止覆盖已恢复的弹匣数据
    public void ForceCancelReloadForSaveLoad()
    {
        if (_reloadCoroutine != null)
        {
            StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = null;
        }
        IsReloading = false;
    }

    // 等换弹时间，从备弹里取子弹补满弹匣
    System.Collections.IEnumerator ReloadCoroutine()
    {
        IsReloading = true;
        OnReloadStarted?.Invoke();
        EventCenter.Publish(ReloadStartedEvent.Default);

        WeaponDataSO w = ActiveWeaponData;
        float rt = w != null ? w.reloadTime : reloadTime;
        AmmoType ammoType = w != null ? w.ammoType : AmmoType.Pistol9mm;

        yield return new WaitForSeconds(rt);

        int ammoNeeded = magazineSize - currentAmmo;
        int loadedAmmo = playerInventory.ConsumeReserveAmmo(ammoType, ammoNeeded);
        currentAmmo += loadedAmmo;
        currentAmmo = Mathf.Min(currentAmmo, magazineSize);

        IsReloading = false;
        _reloadCoroutine = null;
        NotifyAmmoChanged();
    }

    // 算出当前备弹数，一起发给 UI
    void NotifyAmmoChanged()
    {
        int reserve = 0;
        if (playerInventory != null)
        {
            WeaponDataSO w = ActiveWeaponData;
            AmmoType t = w != null ? w.ammoType : AmmoType.Pistol9mm;
            reserve = playerInventory.GetReserveAmmo(t);
        }
        OnAmmoChanged?.Invoke(currentAmmo, reserve);
    }
}
