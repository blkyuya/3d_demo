using UnityEngine;
using UnityEngine.EventSystems;

// 背包医疗包图标交互：右键点击时调用 PlayerHealAction.TryUseMedKit。
// 需挂在医疗包图标的 Image 上，并勾选 Raycast Target。
public class MedKitSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("留空则从 UIManager 或场景中查找 PlayerHealAction")]
    public PlayerHealAction healAction;

    // 补引用，优先从 UIManager 取
    void Start()
    {
        if (healAction == null && UIManager.Instance != null)
            healAction = UIManager.Instance.playerHealAction;
        if (healAction == null)
            healAction = FindObjectOfType<PlayerHealAction>();
    }

    // 只响应右键，左键不处理
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (healAction != null)
            healAction.TryUseMedKit();
    }
}
