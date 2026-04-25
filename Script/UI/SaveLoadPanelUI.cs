using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 5 槽存档 UI：先选「存档/读取」模式再点槽位，存档时有二次确认弹框；
// 读取后加载场景并通过 SaveLoadPendingStore 把数据传给 SaveLoadBootstrap 应用。
// 挂载：Canvas 弹窗层下独立面板根节点，槽位按钮和确认框在 Inspector 绑定。
public class SaveLoadPanelUI : BasePanel
{
    public static SaveLoadPanelUI Instance { get; private set; }

    // 打开时阻塞角色操作（与 GameStateManager.IsGameplayPlaying 联动）
    public static bool IsOpenBlockingGameplay { get; private set; }

    [Header("主面板")]
    [SerializeField]
    GameObject mainPanelRoot;

    [SerializeField]
    TextMeshProUGUI titleText;

    [SerializeField]
    TextMeshProUGUI hintText;

    [Header("五个存档位（Button + 子物体 TMP 显示摘要）")]
    [SerializeField]
    Button[] slotButtons = new Button[5];

    [SerializeField]
    TextMeshProUGUI[] slotLineTexts = new TextMeshProUGUI[5];

    [Header("底部按钮")]
    [SerializeField]
    Button buttonRead;

    [SerializeField]
    Button buttonSave;

    [SerializeField]
    Button buttonClose;

    [Header("二次确认框")]
    [SerializeField]
    GameObject confirmRoot;

    [SerializeField]
    TextMeshProUGUI confirmMessageText;

    [SerializeField]
    Button confirmOkButton;

    [SerializeField]
    Button confirmCancelButton;

    bool _modeSave;
    bool _modeLoad;
    int _pendingSlot = -1;
    Action _confirmAction;

    // 各控件独立绑定标记，避免槽位未配满时整段 return 导致底部按钮漏绑
    bool _btnReadBound;

    bool _btnSaveBound;

    bool _btnCloseBound;

    readonly bool[] _slotBtnBound = new bool[SaveFileService.SlotCount];

    bool _confirmOkBound;

    bool _confirmCancelBound;

    protected override void OnPanelInit()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (mainPanelRoot != null)
            panelRoot = mainPanelRoot;

        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (confirmRoot != null)
            confirmRoot.SetActive(false);

        // 全屏暗色底若开启 Raycast Target，会挡住下层「读取/存档/关闭」按钮；仅中间面板挡点击。
        FixConfirmBackdropRaycast();

