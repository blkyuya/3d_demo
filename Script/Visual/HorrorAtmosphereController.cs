using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

// 恐怖氛围控制器：根据玩家血量驱动暗角/颗粒/冷色调，血量越低效果越强。
// 支持两种输出模式：
//   1) Post Processing Stack（仅当工程未启用 SRP 时内置路径才生效）
//   2) 全屏 UI 叠层（不依赖后处理栈，URP 项目或切管线时用这个）
// 若画面始终无变化，切换到「全屏 UI 叠层」模式即可。
// 挂载：场景根或 GameSystems 空物体。
[DefaultExecutionOrder(100)]
public class HorrorAtmosphereController : MonoBehaviour
{
    // 若画面始终无变化，请改用「全屏 UI 叠层」
    public enum HorrorOutputMode
    {
        [Tooltip("Post Processing v3；要求 Built-In 且 Graphics 中 SRP 为空")]
        PostProcessingStack = 0,

        [Tooltip("程序化暗角/颗粒/冷色叠在屏幕上，不依赖 PostProcessLayer")]
        ScreenSpaceUiOverlay = 1,
    }

    [Header("输出模式")]
    [SerializeField]
    HorrorOutputMode outputMode = HorrorOutputMode.ScreenSpaceUiOverlay;

    [Header("引用")]
    [Tooltip("留空则在场景中查找 PlayerHealth")]
    [SerializeField]
    PlayerHealth playerHealth;

    [Tooltip("仅 Post Processing 模式：留空则自动创建 Volume")]
    [SerializeField]
    PostProcessVolume postProcessVolume;

    [Tooltip("留空则解析 MainCamera / CinemachineBrain 上的 Camera")]
    [SerializeField]
    Camera targetCamera;

    [Header("危险度曲线（危险度 = 1 - 当前血量/最大血量）")]
    [SerializeField, Range(0f, 1f)]
    float vignetteAtFullHp = 0.4f;

    [SerializeField, Range(0f, 1f)]
    float vignetteAtLowHp = 0.72f;

    [SerializeField, Range(0f, 1f)]
    float grainAtFullHp = 0.018f;

    [SerializeField, Range(0f, 1f)]
    float grainAtLowHp = 0.09f;

    [Tooltip("偏冷色温（仅 Post Processing）；UI 模式用「冷色叠层强度」曲线")]
    [SerializeField, Range(-100f, 0f)]
    float temperatureAtFullHp = -14f;

    [SerializeField, Range(-100f, 0f)]
    float temperatureAtLowHp = -35f;

    [Header("UI 叠层专用（Screen Space Ui Overlay）")]
    [Tooltip("乘在暗角 RawImage 透明度上，略大于 1 可让暗角更明显")]
    [SerializeField, Range(1f, 2f)]
    float uiVignetteStrengthMul = 1.45f;

    [Tooltip("噪点贴图平铺次数，越大颗粒越密")]
    [SerializeField, Range(1f, 6f)]
    float uiGrainUvTile = 2.25f;

    [SerializeField, Range(0f, 0.25f)]
    float coldTintAlphaAtFullHp = 0.04f;

    [SerializeField, Range(0f, 0.35f)]
    float coldTintAlphaAtLowHp = 0.14f;

    [Header("调试")]
    [SerializeField]
    bool logSetupOnce = true;

    Vignette _vignette;
    Grain _grain;
    ColorGrading _colorGrading;
    bool _setupLogged;
    bool _missingHealthWarned;

    RawImage _uiVignette;
    RawImage _uiGrain;
    Image _uiColdTint;
    Texture2D _grainTex;
    Texture2D _vignetteTex;

    // 解析相机引用，根据模式初始化后处理或 UI 叠层
    void Awake()
    {
        if (targetCamera == null)
            targetCamera = ResolveGameplayCamera();

        LogPipelineDiagnostics();

        if (outputMode == HorrorOutputMode.ScreenSpaceUiOverlay)
        {
            EnsureUiOverlay();
            return;
        }

        EnsurePostProcessLayerOnCamera();
        EnsureVolumeAndEffects();
    }

