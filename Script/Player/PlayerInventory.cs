using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 玩家背包：16 格网格（任意交换），钥匙/医疗包/备弹/纸条均存于格内。
// 对外仍提供备弹查询 API（GetReserveAmmo/ConsumeReserveAmmo），兼容 HitscanShooter 的换弹逻辑。
// 弹药格合并堆叠而非每颗一格；拖放交换通过 SwapGridSlots 实现，UI 层调用。
// 挂载：PlayerArmature 根节点，与 PlayerHealth、HitscanShooter 同层。
public class PlayerInventory : MonoBehaviour
{
    public const int GridSlotCount = 16;

    [Header("网格背包")]
    [SerializeField]
    InventoryGridCell[] gridCells = new InventoryGridCell[GridSlotCount];

    // 旧版迁移标记，迁移完成后不再执行，防止重复叠加弹药
    [SerializeField, HideInInspector]
    bool _legacyMigratedToGrid;

    [SerializeField, HideInInspector, FormerlySerializedAs("medKitCount")]
    int _migrationMedKits;

    [SerializeField, HideInInspector, FormerlySerializedAs("hasKey")]
    bool _migrationHasKey;

    [Serializable]
    public class AmmoEntry
    {
        public AmmoType ammoType;
        public int count;
    }

    [SerializeField, HideInInspector, FormerlySerializedAs("ammoByType")]
    List<AmmoEntry> ammoByType = new List<AmmoEntry>();

    [SerializeField, HideInInspector, FormerlySerializedAs("reserveAmmo")]
    int _legacyReserveAmmo = 24;

    // 备弹数量变化时触发，AmmoUI 订阅；弹匣变化由 HitscanShooter.OnAmmoChanged 负责
    public event Action OnAmmoInventoryChanged;
    public event Action<int> OnMedKitChanged;

    // 任意格子内容变化时触发，背包格子 UI 统一刷新
    public event Action OnInventoryDisplayChanged;

    // 是否持有钥匙（遍历格子计数）
    public bool hasKey => CountKind(InventoryGridItemKind.Key) > 0;

    // 医疗包总数（遍历格子求和）
    public int medKitCount => GetMedKitTotal();

    // 确保数组已初始化，Awake 比 Start 早，防止其他脚本在 Start 里调 API 时数组还是 null
    void Awake()
    {
        EnsureGridArray();
    }

    // 处理旧版数据迁移，触发初始 UI 刷新事件
    void Start()
    {
        if (!_legacyMigratedToGrid)
            MigrateLegacyAmmoIfNeeded();
        MigrateLegacyInventoryToGridIfNeeded();
        OnAmmoInventoryChanged?.Invoke();
        OnMedKitChanged?.Invoke(GetMedKitTotal());
        RaiseInventoryDisplayChanged();
    }

    // 触发背包格子刷新，所有格子数据变化都调这里统一通知 UI
    void RaiseInventoryDisplayChanged()
    {
        OnInventoryDisplayChanged?.Invoke();
    }

    // 检查并初始化格子数组，防止 Inspector 序列化异常导致格子是 null
    void EnsureGridArray()
    {
        if (gridCells == null || gridCells.Length != GridSlotCount)
        {
            gridCells = new InventoryGridCell[GridSlotCount];
            for (int i = 0; i < GridSlotCount; i++)
                gridCells[i] = new InventoryGridCell();
            return;
        }

        for (int i = 0; i < gridCells.Length; i++)
        {
            if (gridCells[i] == null)
                gridCells[i] = new InventoryGridCell();
        }
    }

    // 判断格子是否视觉上全空（用于决定是否需要执行旧版数据迁移）
    bool IsGridVisuallyEmpty()
    {
        EnsureGridArray();
        for (int i = 0; i < GridSlotCount; i++)
        {
            if (gridCells[i] != null && !gridCells[i].IsEmpty)
                return false;
        }
        return true;
    }

