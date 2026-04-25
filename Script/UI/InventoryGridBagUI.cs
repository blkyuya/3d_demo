using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 4×4 背包格子 UI：每格对应 PlayerInventory 网格数据，可拖动交换格子，右键在医疗包格上使用。
// Awake 里运行时生成 16 个 InventorySlotUI 格子，隐藏旧 MedKitSlot 行；
// 挂载：Canvas 上的背包面板节点，与 PlayerInventoryBagUI 同物体或同父级。
public class InventoryGridBagUI : MonoBehaviour
{
    public const int Columns = 4;
    public const int Rows = 4;
    public const int SlotCount = Columns * Rows;

    [Header("引用")]
    [Tooltip("与 PlayerInventoryBagUI 的 bagPanelRoot 一致")]
    public GameObject bagPanelRoot;

    public PlayerInventory playerInventory;
    public PlayerHealAction healAction;

    [Header("图标（拖入 asset_download/sucai/icons）")]
    public Sprite iconMedkit;
    public Sprite iconKey;
    public Sprite iconBullets;
    public Sprite iconShotgunAmmo;

    [Tooltip("纸条；可空则用钥匙图占位")]
    public Sprite iconClueNote;

    [Tooltip("右键纸条时弹出；可空则运行时查找")]
    public ClueNoteReadPanel clueReadPanel;

    [Header("外观")]
    public Vector2 cellSize = new Vector2(72f, 72f);
    public Vector2 spacing = new Vector2(8f, 8f);
    public Color emptySlotColor = new Color(0.15f, 0.15f, 0.15f, 0.65f);
    public Color filledSlotColor = new Color(0.25f, 0.25f, 0.28f, 0.9f);

    InventorySlotUI[] _slots = new InventorySlotUI[SlotCount];
    RectTransform _gridRoot;
    bool _built;

    void Awake()
    {
        if (bagPanelRoot == null)
        {
            var bagUi = GetComponent<PlayerInventoryBagUI>();
            if (bagUi != null)
                bagPanelRoot = bagUi.bagPanelRoot;
        }

        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();
        if (healAction == null)
        {
            if (UIManager.Instance != null)
                healAction = UIManager.Instance.playerHealAction;
            if (healAction == null)
                healAction = FindObjectOfType<PlayerHealAction>();
        }

        if (playerInventory != null)
            playerInventory.OnInventoryDisplayChanged += RefreshAllSlots;
    }

    void Start()
    {
        BuildGridIfNeeded();
        RefreshAllSlots();
    }

