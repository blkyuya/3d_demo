using UnityEngine;
using TMPro;

// 弹药 HUD：显示当前弹匣剩余 + 对应武器类型的备弹，订阅 HitscanShooter 和 PlayerInventory 事件实时刷新。
// 挂载：HUD Canvas 上的弹药文字节点。
public class AmmoUI : BasePanel
{
    [Header("引用")]
    public HitscanShooter hitscanShooter;
    public PlayerInventory playerInventory;

    [Tooltip("有则按当前武器弹药类型显示备弹，留空则默认手枪")]
    public WeaponHolder weaponHolder;

    public TextMeshProUGUI ammoText;

    // 初始化：订阅弹药变化事件，并做一次初始刷新
    protected override void OnPanelInit()
    {
        if (ammoText != null)
            panelRoot = ammoText.gameObject;

        if (hitscanShooter != null)
            hitscanShooter.OnAmmoChanged += OnAmmoChangedFromShooter;

        if (playerInventory != null)
            playerInventory.OnAmmoInventoryChanged += RefreshAmmoText;

        // 如果没指定 weaponHolder，就从 hitscanShooter 同级找
        if (weaponHolder == null && hitscanShooter != null)
            weaponHolder = hitscanShooter.GetComponent<WeaponHolder>();

        RefreshAmmoText();
    }

    // 取消订阅
    void OnDestroy()
    {
        if (hitscanShooter != null)
            hitscanShooter.OnAmmoChanged -= OnAmmoChangedFromShooter;
        if (playerInventory != null)
            playerInventory.OnAmmoInventoryChanged -= RefreshAmmoText;
    }

    // 弹匣数量变化时由 HitscanShooter 事件触发
    void OnAmmoChangedFromShooter(int currentAmmo, int reserveAmmo)
    {
        UpdateAmmoText(currentAmmo, reserveAmmo);
    }

    // 备弹变化时（拾取弹药等）重新读当前弹匣 + 备弹刷新
    void RefreshAmmoText()
    {
        int cur = hitscanShooter != null ? hitscanShooter.currentAmmo : 0;
        int res = GetDisplayedReserve();
        UpdateAmmoText(cur, res);
    }

    // 根据当前武器弹药类型从背包读备弹数量
    int GetDisplayedReserve()
    {
        if (playerInventory == null) return 0;

        AmmoType t = AmmoType.Pistol9mm;
        if (weaponHolder != null && weaponHolder.CurrentWeapon != null)
            t = weaponHolder.CurrentWeapon.ammoType;
        else if (hitscanShooter != null && hitscanShooter.ActiveWeaponData != null)
            t = hitscanShooter.ActiveWeaponData.ammoType;

        return playerInventory.GetReserveAmmo(t);
    }

    // 格式化显示 "弹匣 / 备弹"
    void UpdateAmmoText(int currentAmmo, int reserveAmmo)
    {
        if (ammoText != null)
            ammoText.text = currentAmmo + " / " + reserveAmmo;
    }
}
