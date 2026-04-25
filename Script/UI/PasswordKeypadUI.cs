using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 密码门屏幕数字键盘 UI：提供 0-9、确定、取消按钮；
// Awake 里遍历子物体的 PasswordKeypadButtonBinder 自动绑定点击事件，不用逐个手动拖。
// 挂载：密码门相关的 Canvas 下键盘面板节点。
public class PasswordKeypadUI : MonoBehaviour
{
    [Header("显示（可与 PasswordDoorController.inputDisplayText 共用同一 TMP）")]
    public TMP_Text displayText;

    PasswordDoorController _boundDoor;

    void Awake()
    {
        WireBindersInChildren();
    }

    // 遍历子物体的 Binder 组件，自动把 Button.onClick 绑到对应 actionId，省掉手动拖绑
    void WireBindersInChildren()
    {
        foreach (var binder in GetComponentsInChildren<PasswordKeypadButtonBinder>(true))
        {
            var btn = binder.GetComponent<Button>();
            if (btn == null)
                continue;
            int id = binder.actionId;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnKeypadAction(id));
        }
    }

    // 密码门打开会话时绑定门引用并显示键盘面板
    public void BindAndShow(PasswordDoorController door)
    {
        _boundDoor = door;
        if (gameObject.activeSelf == false)
            gameObject.SetActive(true);
    }

    public void HidePanel()
    {
        _boundDoor = null;
        gameObject.SetActive(false);
    }

    // 以下供 Button OnClick 绑定（Unity 默认 OnClick 无法传参，故拆成 10 个方法）
    public void UIButton0() => SendDigit(0);
    public void UIButton1() => SendDigit(1);
    public void UIButton2() => SendDigit(2);
    public void UIButton3() => SendDigit(3);
    public void UIButton4() => SendDigit(4);
    public void UIButton5() => SendDigit(5);
    public void UIButton6() => SendDigit(6);
    public void UIButton7() => SendDigit(7);
    public void UIButton8() => SendDigit(8);
    public void UIButton9() => SendDigit(9);

    void SendDigit(int d)
    {
        if (_boundDoor != null)
            _boundDoor.UiAppendDigit(d);
    }

    public void UIButtonConfirm()
    {
        if (_boundDoor != null)
            _boundDoor.UiConfirm();
    }

    public void UIButtonCancel()
    {
        if (_boundDoor != null)
            _boundDoor.UiCancel();
    }

    // actionId 0-9 为数字，10 为确定，11 为取消（与 PasswordKeypadButtonBinder 保持一致）
    public void OnKeypadAction(int action)
    {
        if (action >= 0 && action <= 9)
        {
            SendDigit(action);
            return;
        }

        if (action == 10)
            UIButtonConfirm();
        else if (action == 11)
            UIButtonCancel();
    }

    // 同步门侧输入缓冲到键盘面板的 TMP 显示文字
    public void SetDisplayFromBuffer(string bufferDisplay)
    {
        if (displayText != null)
            displayText.text = bufferDisplay;
    }
}