    // 查找 PlayerHealth 并订阅血量变化事件，初始化一次效果
    void Start()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>(true);

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
            ApplyFromHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
        else
        {
            if (logSetupOnce && !_missingHealthWarned)
            {
                _missingHealthWarned = true;
                Debug.LogWarning(
                    "HorrorAtmosphereController: 未找到 PlayerHealth（含未激活物体）。将按「满血」曲线显示基准效果；请拖到引用槽。");
            }
            ApplyFromHealth(1, 1);
        }
    }

    // 取消订阅并销毁运行时生成的贴图，防止内存泄漏
    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= OnHealthChanged;

        if (_grainTex != null) Destroy(_grainTex);
        if (_vignetteTex != null) Destroy(_vignetteTex);
    }

    // 血量变化时重新计算并应用效果
    void OnHealthChanged(int current, int max)
    {
        ApplyFromHealth(current, max);
    }

    // 检测 SRP 状态，输出配置诊断信息；有问题时用 LogError 提示需要切模式
    void LogPipelineDiagnostics()
    {
        if (!logSetupOnce) return;

#if UNITY_2019_3_OR_NEWER
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp != null)
        {
            Debug.LogError(
                "【S12】GraphicsSettings.currentRenderPipeline 不为空（"
                + rp.name
                + "）。Post Processing v3 内置路径不会注入，暗角可能无效。"
                + " 请将「输出模式」改为「全屏 UI 叠层」，或清空 Project Settings - Graphics 中的 SRP。");
        }