    void OnDestroy()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryDisplayChanged -= RefreshAllSlots;
    }

    void BuildGridIfNeeded()
    {
        if (_built || bagPanelRoot == null)
            return;

        Transform content = bagPanelRoot.transform.Find("Content");
        if (content == null)
            content = bagPanelRoot.transform;

        var oldMed = content.Find("MedKitSlot");
        if (oldMed != null)
            oldMed.gameObject.SetActive(false);
        var legacyCount = content.Find("MedKitSlot/MedKitCountText");
        if (legacyCount != null)
            legacyCount.gameObject.SetActive(false);

        var go = new GameObject("InventoryGrid4x4", typeof(RectTransform));
        go.transform.SetParent(content, false);
        _gridRoot = go.GetComponent<RectTransform>();
        _gridRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _gridRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _gridRoot.pivot = new Vector2(0.5f, 0.5f);
        float w = Columns * cellSize.x + (Columns - 1) * spacing.x;
        float h = Rows * cellSize.y + (Rows - 1) * spacing.y;
        _gridRoot.sizeDelta = new Vector2(w, h);
        _gridRoot.anchoredPosition = new Vector2(0f, 40f);

        var grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;

        for (int i = 0; i < SlotCount; i++)
            CreateSlotCell(_gridRoot, i);

        _built = true;
    }

    void CreateSlotCell(Transform parent, int index)
    {
        var cell = new GameObject("Slot_" + index, typeof(RectTransform));
        cell.transform.SetParent(parent, false);

        var bg = cell.AddComponent<Image>();
        bg.color = emptySlotColor;
        bg.raycastTarget = true;

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(cell.transform, false);
        var icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        var irt = icon.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.1f, 0.1f);
        irt.anchorMax = new Vector2(0.9f, 0.45f);
        irt.offsetMin = Vector2.zero;
        irt.offsetMax = Vector2.zero;

        var txtGo = new GameObject("Count", typeof(RectTransform));
        txtGo.transform.SetParent(cell.transform, false);
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.BottomRight;
        tmp.fontSize = 18;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var trt = tmp.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 0.35f);
        trt.offsetMin = new Vector2(4f, 2f);
        trt.offsetMax = new Vector2(-4f, -2f);

        var slotUi = cell.AddComponent<InventorySlotUI>();
        slotUi.backgroundImage = bg;
        slotUi.iconImage = icon;
        slotUi.countLabel = tmp;
        slotUi.Bind(this, index);
        _slots[index] = slotUi;
    }

    // 供 InventorySlotUI 右键回调：纸条格弹阅读面板，医疗包格调用治疗
    public void HandleSlotRightClick(int slotIndex)
    {
        if (playerInventory == null)
            return;

        InventoryGridCell cell = playerInventory.GetGridCell(slotIndex);
        if (cell == null || cell.IsEmpty)
            return;

        if (cell.kind == InventoryGridItemKind.ClueNote)
        {
            var panel = clueReadPanel != null ? clueReadPanel : FindObjectOfType<ClueNoteReadPanel>();
            if (panel != null)
                panel.Show(cell.customPayload ?? "");
            return;
        }

        if (healAction == null)
            return;
        if (cell.kind != InventoryGridItemKind.MedKit || cell.count < 1)
            return;

        healAction.TryUseMedKitFromSlot(slotIndex);
    }

    // 供拖放 OnDrop 调用：交换两格物品数据
    public void ApplySlotDrop(int fromIndex, int toIndex)
    {
        if (playerInventory == null)
            return;
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
            return;
        playerInventory.SwapGridSlots(fromIndex, toIndex);
    }

    void RefreshAllSlots()
    {
        if (!_built || playerInventory == null)
            return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] == null)
                continue;

            InventoryGridCell cell = playerInventory.GetGridCell(i);
            if (cell == null || cell.IsEmpty)
            {
                _slots[i].SetVisual(null, emptySlotColor, "");
                continue;
            }

            switch (cell.kind)
            {
                case InventoryGridItemKind.MedKit:
                {
                    string txt = cell.count > 0 ? "×" + cell.count : "";
                    _slots[i].SetVisual(iconMedkit, filledSlotColor, txt);
                    break;
                }
                case InventoryGridItemKind.Key:
                {
                    string txt = cell.count > 1 ? cell.count.ToString() : "1";
                    _slots[i].SetVisual(iconKey, filledSlotColor, txt);
                    break;
                }
                case InventoryGridItemKind.AmmoPistol:
                {
                    string txt = cell.count > 0 ? cell.count.ToString() : "";
                    _slots[i].SetVisual(iconBullets, filledSlotColor, txt);
                    break;
                }
                case InventoryGridItemKind.AmmoShotgun:
                {
                    string txt = cell.count > 0 ? cell.count.ToString() : "";
                    _slots[i].SetVisual(iconShotgunAmmo, filledSlotColor, txt);
                    break;
                }
                case InventoryGridItemKind.ClueNote:
                {
                    Sprite sp = iconClueNote != null ? iconClueNote : iconKey;
                    _slots[i].SetVisual(sp, filledSlotColor, "纸条");
                    break;
                }
                default:
                    _slots[i].SetVisual(null, emptySlotColor, "");
                    break;
            }
        }
    }
}