    // 旧版弹药字段为空时初始化默认手枪弹，防止第一次启动弹药为 0
    void MigrateLegacyAmmoIfNeeded()
    {
        if (ammoByType == null)
            ammoByType = new List<AmmoEntry>();

        if (ammoByType.Count == 0)
        {
            ammoByType.Add(new AmmoEntry
            {
                ammoType = AmmoType.Pistol9mm,
                count = Mathf.Max(0, _legacyReserveAmmo)
            });
        }
    }

    // 将旧版 medKit / key / ammoByType 一次性写入网格格子，写完清空源字段
    void MigrateLegacyInventoryToGridIfNeeded()
    {
        EnsureGridArray();
        if (_legacyMigratedToGrid)
            return;

        // 格子已有数据说明已经用过新系统，不覆盖
        if (!IsGridVisuallyEmpty())
        {
            _legacyMigratedToGrid = true;
            ammoByType?.Clear();
            _migrationMedKits = 0;
            _migrationHasKey = false;
            return;
        }

        int pistol = 0;
        int shell = 0;
        if (ammoByType != null)
        {
            foreach (AmmoEntry e in ammoByType)
            {
                if (e == null) continue;
                if (e.ammoType == AmmoType.Pistol9mm)
                    pistol = Mathf.Max(0, e.count);
                else if (e.ammoType == AmmoType.ShotgunShell)
                    shell = Mathf.Max(0, e.count);
            }
        }

        TryStackOrPlace(InventoryGridItemKind.MedKit, _migrationMedKits);
        if (_migrationHasKey)
            TryStackOrPlace(InventoryGridItemKind.Key, 1);
        if (pistol > 0)
            TryStackOrPlace(InventoryGridItemKind.AmmoPistol, pistol);
        if (shell > 0)
            TryStackOrPlace(InventoryGridItemKind.AmmoShotgun, shell);

        _legacyMigratedToGrid = true;
        _migrationMedKits = 0;
        _migrationHasKey = false;
        ammoByType?.Clear();
    }

    // AmmoType 转对应的 InventoryGridItemKind
    static InventoryGridItemKind KindFromAmmoType(AmmoType type)
    {
        if (type == AmmoType.ShotgunShell)
            return InventoryGridItemKind.AmmoShotgun;
        return InventoryGridItemKind.AmmoPistol;
    }

    // InventoryGridItemKind 转 AmmoType，不是弹药格则返回 null
    static AmmoType? AmmoTypeFromKind(InventoryGridItemKind kind)
    {
        switch (kind)
        {
            case InventoryGridItemKind.AmmoPistol:  return AmmoType.Pistol9mm;
            case InventoryGridItemKind.AmmoShotgun: return AmmoType.ShotgunShell;
            default:                                return null;
        }
    }

    // 遍历全部格子，统计指定 kind 的总数量
    int CountKind(InventoryGridItemKind kind)
    {
        EnsureGridArray();
        int sum = 0;
        for (int i = 0; i < GridSlotCount; i++)
        {
            if (gridCells[i] != null && gridCells[i].kind == kind)
                sum += Mathf.Max(0, gridCells[i].count);
        }
        return sum;
    }

    // 读医疗包总数
    public int GetMedKitTotal()
    {
        return CountKind(InventoryGridItemKind.MedKit);
    }

    // 读指定类型的备弹总数，换弹前 HitscanShooter 调这里检查是否有子弹
    public int GetReserveAmmo(AmmoType type)
    {
        return CountKind(KindFromAmmoType(type));
    }

    // 将某类型总量强制设为 value（兼容旧调用：清空该类型所有格再写入第一格）
    void SetReserveAmmoInternal(AmmoType type, int value)
    {
        EnsureGridArray();
        value = Mathf.Max(0, value);
        var k = KindFromAmmoType(type);
        for (int i = 0; i < GridSlotCount; i++)
        {
            if (gridCells[i] != null && gridCells[i].kind == k)
            {
                gridCells[i].kind = InventoryGridItemKind.Empty;
                gridCells[i].count = 0;
                gridCells[i].customPayload = "";
            }
        }
        if (value > 0)
            TryStackOrPlace(k, value);
    }

