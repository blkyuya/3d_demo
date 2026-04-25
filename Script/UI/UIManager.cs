using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 场景级 UI 入口：集中持有常用玩家/UI 引用，减少其他脚本到处 FindObjectOfType 的开销。
// 提供简易面板栈（弹窗类 Push/Pop），暂停菜单、存档界面等弹窗通过这里管理层级关系。
// 执行顺序设为 -80，比普通脚本早，保证 GameStateManager 在 Start 里能拿到 inventoryBagUI 等引用。
// 挂载：与 Canvas 同级或场景常驻空物体上。
[DefaultExecutionOrder(-80)]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("常用引用（推荐在 Inspector 一次性拖入，避免运行时查找）")]
    [Tooltip("玩家身上的治疗组件")]
    public PlayerHealAction playerHealAction;

    [Tooltip("玩家背包数据")]
    public PlayerInventory playerInventory;

    [Tooltip("背包面板 UI")]
    public PlayerInventoryBagUI inventoryBagUI;

    [Tooltip("交互提示 UI")]
    public InteractionPromptUI interactionPromptUI;

    [Header("拾取飘字字体（可选）")]
    [Tooltip("拖入思源黑体 TMP 字体资源，供 PickupToastUI 使用")]
    [SerializeField]
    TMP_FontAsset pickupToastFont;

    [Header("三层 Canvas（可选，由菜单「Canvas 分层」自动填充）")]
    public Canvas canvasHudStatic;
    public Canvas canvasHudDynamic;
    public Canvas canvasPopup;

    // 弹窗栈：Push 显示，Pop 隐藏；Esc 关闭时从栈顶弹
    readonly Stack<BasePanel> _popupStack = new Stack<BasePanel>();

    // 单例初始化，注册飘字字体
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (pickupToastFont != null)
            PickupToastUI.RegisterSharedToastFont(pickupToastFont);
    }

    // 销毁时清空单例，防止场景切换后引用到已销毁对象
    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 将弹窗类面板压栈并显示（如暂停菜单、存档界面）
    public void PushPopup(BasePanel panel)
    {
        if (panel == null) return;
        _popupStack.Push(panel);
        panel.Show();
    }

    // 关闭栈顶弹窗；传入 expectedTop 时若不是栈顶则只 Hide 不弹栈，保持栈结构正确
    public void PopPopup(BasePanel expectedTop = null)
    {
        if (_popupStack.Count == 0) return;
        if (expectedTop != null && _popupStack.Count > 0 && _popupStack.Peek() != expectedTop)
        {
            expectedTop.Hide();
            return;
        }
        BasePanel top = _popupStack.Pop();
        top.Hide();
    }

    // 当前弹窗栈深度，调试用
    public int PopupStackDepth => _popupStack.Count;

    // 清空栈，切场景前调用，防止残留引用
    public void ClearPopupStack()
    {
        while (_popupStack.Count > 0)
        {
            var p = _popupStack.Pop();
            if (p != null) p.Hide();
        }
    }
}
