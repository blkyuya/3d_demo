using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 线索纸条阅读面板：背包右键纸条格时弹出的只读展示面板，显示纸条文本（如密码）。
// 默认隐藏，由 InventoryGridBagUI 在右键触发时调用 Show；关闭按钮或外部调用 Hide。
// 挂载：Canvas 弹窗层子物体，默认 SetActive(false)。
public class ClueNoteReadPanel : MonoBehaviour
{
    [Header("引用")]
    public GameObject panelRoot;
    public TMP_Text bodyText;
    public Button closeButton;

    // 绑定关闭按钮，留空则只能通过代码关闭
    void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    // 显示面板并填充纸条内容
    public void Show(string content)
    {
        if (bodyText != null)
            bodyText.text = string.IsNullOrEmpty(content) ? "（空白纸条）" : content;
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    // 隐藏面板
    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
