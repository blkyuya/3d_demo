using System;
using UnityEngine;

// 网格背包单格物品种类（与 AmmoType 对应关系见 PlayerInventory）
public enum InventoryGridItemKind
{
    Empty      = 0, // 空槽
    MedKit     = 1, // 医疗包（可堆叠）
    Key        = 2, // 钥匙
    AmmoPistol = 3, // 手枪备弹
    AmmoShotgun= 4, // 霰弹枪备弹
    ClueNote   = 5  // 线索纸条（不同密码内容不堆叠，内容存在 customPayload）
}

// 网格背包单格数据（可序列化，供 Inspector 配置和存档读写）
[Serializable]
public class InventoryGridCell
{
    public InventoryGridItemKind kind = InventoryGridItemKind.Empty;

    // 数量：弹药/医疗包可大于 1；钥匙通常为 1
    public int count;

    // 自定义数据，纸条存密码等文本，其他种类留空
    public string customPayload = "";

    public bool IsEmpty => kind == InventoryGridItemKind.Empty || count <= 0;
}
