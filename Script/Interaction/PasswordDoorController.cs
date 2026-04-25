using System.Collections;
using TMPro;
using UnityEngine;

// 密码门控制器：靠近按 E 打开密码输入界面，优先使用屏幕 PasswordKeypadUI；
// 未指定 UI 时退回 3D 射线点击 PuzzleDigitKey。
// 密码正确后播放开门动画，可选延迟触发 HordeWaveManager 刷怪。
// 挂载：密码门触发体根节点，与门扇 Transform 配合。
[DisallowMultipleComponent]
public class PasswordDoorController : MonoBehaviour
{
    [Header("密码")]
    [Tooltip("四位数字字符串，例如 1234")]
    public string correctCode = "1234";

    [Header("屏幕键盘 UI（推荐）")]
    [Tooltip("若赋值则按 E 打开该 UI，用按钮输入；留空则用下方 3D 键盘")]
    public PasswordKeypadUI keypadUi;

    [Header("门表现")]
    [Tooltip("绕本地 Y 轴旋转开门的门扇 Transform")]
    public Transform doorLeaf;

    [Tooltip("开门角度（度，绕 Y）")]
    public float openAngleDegrees = 90f;

    [Tooltip("开门插值速度")]
    public float openSpeed = 3f;

    [Tooltip("开门后禁用的阻挡碰撞体（可走通道）；可为空")]
    public Collider blockingCollider;

    [Header("尸潮（可选）")]
    [Tooltip("开门成功后延迟若干秒再开始刷尸潮；与「过阈值触发」二选一即可")]
    public HordeWaveManager hordeAfterDoorOpen;

    [Tooltip("开门成功后延迟秒数再 BeginHorde")]
    public float hordeDelayAfterUnlockSeconds = 3f;

    [Header("3D 键盘（无 UI 时使用）")]
    [Tooltip("数字键盘根物体，平时可隐藏，开门后保持隐藏")]
    public GameObject keyboardRoot;

    [Tooltip("显示当前已输入位数的文本（可为 World 或 Screen TMP）")]
    public TMP_Text inputDisplayText;

    [Header("射线")]
    [Tooltip("用于点击数字键的相机；留空则用 Camera.main")]
    public Camera rayCamera;

    public LayerMask raycastMask = ~0;

    [Header("距离")]
    public float interactRange = 2.5f;

    [Header("引用")]
    public Transform player;

    InteractionPromptUI _prompt;
    Quaternion _closedLocalRot;
    Quaternion _openLocalRot;
    bool _doorOpened;
    bool _sessionForThisDoor;
    string _buffer = "";
    bool _hordeScheduled;

    // 门已解锁（密码正确并执行开门后）；尸潮触发等可读取
    public bool IsDoorOpened => _doorOpened;

    void Start()
    {
        if (UIManager.Instance != null)
            _prompt = UIManager.Instance.interactionPromptUI;
        if (_prompt == null)
            _prompt = FindObjectOfType<InteractionPromptUI>();
        if (player == null)
        {
            var inv = FindObjectOfType<PlayerInventory>();
            if (inv != null)
                player = inv.transform;
        }

        if (rayCamera == null)
            rayCamera = Camera.main;

        if (doorLeaf != null)
        {
            _closedLocalRot = doorLeaf.localRotation;
            _openLocalRot = _closedLocalRot * Quaternion.Euler(0f, openAngleDegrees, 0f);
        }

        if (keyboardRoot != null)
            keyboardRoot.SetActive(false);
        if (keypadUi != null)
            keypadUi.gameObject.SetActive(false);
        RefreshDisplayText();
    }

