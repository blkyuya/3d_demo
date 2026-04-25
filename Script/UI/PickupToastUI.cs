using System.Collections;
using TMPro;
using UnityEngine;

// 拾取飘字 UI：订阅 PickupNotificationHub，收到消息后显示文案停留再淡出。
// 首次发布消息时若场景无实例，EnsureExists 会自动在 Canvas 下创建一个；
// 挂载：第一个 Canvas 子物体 PickupToastUI（含 TMP 文字），也可不挂，由代码自动生成。
[DefaultExecutionOrder(-79)]
[DisallowMultipleComponent]
public class PickupToastUI : MonoBehaviour
{
    public static PickupToastUI Instance { get; private set; }

    // UIManager 等在 Awake 登记，供运行时动态创建的飘字共享同一套 TMP 字体
    static TMP_FontAsset _sharedToastFont;

    [Header("引用（可空，运行时自动创建）")]
    [SerializeField] TextMeshProUGUI toastText;
    [SerializeField] CanvasGroup canvasGroup;

    [Header("字体")]
    [Tooltip("留空则用 UIManager 登记的字体，再否则用 TMP 默认字体")]
    [SerializeField] TMP_FontAsset toastFontAsset;

    [Header("时间")]
    [SerializeField] float showSeconds = 2.2f;
    [SerializeField] float fadeSeconds = 0.55f;

    Coroutine _fadeRoutine;

    // 确保场景有飘字实例；首次拾取前由 PickupNotificationHub 调用
    public static void EnsureExists()
    {
        if (Instance != null) return;

        // 含未激活物体也查找，避免重复创建
        PickupToastUI existing = FindObjectOfType<PickupToastUI>(true);
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        // 场景没有则自动在 Canvas 下创建
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("PickupToastUI");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.88f);
        rt.anchorMax = new Vector2(0.5f, 0.88f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(720f, 48f);
        go.AddComponent<PickupToastUI>();
    }

    // UIManager 调用此方法登记共享字体；飘字已存在时立即刷新
    public static void RegisterSharedToastFont(TMP_FontAsset font)
    {
        if (font == null) return;
        _sharedToastFont = font;
        if (Instance != null)
            Instance.ApplyToastFont();
    }

    // 单例初始化，构建 UI 节点，订阅通知事件
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUiIfNeeded();
        ApplyToastFont();
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        PickupNotificationHub.OnMessage += OnPickupMessage;
    }

    // 取消订阅，清实例引用
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PickupNotificationHub.OnMessage -= OnPickupMessage;
    }

    // 如果没有在 Inspector 拖入 UI 组件就运行时创建文字节点
    void BuildUiIfNeeded()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (toastText != null) return;

        var textGo = new GameObject("ToastText", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        toastText = textGo.AddComponent<TextMeshProUGUI>();
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.fontSize = 22f;
        toastText.color = Color.white;
        ApplyToastFont();
    }

    // 优先用本组件指定字体，其次 UIManager 登记的，再次 TMP 项目默认
    public void ApplyToastFont()
    {
        if (toastText == null) return;
        TMP_FontAsset f = toastFontAsset != null ? toastFontAsset : _sharedToastFont;
        if (f == null && TMP_Settings.defaultFontAsset != null)
            f = TMP_Settings.defaultFontAsset;
        if (f != null) toastText.font = f;
    }

    // 收到飘字消息，打断上一条协程后重新显示
    void OnPickupMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (toastText == null || canvasGroup == null) return;

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(ShowAndFade(message));
    }

    // 显示文字 → 停留 showSeconds → 线性淡出 fadeSeconds
    IEnumerator ShowAndFade(string message)
    {
        toastText.text = message;
        canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(showSeconds);

        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - t / fadeSeconds);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        _fadeRoutine = null;
    }
}
