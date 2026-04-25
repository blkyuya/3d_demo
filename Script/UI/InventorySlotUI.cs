using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 单个背包格子：图标 + 数量文本；右键通知 InventoryGridBagUI 处理使用；支持拖放与其它格交换物品。
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IDropHandler
{
    public Image backgroundImage;
    public Image iconImage;
    public TextMeshProUGUI countLabel;

    InventoryGridBagUI _grid;
    int _slotIndex;

    // 格子序号（0–15），供 InventoryGridBagUI 识别拖放源
    public int SlotIndex => _slotIndex;

    public void Bind(InventoryGridBagUI grid, int slotIndex)
    {
        _grid = grid;
        _slotIndex = slotIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;
        if (_grid != null)
            _grid.HandleSlotRightClick(_slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 允许开始拖拽以便放下到其它格（本格作为 pointerDrag 来源）。
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_grid == null || eventData.pointerDrag == null)
            return;
        var src = eventData.pointerDrag.GetComponent<InventorySlotUI>();
        if (src == null)
            return;
        _grid.ApplySlotDrop(src.SlotIndex, _slotIndex);
    }

    // 统一刷新格子外观：countText 为空则隐藏数量标签
    public void SetVisual(Sprite icon, Color bgColor, string countText)
    {
        if (backgroundImage != null)
            backgroundImage.color = bgColor;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            if (icon != null)
                iconImage.preserveAspect = true;
        }

        if (countLabel != null)
        {
            bool show = !string.IsNullOrEmpty(countText);
            countLabel.gameObject.SetActive(show);
            if (show)
                countLabel.text = countText;
        }
    }
}
