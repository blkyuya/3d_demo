using UnityEngine;

// UI 面板基类：统一 Show/Hide 与初始化入口，便于 UIManager 管理面板栈生命周期。
// 子类在 OnPanelInit 里订阅事件和做初始化，不要在子类重写 Awake 里写两套逻辑。
public abstract class BasePanel : MonoBehaviour
{
    [Header("面板根节点")]
    [Tooltip("留空则使用本脚本所在 GameObject；有独立 Panel 根物体时拖入（如仅控制子物体显隐）")]
    [SerializeField] protected GameObject panelRoot;

    // 面板是否可见，根据 panelRoot 的 activeSelf 判断
    public virtual bool IsVisible => panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf;

    // Awake 自动补全 panelRoot，再调 OnPanelInit
    protected virtual void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
        OnPanelInit();
    }

    // 子类重写这里，订阅事件、刷新初始 UI 数据
    protected virtual void OnPanelInit() { }

    // 激活面板根节点
    public virtual void Show(bool immediate = true)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        else gameObject.SetActive(true);
    }

    // 隐藏面板根节点
    public virtual void Hide(bool immediate = true)
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        else gameObject.SetActive(false);
    }
}