    // 先找同类型格合并堆叠，没有则找第一个空格放入；背包全满时 Warning 提示
    void TryStackOrPlace(InventoryGridItemKind kind, int amount)
    {
        if (amount <= 0 || kind == InventoryGridItemKind.Empty)
            return;
        EnsureGridArray();

        // 同类型格合并
        for (int i = 0; i < GridSlotCount; i++)
        {
            if (gridCells[i] != null && gridCells[i].kind == kind)
            {
                gridCells[i].count += amount;
                return;
            }
        }

        // 找空格
        for (int i = 0; i < GridSlotCount; i++)
        {
            if (gridCells[i] != null && gridCells[i].IsEmpty)
            {
                gridCells[i].kind = kind;
                gridCells[i].count = amount;
                if (kind != InventoryGridItemKind.ClueNote)
                    gridCells[i].customPayload = "";
                return;
            }
        }

        Debug.LogWarning("PlayerInventory: 网格已满，无法放入 " + kind);
    }

    // 拾取弹药：合并到同类型格或第一个空槽，触发 UI 刷新
    public void AddAmmo(AmmoType type, int amount)
    {
        if (amount <= 0) return;
        TryStackOrPlace(KindFromAmmoType(type), amount);
        OnAmmoInventoryChanged?.Invoke();
        RaiseInventoryDisplayChanged();
    }

    // 兼容旧版 PickupItem，没指定类型时当手枪弹处理
    public void AddAmmo(int amount)
    {
        AddAmmo(AmmoType.Pistol9mm, amount);
    }

    // 换弹时从备弹中扣除，返回实际扣除数量（按格子顺序扣，优先扣靠前的格）
    public int ConsumeReserveAmmo(AmmoType type, int amount)
    {
        if (amount <= 0) return 0;
        EnsureGridArray();
        var k = KindFromAmmoType(type);
        int remaining = amount;
        int used = 0;
        for (int i = 0; i < GridSlotCount && remaining > 0; i++)
        {
            if (gridCells[i] == null || gridCells[i].kind != k)
                continue;
            int take = Mathf.Min(remaining, gridCells[i].count);
            gridCells[i].count -= take;
            used += take;
            remaining -= take;
            if (gridCells[i].count <= 0)
            {
                gridCells[i].kind = InventoryGridItemKind.Empty;
                gridCells[i].count = 0;
                gridCells[i].customPayload = "";
            }
        }
        OnAmmoInventoryChanged?.Invoke();
        RaiseInventoryDisplayChanged();
        return used;
    }

    // 拾取钥匙，占用一个格子
    public void AddKey()
    {
        TryStackOrPlace(InventoryGridItemKind.Key, 1);
        RaiseInventoryDisplayChanged();
    }

    // 拾取医疗包，触发 OnMedKitChanged 通知 UI
    public void AddMedKit(int amount)
    {
        if (amount <= 0) return;
        TryStackOrPlace(InventoryGridItemKind.MedKit, amount);
        OnMedKitChanged?.Invoke(GetMedKitTotal());
        RaiseInventoryDisplayChanged();
    }

    // 拾取线索纸条，占用一格，payload 通常是四位密码或提示文本
    public bool TryAddClueNote(string payload)
    {
        EnsureGridArray();
        string p = payload ?? "";
        for (int i = 0; i < GridSlotCount; i++)
        {
            if (gridCells[i] == null || !gridCells[i].IsEmpty)
                continue;
            gridCells[i].kind = InventoryGridItemKind.ClueNote;
            gridCells[i].count = 1;
            gridCells[i].customPayload = p;
            RaiseInventoryDisplayChanged();
            return true;
        }
        Debug.LogWarning("PlayerInventory: 网格已满，无法放入纸条。");
        return false;
    }

