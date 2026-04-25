using UnityEngine;

// 存档应用器：场景加载后把存档数据还原到玩家（由 SaveLoadBootstrap 在场景就绪后调用一次）。
// 静态工具类，无需挂载；和 SaveGameCollector 一起构成读写对。
public static class SaveLoadSceneApplier
{
    // 把存档数据里的位置、血量、背包、武器状态全部写回场景对象
    public static void Apply(GameSaveData d)
    {
        if (d == null) return;

        PlayerHealth ph = Object.FindObjectOfType<PlayerHealth>();
        if (ph == null)
        {
            Debug.LogWarning("SaveLoadSceneApplier: 未找到 PlayerHealth。");
            return;
        }

        // 移动 CharacterController 时必须先禁用它，否则 SetPosition 无效
        Transform tr = ph.transform;
        CharacterController cc = tr.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        tr.SetPositionAndRotation(
            new Vector3(d.playerPosX, d.playerPosY, d.playerPosZ),
            Quaternion.Euler(0f, d.playerRotY, 0f));

        if (cc != null) cc.enabled = true;

        // 直接覆盖血量，不触发死亡事件，避免读档时触发 GameOver
        ph.ApplyLoadedHealthState(d.currentHealth, d.maxHealth, d.currentHealth <= 0);

        PlayerInventory inv = UIManager.Instance != null
            ? UIManager.Instance.playerInventory
            : Object.FindObjectOfType<PlayerInventory>();
        if (inv != null && d.inventoryCells != null)
            inv.ImportCellsFromSave(d.inventoryCells);

        // 恢复武器索引和弹匣余量
        WeaponHolder wh = Object.FindObjectOfType<WeaponHolder>();
        if (wh != null && d.magazineRoundsPerWeapon != null)
            wh.RestoreFromSave(d.currentWeaponIndex, d.magazineRoundsPerWeapon);

        // 读档时若换弹协程还在跑（存档时刚好在换弹），强制取消，防止覆盖刚恢复的弹匣数据
        HitscanShooter hs = Object.FindObjectOfType<HitscanShooter>();
        if (hs != null)
            hs.ForceCancelReloadForSaveLoad();

        // 清空事件中心，防止旧场景的 delegate 在新状态下意外触发
        EventCenter.ClearAll();
    }
}
