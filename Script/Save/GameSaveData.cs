using System;
using UnityEngine;

// 单槽完整存档数据，JsonUtility 可序列化。
// 扩展关卡状态时在这里加字段，同时把 version 加一，便于读档时做版本兼容判断。
[Serializable]
public class GameSaveData
{
    public int version = 2;

    // 写入时的 UTC 时间戳（Ticks），SaveLoadPanelUI 用它显示存档时间
    public long savedUtcTicks;

    public int sceneBuildIndex;

    public float playerPosX;
    public float playerPosY;
    public float playerPosZ;

    // 只存 Y 轴旋转（度），读档时 Euler(0, playerRotY, 0) 还原
    public float playerRotY;

    public int currentHealth;
    public int maxHealth;

    // 16 格背包，按 GridSlotCount 顺序序列化
    public InventoryCellSave[] inventoryCells;

    public int currentWeaponIndex;

    // 与 WeaponHolder.weapons 列表长度一致，各武器弹匣当前余量
    public int[] magazineRoundsPerWeapon;
}

// 单格背包序列化结构，kind 对应 InventoryGridItemKind 枚举整数值
[Serializable]
public class InventoryCellSave
{
    public int kind;
    public int count;

    // 纸条等自定义内容；旧存档字段为空时读档不影响其他格子
    public string customPayload;
}
