using UnityEngine;
using UnityEngine.SceneManagement;

// 存档数据采集器：从当前运行场景收集玩家位置、血量、背包、武器弹匣，打包成 GameSaveData。
// 静态工具类，无需挂载；SaveLoadPanelUI 触发「保存」时调用 BuildFromCurrentScene。
public static class SaveGameCollector
{
    // 遍历场景采集所有需要保存的玩家状态，找不到 PlayerHealth 直接返回空数据
    public static GameSaveData BuildFromCurrentScene()
    {
        var data = new GameSaveData();
        data.version = 2;
        data.sceneBuildIndex = SceneManager.GetActiveScene().buildIndex;

        PlayerHealth ph = Object.FindObjectOfType<PlayerHealth>();
        if (ph == null)
        {
            Debug.LogWarning("SaveGameCollector: 未找到 PlayerHealth。");
            return data;
        }

        // 保存玩家位置和水平朝向（只存 Y 轴旋转，读档时直接 Euler 还原）
        Transform t = ph.transform;
        data.playerPosX = t.position.x;
        data.playerPosY = t.position.y;
        data.playerPosZ = t.position.z;
        data.playerRotY = t.eulerAngles.y;
        data.currentHealth = ph.CurrentHealth;
        data.maxHealth = ph.MaxHealth;

        // 优先从 UIManager 拿背包引用，避免 FindObjectOfType 在大场景里性能差
        PlayerInventory inv = UIManager.Instance != null
            ? UIManager.Instance.playerInventory
            : Object.FindObjectOfType<PlayerInventory>();
        if (inv != null)
            data.inventoryCells = inv.ExportCellsForSave();

        // 保存当前武器索引和各武器弹匣余量，读档后还原时不会变成满弹
        WeaponHolder wh = Object.FindObjectOfType<WeaponHolder>();
        if (wh != null)
        {
            data.currentWeaponIndex = wh.CurrentWeaponIndex;
            data.magazineRoundsPerWeapon = wh.GetMagazineRoundsSnapshot();
        }

        return data;
    }
}