    void Update()
    {
        if (!GameStateManager.IsGameplayPlaying || _doorOpened)
            return;

        if (!IsPlayerInRange())
        {
            if (_sessionForThisDoor)
                CloseSession();
            if (_prompt != null)
                _prompt.HidePrompt();
            return;
        }

        if (!_sessionForThisDoor && _prompt != null)
            _prompt.ShowPrompt("按 E 输入密码");

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!_sessionForThisDoor)
                OpenSession();
            else
                CloseSession();
        }

        if (_sessionForThisDoor && Input.GetKeyDown(KeyCode.Escape))
            CloseSession();

        if (_sessionForThisDoor && keypadUi == null && Input.GetMouseButtonDown(0))
            TryRaycastDigit();
    }

    bool IsPlayerInRange()
    {
        if (player == null)
            return false;
        return Vector3.Distance(transform.position, player.position) <= interactRange;
    }

    void OpenSession()
    {
        _sessionForThisDoor = true;
        PasswordDoorSession.Begin();
        _buffer = "";
        if (keypadUi != null)
        {
            keypadUi.BindAndShow(this);
            if (keyboardRoot != null)
                keyboardRoot.SetActive(false);
        }
        else if (keyboardRoot != null)
            keyboardRoot.SetActive(true);
        RefreshDisplayText();
        if (_prompt != null)
            _prompt.ShowPrompt(keypadUi != null ? "输入密码后点确定 · Esc 取消" : "点击数字键输入 · Esc 取消");
    }

    void CloseSession()
    {
        if (!_sessionForThisDoor)
            return;
        _sessionForThisDoor = false;
        PasswordDoorSession.End();
        if (keyboardRoot != null)
            keyboardRoot.SetActive(false);
        if (keypadUi != null)
            keypadUi.HidePanel();
        _buffer = "";
        RefreshDisplayText();
        if (_prompt != null && !_doorOpened && IsPlayerInRange())
            _prompt.ShowPrompt("按 E 输入密码");
        else if (_prompt != null)
            _prompt.HidePrompt();
    }

    void TryRaycastDigit()
    {
        if (rayCamera == null)
            return;
        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 80f, raycastMask, QueryTriggerInteraction.Collide))
            return;
        var key = hit.collider.GetComponentInParent<PuzzleDigitKey>();
        if (key == null)
            key = hit.collider.GetComponent<PuzzleDigitKey>();
        if (key == null)
            return;

        _buffer += key.digit.ToString();
        if (_buffer.Length > 4)
            _buffer = _buffer.Substring(_buffer.Length - 4);
        RefreshDisplayText();

        // 仅 3D 键盘：满四位自动尝试开门
        if (keypadUi == null && _buffer.Length >= 4)
            TrySubmit();
    }

    // 屏幕键盘按钮调用：追加一位数字（0-9）
    public void UiAppendDigit(int digit)
    {
        if (!_sessionForThisDoor || _doorOpened)
            return;
        digit = Mathf.Clamp(digit, 0, 9);
        _buffer += digit.ToString();
        if (_buffer.Length > 4)
            _buffer = _buffer.Substring(_buffer.Length - 4);
        RefreshDisplayText();
    }

    // 屏幕键盘「确定」按钮回调
    public void UiConfirm()
    {
        if (!_sessionForThisDoor || _doorOpened)
            return;
        TrySubmit();
    }

    // 屏幕键盘「取消」按钮回调
    public void UiCancel()
    {
        CloseSession();
    }

    void TrySubmit()
    {
        if (keypadUi != null && _buffer.Length != 4)
        {
            PickupNotificationHub.Publish("请输入四位数字后点确定");
            return;
        }

        string want = NormalizeCode(correctCode);
        string got = NormalizeCode(_buffer);
        if (got == want)
        {
            _doorOpened = true;
            PickupNotificationHub.Publish("门锁已打开");
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx3D(AudioKeys.ItemPickup, transform.position);
            CloseSession();
            if (_prompt != null)
                _prompt.HidePrompt();
            StartCoroutine(OpenDoorAnim());
            TryScheduleHordeAfterUnlock();
        }
        else
        {
            PickupNotificationHub.Publish("密码错误");
            _buffer = "";
            RefreshDisplayText();
        }
    }

    static string NormalizeCode(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        var t = "";
        foreach (char c in s)
        {
            if (c >= '0' && c <= '9')
                t += c;
        }
        if (t.Length >= 4)
            return t.Substring(0, 4);
        return t.PadLeft(4, '0');
    }

    void RefreshDisplayText()
    {
        string line;
        if (_buffer.Length == 0)
            line = "_ _ _ _";
        else
        {
            string a = _buffer.PadRight(4, '_');
            line = string.Format("{0} {1} {2} {3}", a[0], a[1], a[2], a[3]);
        }

        if (inputDisplayText != null)
            inputDisplayText.text = line;
        if (keypadUi != null && keypadUi.displayText != null)
            keypadUi.displayText.text = line;
    }

    void TryScheduleHordeAfterUnlock()
    {
        if (_hordeScheduled || hordeAfterDoorOpen == null)
            return;
        _hordeScheduled = true;
        StartCoroutine(HordeAfterDelay());
    }

    IEnumerator HordeAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, hordeDelayAfterUnlockSeconds));
        if (hordeAfterDoorOpen != null && !hordeAfterDoorOpen.HasStarted)
            hordeAfterDoorOpen.BeginHorde();
    }

    IEnumerator OpenDoorAnim()
    {
        if (blockingCollider != null)
            blockingCollider.enabled = false;
        if (doorLeaf == null)
            yield break;
        while (Quaternion.Angle(doorLeaf.localRotation, _openLocalRot) > 0.4f)
        {
            doorLeaf.localRotation = Quaternion.Slerp(doorLeaf.localRotation, _openLocalRot, openSpeed * Time.deltaTime);
            yield return null;
        }

        doorLeaf.localRotation = _openLocalRot;
    }

    void OnDisable()
    {
        if (_sessionForThisDoor)
            CloseSession();
    }
}