        TryBindListeners();
    }

    // 确认框根节点全屏 Image 关闭 Raycast Target，否则会挡住主面板底部按钮
    void FixConfirmBackdropRaycast()
    {
        if (confirmRoot == null)
            return;
        var rootImg = confirmRoot.GetComponent<Image>();
        if (rootImg != null)
            rootImg.raycastTarget = false;
    }

    // 供 GameStateManager 判断：存档 UI 是否打开（含 Esc 关闭逻辑）
    public bool IsOpenForInput()
    {
        return IsOpenBlockingGameplay && panelRoot != null && panelRoot.activeSelf;
    }

    // Esc：先关确认框，再关整个存档界面
    public void OnEscapePressed()
    {
        if (confirmRoot != null && confirmRoot.activeSelf)
        {
            OnConfirmCancel();
            return;
        }

        ClosePanel();
    }

    void Start()
    {
        TryBindListeners();
    }

    void TryBindListeners()
    {
        if (buttonRead != null && !_btnReadBound)
        {
            buttonRead.onClick.AddListener(OnClickReadMode);
            _btnReadBound = true;
        }

        if (buttonSave != null && !_btnSaveBound)
        {
            buttonSave.onClick.AddListener(OnClickSaveMode);
            _btnSaveBound = true;
        }

        if (buttonClose != null && !_btnCloseBound)
        {
            buttonClose.onClick.AddListener(ClosePanel);
            _btnCloseBound = true;
        }

        if (slotButtons != null)
        {
            for (int i = 0; i < slotButtons.Length && i < SaveFileService.SlotCount; i++)
            {
                if (slotButtons[i] == null || _slotBtnBound[i])
                    continue;
                int idx = i;
                slotButtons[i].onClick.AddListener(() => OnSlotClicked(idx));
                _slotBtnBound[i] = true;
            }
        }

        if (confirmOkButton != null && !_confirmOkBound)
        {
            confirmOkButton.onClick.AddListener(OnConfirmOk);
            _confirmOkBound = true;
        }

        if (confirmCancelButton != null && !_confirmCancelBound)
        {
            confirmCancelButton.onClick.AddListener(OnConfirmCancel);
            _confirmCancelBound = true;
        }

        // Image 未指定 Sprite 时，部分环境下射线命中不稳定；补 1×1 白图并保留极低透明度。
        EnsureButtonClickTargets(buttonRead);
        EnsureButtonClickTargets(buttonSave);
        EnsureButtonClickTargets(buttonClose);
        EnsureButtonClickTargets(confirmOkButton);
        EnsureButtonClickTargets(confirmCancelButton);
        if (slotButtons != null)
        {
            for (int i = 0; i < slotButtons.Length && i < SaveFileService.SlotCount; i++)
                EnsureButtonClickTargets(slotButtons[i]);
        }
    }

    // 为无 Sprite 的按钮 Image 生成可命中区域，避免「看得见却点不到」
    static void EnsureButtonClickTargets(Button btn)
    {
        if (btn == null)
            return;
        var img = btn.GetComponent<Image>();
        if (img == null)
            return;
        if (!img.raycastTarget)
            img.raycastTarget = true;
        if (img.sprite != null)
            return;
        var tex = Texture2D.whiteTexture;
        img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        var c = img.color;
        img.color = new Color(c.r, c.g, c.b, Mathf.Max(c.a, 0.01f));
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        IsOpenBlockingGameplay = false;
    }

    // 检查点或菜单调用：打开存档 UI
    public void OpenPanel()
    {
        TryBindListeners();

        if (panelRoot != null && panelRoot.activeSelf)
            return;

        _modeSave = false;
        _modeLoad = false;
        ClearHint();
        RefreshSlotTexts();

        IsOpenBlockingGameplay = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (UIManager.Instance != null)
            UIManager.Instance.PushPopup(this);
        else
            Show();

        if (titleText != null)
            titleText.text = "存档 / 读取";
    }

    public void ClosePanel()
    {
        if (confirmRoot != null)
            confirmRoot.SetActive(false);

        _modeSave = false;
        _modeLoad = false;
        _pendingSlot = -1;
        _confirmAction = null;

        IsOpenBlockingGameplay = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (UIManager.Instance != null)
            UIManager.Instance.PopPopup(this);
        else
            Hide();
    }

    void OnClickSaveMode()
    {
        _modeSave = true;
        _modeLoad = false;
        if (hintText != null)
            hintText.text = "已选择：存档 — 请点击下方一条空槽或已有槽以覆盖";
    }

    void OnClickReadMode()
    {
        _modeLoad = true;
        _modeSave = false;
        if (hintText != null)
            hintText.text = "已选择：读取 — 请点击下方已有存档的槽位";
    }

    void ClearHint()
    {
        if (hintText != null)
            hintText.text = "请先点击「存档」或「读取」，再选择槽位";
    }

    void OnSlotClicked(int slotIndex)
    {
        if (!_modeSave && !_modeLoad)
        {
            if (hintText != null)
                hintText.text = "请先点击下方的「存档」或「读取」按钮";
            return;
        }

        if (_modeSave)
        {
            bool exists = SaveFileService.SlotExists(slotIndex);
            _pendingSlot = slotIndex;
            if (confirmRoot != null)
                confirmRoot.SetActive(true);
            if (confirmMessageText != null)
            {
                confirmMessageText.text = exists
                    ? $"确定要覆盖「存档槽 {slotIndex + 1}」吗？"
                    : $"确定在「存档槽 {slotIndex + 1}」新建存档吗？";
            }

            _confirmAction = () =>
            {
                GameSaveData data = SaveGameCollector.BuildFromCurrentScene();
                if (SaveFileService.TrySave(slotIndex, data))
                    RefreshSlotTexts();
            };
            return;
        }

        if (_modeLoad)
        {
            if (!SaveFileService.SlotExists(slotIndex))
            {
                if (hintText != null)
                    hintText.text = "该槽没有存档";
                return;
            }

            _pendingSlot = slotIndex;
            if (confirmRoot != null)
                confirmRoot.SetActive(true);
            if (confirmMessageText != null)
                confirmMessageText.text = $"确定读取「存档槽 {slotIndex + 1}」？将重新加载场景。";

            _confirmAction = () =>
            {
                GameSaveData data = SaveFileService.TryLoad(slotIndex);
                if (data == null)
                    return;
                EventCenter.ClearAll();
                SaveLoadPendingStore.Pending = data;
                ClosePanel();
                SceneManager.LoadScene(data.sceneBuildIndex);
            };
        }
    }

    void OnConfirmOk()
    {
        if (confirmRoot != null)
            confirmRoot.SetActive(false);
        _confirmAction?.Invoke();
        _confirmAction = null;
        _pendingSlot = -1;
    }

    void OnConfirmCancel()
    {
        if (confirmRoot != null)
            confirmRoot.SetActive(false);
        _confirmAction = null;
        _pendingSlot = -1;
    }

    void RefreshSlotTexts()
    {
        for (int i = 0; i < slotLineTexts.Length && i < SaveFileService.SlotCount; i++)
        {
            if (slotLineTexts[i] == null)
                continue;
            if (!SaveFileService.SlotExists(i))
            {
                slotLineTexts[i].text = $"存档槽 {i + 1}\n<color=#888888>（空）</color>";
                continue;
            }

            DateTime? utc = SaveFileService.TryGetSavedTimeUtc(i);
            string timeStr = utc.HasValue
                ? utc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "未知时间";
            GameSaveData peek = SaveFileService.TryLoad(i);
            string sceneInfo = peek != null ? $"场景 #{peek.sceneBuildIndex}" : "";
            slotLineTexts[i].text = $"存档槽 {i + 1}\n{timeStr}  {sceneInfo}";
        }
    }
}