    // 快捷使用：从靠前的医疗包格扣 amount 个，扣不够则返回 false
    public bool TryConsumeMedKit(int amount)
    {
        if (amount <= 0) return true;
        EnsureGridArray();
        int left = amount;
        for (int i = 0; i < GridSlotCount && left > 0; i++)
        {
            if (gridCells[i] == null || gridCells[i].kind != InventoryGridItemKind.MedKit)
                continue;
            int take = Mathf.Min(left, gridCells[i].count);
            gridCells[i].count -= take;
            left -= take;
            if (gridCells[i].count <= 0)
            {
                gridCells[i].kind = InventoryGridItemKind.Empty;
                gridCells[i].count = 0;
                gridCells[i].customPayload = "";
            }
        }
        if (left > 0) return false;
        OnMedKitChanged?.Invoke(GetMedKitTotal());
        RaiseInventoryDisplayChanged();
        return true;
    }

    // 背包 UI 右键指定格使用医疗包：只扣这一格，不跨格
    public bool TryConsumeMedKitFromSlot(int slotIndex)
    {
        EnsureGridArray();
        if (slotIndex < 0 || slotIndex >= GridSlotCount) return false;
        if (gridCells[slotIndex] == null || gridCells[slotIndex].kind != InventoryGridItemKind.MedKit) return false;
        if (gridCells[slotIndex].count < 1) return false;

        gridCells[slotIndex].count--;
        if (gridCells[slotIndex].count <= 0)
        {
            gridCells[slotIndex].kind = InventoryGridItemKind.Empty;
            gridCells[slotIndex].count = 0;
            gridCells[slotIndex].customPayload = "";
        }
        OnMedKitChanged?.Invoke(GetMedKitTotal());
        RaiseInventoryDisplayChanged();
        return true;
    }

    // 背包拖放：交换两格的全部内容（kind + count + payload）
    public void SwapGridSlots(int indexA, int indexB)
    {
        EnsureGridArray();
        if (indexA == indexB) return;
        if (indexA < 0 || indexA >= GridSlotCount || indexB < 0 || indexB >= GridSlotCount) return;

        InventoryGridCell a = gridCells[indexA];
        InventoryGridCell b = gridCells[indexB];
        var ak = a.kind; var ac = a.count; var ap = a.customPayload;
        a.kind = b.kind; a.count = b.count; a.customPayload = b.customPayload;
        b.kind = ak;     b.count = ac;     b.customPayload = ap;

        OnAmmoInventoryChanged?.Invoke();
        OnMedKitChanged?.Invoke(GetMedKitTotal());
        RaiseInventoryDisplayChanged();
    }

    // UI 读取单格内容用，slotIndex 越界返回 null
    public InventoryGridCell GetGridCell(int index)
    {
        EnsureGridArray();
        if (index < 0 || index >= GridSlotCount) return null;
        return gridCells[index];
    }

    // 存档：导出 16 格数据为可序列化结构
    public InventoryCellSave[] ExportCellsForSave()
    {
        EnsureGridArray();
        var arr = new InventoryCellSave[GridSlotCount];
        for (int i = 0; i < GridSlotCount; i++)
        {
            arr[i] = new InventoryCellSave
            {
                kind = (int)gridCells[i].kind,
                count = gridCells[i].count,
                customPayload = gridCells[i].customPayload ?? ""
            };
        }
        return arr;
    }

    // 读档：从存档数据覆盖网格，多余格子留空，然后触发 UI 刷新
    public void ImportCellsFromSave(InventoryCellSave[] cells)
    {
        EnsureGridArray();
        for (int i = 0; i < GridSlotCount; i++)
        {
            if (cells != null && i < cells.Length)
            {
                gridCells[i].kind = (InventoryGridItemKind)cells[i].kind;
                gridCells[i].count = Mathf.Max(0, cells[i].count);
                gridCells[i].customPayload = cells[i].customPayload ?? "";
            }
            else
            {
                gridCells[i].kind = InventoryGridItemKind.Empty;
                gridCells[i].count = 0;
                gridCells[i].customPayload = "";
            }
        }
        OnAmmoInventoryChanged?.Invoke();
        OnMedKitChanged?.Invoke(GetMedKitTotal());
        RaiseInventoryDisplayChanged();
    }
}