#endif

        if (RuntimeUtilities.scriptableRenderPipelineActive)
        {
            Debug.LogError(
                "【S12】RuntimeUtilities.scriptableRenderPipelineActive == true，PostProcessLayer 的 Legacy 路径已跳过。"
                + " 请把「输出模式」改为「全屏 UI 叠层」。");
        }

        if (targetCamera != null)
        {
            Rect r = targetCamera.rect;
            if (Mathf.Abs(r.width - 1f) > 0.02f || Mathf.Abs(r.height - 1f) > 0.02f)
            {
                Debug.LogWarning(
                    "【S12】主摄像机 Rect 非全屏 (" + r + ")，内置后处理可能异常；可试「全屏 UI 叠层」模式。");
            }
        }
    }

    // 按优先级查找相机：Camera.main > CinemachineBrain 上的 Camera > 场景第一台 Camera
    Camera ResolveGameplayCamera()
    {
        Camera main = Camera.main;
        if (main != null) return main;

        CinemachineBrain brain = FindObjectOfType<CinemachineBrain>();
        if (brain != null)
        {
            Camera c = brain.GetComponent<Camera>();
            if (c != null)
            {
                if (logSetupOnce)
                    Debug.LogWarning(
                        "HorrorAtmosphereController: 未检测到 Camera.main，已改用 CinemachineBrain 所在摄像机：" + c.gameObject.name);
                return c;
            }
        }

        Camera any = FindObjectOfType<Camera>();
        if (any != null && logSetupOnce)
            Debug.LogWarning("HorrorAtmosphereController: 未检测到 Camera.main，已改用场景中第一台 Camera：" + any.gameObject.name);
        return any;
    }

    // 确保相机上有 PostProcessLayer，没有则自动添加
    void EnsurePostProcessLayerOnCamera()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("HorrorAtmosphereController: 未找到摄像机。");
            return;
        }

        PostProcessLayer layer = targetCamera.GetComponent<PostProcessLayer>();
        if (layer == null)
            layer = targetCamera.gameObject.AddComponent<PostProcessLayer>();

        layer.volumeLayer = (LayerMask)(-1);
        if (layer.volumeTrigger == null)
            layer.volumeTrigger = targetCamera.transform;
    }

    // 确保 PostProcessVolume 存在并包含暗角/颗粒/色调三种效果
    void EnsureVolumeAndEffects()
    {
        if (postProcessVolume == null)
        {
            postProcessVolume = FindObjectOfType<PostProcessVolume>();
            if (postProcessVolume == null)
            {
                var go = new GameObject("HorrorPostProcessVolume");
                go.transform.SetParent(transform, false);
                postProcessVolume = go.AddComponent<PostProcessVolume>();
                postProcessVolume.isGlobal = true;
                postProcessVolume.weight = 1f;
                postProcessVolume.priority = 0f;
            }
        }

        PostProcessProfile profile = postProcessVolume.profile;
        if (!profile.TryGetSettings(out _vignette)) _vignette = profile.AddSettings<Vignette>();
        if (!profile.TryGetSettings(out _grain)) _grain = profile.AddSettings<Grain>();
        if (!profile.TryGetSettings(out _colorGrading)) _colorGrading = profile.AddSettings<ColorGrading>();

        _vignette.enabled.value = true;
        _grain.enabled.value = true;
        _colorGrading.enabled.value = true;
        _colorGrading.gradingMode.value = GradingMode.LowDefinitionRange;
        _colorGrading.tonemapper.value = Tonemapper.Neutral;
        _vignette.mode.value = VignetteMode.Classic;
        _vignette.rounded.value = true;
        _grain.colored.value = true;
        _grain.size.value = 1.1f;
        _setupLogged = true;
    }

    // 动态创建全屏 Canvas（sortingOrder=32000 保证在最上层）和三层 UI：冷色底 → 颗粒 → 暗角
    void EnsureUiOverlay()
    {
        if (_uiVignette != null) return;

        var canvasGo = new GameObject("HorrorUiOverlay");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // blocksRaycasts=false 防止覆盖层拦截所有点击
        var cg = canvasGo.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        _vignetteTex = BuildRadialVignetteTexture(256, 256);
        _grainTex = BuildNoiseTexture(128, 128);

        // 绘制顺序：冷色底 → 颗粒 → 最上层暗角（暗角在最上层才能压住颗粒）
        var tintGo = new GameObject("ColdTint");
        tintGo.transform.SetParent(canvasGo.transform, false);
        _uiColdTint = tintGo.AddComponent<Image>();
        _uiColdTint.color = new Color(0.72f, 0.82f, 1f, coldTintAlphaAtFullHp);
        StretchFull(tintGo.GetComponent<RectTransform>());

        var grainGo = new GameObject("Grain");
        grainGo.transform.SetParent(canvasGo.transform, false);
        _uiGrain = grainGo.AddComponent<RawImage>();
        _uiGrain.texture = _grainTex;
        _uiGrain.uvRect = new Rect(0f, 0f, uiGrainUvTile, uiGrainUvTile);
        StretchFull(grainGo.GetComponent<RectTransform>());

        var vigGo = new GameObject("Vignette");
        vigGo.transform.SetParent(canvasGo.transform, false);
        _uiVignette = vigGo.AddComponent<RawImage>();
        _uiVignette.texture = _vignetteTex;
        StretchFull(vigGo.GetComponent<RectTransform>());

        _setupLogged = true;
    }

    // 把 RectTransform 拉伸到全屏
    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 程序化生成径向渐变暗角贴图：中心透明，角落不透明
    static Texture2D BuildRadialVignetteTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w - 0.5f;
                float ny = (y + 0.5f) / h - 0.5f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float t = dist / 0.70710678f;
                // SmoothStep 起始偏小，让暗角更早进入视野
                float a = Mathf.Clamp01(Mathf.SmoothStep(0.02f, 1f, t));
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    // 程序化生成均匀噪点贴图：亮度收窄到 [0.46, 0.54]，靠透明度控制强度，减轻「糊屏」感
    static Texture2D BuildNoiseTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        var rng = new System.Random(42);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float n = (float)rng.NextDouble();
                float g = Mathf.Lerp(0.46f, 0.54f, n);
                tex.SetPixel(x, y, new Color(g, g, g, 1f));
            }
        }
        tex.Apply();
        return tex;
    }

    // 根据血量算危险度（0=满血，1=快死），SmoothStep 让低血段效果加速变强
    void ApplyFromHealth(int current, int maxHp)
    {
        float danger = 0f;
        if (maxHp > 0)
            danger = 1f - Mathf.Clamp01((float)current / maxHp);

        float t = Mathf.SmoothStep(0f, 1f, danger);

        if (outputMode == HorrorOutputMode.ScreenSpaceUiOverlay)
        {
            ApplyUiOverlay(t);
            return;
        }

        if (_vignette == null || _grain == null || _colorGrading == null) return;

        _vignette.intensity.value = Mathf.Lerp(vignetteAtFullHp, vignetteAtLowHp, t);
        _grain.intensity.value = Mathf.Lerp(grainAtFullHp, grainAtLowHp, t);
        _colorGrading.temperature.value = Mathf.Lerp(temperatureAtFullHp, temperatureAtLowHp, t);
    }

    // 把危险度映射到三个 UI 叠层的透明度
    void ApplyUiOverlay(float dangerT)
    {
        if (_uiVignette == null || _uiGrain == null || _uiColdTint == null) return;

        float v = Mathf.Clamp01(Mathf.Lerp(vignetteAtFullHp, vignetteAtLowHp, dangerT) * uiVignetteStrengthMul);
        float g = Mathf.Lerp(grainAtFullHp, grainAtLowHp, dangerT);
        float tintA = Mathf.Lerp(coldTintAlphaAtFullHp, coldTintAlphaAtLowHp, dangerT);

        _uiVignette.color = new Color(1f, 1f, 1f, v);
        _uiGrain.color = new Color(1f, 1f, 1f, g);
        var c = _uiColdTint.color;
        c.a = tintA;
        _uiColdTint.color = c;
    }
}
