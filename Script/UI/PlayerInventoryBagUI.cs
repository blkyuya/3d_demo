using TMPro;
using UnityEngine;

// 简易背包面板：Tab 键开关，显示医疗包数量；打开时可选解锁鼠标以右键使用医疗包。
// 与 MedKitSlotUI 配合：槽位上右键调用 PlayerHealAction.TryUseMedKit。
// 挂载：HUD Canvas 下的背包面板节点。
public class PlayerInventoryBagUI : BasePanel
{
    [Header("引用")]
    [Tooltip("要显示/隐藏的背包根节点（建议含半透明底图）")]
    public GameObject bagPanelRoot;

    [Tooltip("显示医疗包数量的文本，可为空")]
    public TextMeshProUGUI medKitCountText;

    [Tooltip("玩家身上的 PlayerHealAction，右键槽位时会调用")]
    public PlayerHealAction healAction;

    [Header("输入")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("鼠标")]
    [Tooltip("关闭：打开背包仅显示 UI，不显示鼠标（第三人称用视角；医疗可用快捷键）。开启：解锁鼠标以便在槽位上右键使用医疗包。")]
    public bool unlockCursorWhenOpen = false;

    bool _isOpen;
    PlayerInventory _subscribedInventory;

    // 供武器等系统判断：背包是否打开（打开时应暂停射击等）
    public bool IsBagOpen => _isOpen;

    protected override void OnPanelInit()
    {
        if (bagPanelRoot != null)
            panelRoot = bagPanelRoot;

        if (healAction == null && UIManager.Instance != null)
            healAction = UIManager.Instance.playerHealAction;
        if (healAction == null)
            healAction = FindObjectOfType<PlayerHealAction>();

        if (bagPanelRoot != null)
            bagPanelRoot.SetActive(false);

        _subscribedInventory = UIManager.Instance != null ? UIManager.Instance.playerInventory : null;
        if (_subscribedInventory == null)
            _subscribedInventory = FindObjectOfType<PlayerInventory>();

        if (_subscribedInventory != null)
        {
            _subscribedInventory.OnMedKitChanged += OnMedKitCountChanged;
            RefreshMedKitText(_subscribedInventory.medKitCount);
        }
    }

    void OnDestroy()
    {
        if (_subscribedInventory != null)
            _subscribedInventory.OnMedKitChanged -= OnMedKitCountChanged;
    }

    void Update()
    {
        if (bagPanelRoot == null)
            return;
        if (!GameStateManager.IsGameplayPlaying)
            return;
        if (Input.GetKeyDown(toggleKey))
            ToggleBag();
    }

    void OnMedKitCountChanged(int count)
    {
        RefreshMedKitText(count);
    }

    void RefreshMedKitText(int count)
    {
        if (medKitCountText != null)
            medKitCountText.text = "× " + count;
    }

    public void ToggleBag()
    {
        SetBagOpen(!_isOpen);
    }

    public void SetBagOpen(bool open)
    {
        if (bagPanelRoot == null)
            return;
        _isOpen = open;
        bagPanelRoot.SetActive(open);

        if (!unlockCursorWhenOpen)
            return;

        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
