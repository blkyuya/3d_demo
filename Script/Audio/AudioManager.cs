using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// 单例音频管理器：维护音效字典、2D/3D 播放、BGM 控制；AudioSource 走对象池复用，避免频繁创建销毁。
// 订阅 EventCenter 的开火/换弹/空枪事件；EventCenter.ClearAll 后通过 Cleared 重新订阅。
// 挂载：首场景任意空物体，运行后 DontDestroyOnLoad 跨场景存活；需拖入 AudioCatalogSO。
[DefaultExecutionOrder(-120)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("目录")]
    [SerializeField]
    AudioCatalogSO catalog;

    [Header("Mixer 分组（可选，无则走默认输出）")]
    [SerializeField] AudioMixerGroup mixerGroupSfx;
    [SerializeField] AudioMixerGroup mixerGroupUi;
    [SerializeField] AudioMixerGroup mixerGroupBgm;

    [Header("池")]
    [SerializeField, Min(1)]
    int sfxPrewarm = 12;

    [Header("3D 衰减")]
    [SerializeField] float spatialMinDistance = 1f;
    [SerializeField] float spatialMaxDistance = 45f;

    SimpleObjectPool<PooledOneShotAudio> _sfxPool;
    AudioSource _bgm;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 同一 key 只警告一次，避免刷屏
    static readonly HashSet<string> MissingKeyWarned = new HashSet<string>();
#endif

    // 单例初始化，构建音效池和 BGM AudioSource，DontDestroyOnLoad 跨场景
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (catalog != null)
            catalog.RebuildLookup();

        // 模板 GameObject 先停用，防止激活时就开始播放
        var templateGo = new GameObject("PooledOneShotTemplate");
        templateGo.transform.SetParent(transform, false);
        templateGo.SetActive(false);
        templateGo.AddComponent<PooledOneShotAudio>();
        var templateComp = templateGo.GetComponent<PooledOneShotAudio>();
        _sfxPool = new SimpleObjectPool<PooledOneShotAudio>(templateComp, transform, sfxPrewarm);

        // BGM 独立 AudioSource，loop=true，spatialBlend=0（2D 全局背景音）
        var bgmGo = new GameObject("BgmSource");
        bgmGo.transform.SetParent(transform, false);
        _bgm = bgmGo.AddComponent<AudioSource>();
        _bgm.playOnAwake = false;
        _bgm.loop = true;
        _bgm.spatialBlend = 0f;
        if (mixerGroupBgm != null)
            _bgm.outputAudioMixerGroup = mixerGroupBgm;
    }

    // 激活时订阅 EventCenter 事件，并监听 Cleared 用于场景切换后重新订阅
    void OnEnable()
    {
        SubscribeEvents();
        EventCenter.Cleared += OnEventCenterCleared;
    }

    // 停用时取消所有订阅，防止物体被禁用后仍收到事件
    void OnDisable()
    {
        EventCenter.Cleared -= OnEventCenterCleared;
        UnsubscribeEvents();
    }

    // 销毁时清空静态实例引用
    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // EventCenter.ClearAll 后重新订阅，DontDestroyOnLoad 的单例需要这个机制
    void OnEventCenterCleared()
    {
        UnsubscribeEvents();
        SubscribeEvents();
    }

    // 订阅开火、换弹、空枪三个事件
    void SubscribeEvents()
    {
        EventCenter.Subscribe<ShotFiredEvent>(OnShotFired);
        EventCenter.Subscribe<ReloadStartedEvent>(OnReloadStarted);
        EventCenter.Subscribe<DryFireEvent>(OnDryFire);
    }

    // 取消订阅，与 SubscribeEvents 成对
    void UnsubscribeEvents()
    {
        EventCenter.Unsubscribe<ShotFiredEvent>(OnShotFired);
        EventCenter.Unsubscribe<ReloadStartedEvent>(OnReloadStarted);
        EventCenter.Unsubscribe<DryFireEvent>(OnDryFire);
    }

    // 开火事件：手枪和霰弹音效不同，在听众位置播放 2D
    void OnShotFired(ShotFiredEvent e)
    {
        Vector3 pos = GetListenerPosition();
        if (e.Category == ShotSoundCategory.Shotgun)
            PlaySfx2DAtPosition(AudioKeys.ShotgunBlast, pos, 1f);
        else
            PlaySfx2DAtPosition(AudioKeys.PistolShot, pos, 1f);
    }

    // 换弹事件：播换弹音效
    void OnReloadStarted(ReloadStartedEvent e)
    {
        PlaySfx2D(AudioKeys.GunReload);
    }

    // 空枪事件：播空枪咔嗒声
    void OnDryFire(DryFireEvent e)
    {
        PlaySfx2D(AudioKeys.GunEmpty);
    }

    // 听众位置取 Camera.main，没有摄像机时用原点
    static Vector3 GetListenerPosition()
    {
        if (Camera.main != null)
            return Camera.main.transform.position;
        return Vector3.zero;
    }

    // 按 key 从目录查 AudioClip，找不到时尝试别名，仍找不到打一次 Warning
    AudioClip ResolveClip(string key)
    {
        if (catalog == null) return null;

        if (catalog.TryGet(key, out AudioClip clip)) return clip;

        // 代码常量与素材命名习惯不一致时的别名候选
        string[] extras = GetExtraAliasKeys(key);
        if (extras != null)
        {
            for (int i = 0; i < extras.Length; i++)
                if (catalog.TryGet(extras[i], out clip)) return clip;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (MissingKeyWarned.Add(key))
            Debug.LogWarning(
                "[AudioManager] 目录里找不到 key「" + key + "」。请添加条目，或与素材名对齐（已支持空格/下划线/大小写）。", this);
#endif
        return null;
    }

    // AudioKeys 常量对应的「常见资源文件名」别名，处理文件名大小写/下划线差异
    static string[] GetExtraAliasKeys(string key)
    {
        if (key == AudioKeys.GunEmpty)
            return new[] { "gun empty click", "gun_empty_click" };
        if (key == AudioKeys.BgmDarkAmbience)
            return new[] { "dark ambient", "dark ambie", "dark_ambient" };
        if (key == AudioKeys.BgmHorrorAmbience)
            return new[] { "horror ambient", "horror ambie", "horror_ambient" };
        return null;
    }

    // 在听众附近播放 2D 化音效（spatialBlend=0）
    public void PlaySfx2D(string key, float volumeScale = 1f)
    {
        PlaySfx2DAtPosition(key, GetListenerPosition(), volumeScale);
    }

    // 在世界空间指定位置播放 2D 音效（spatialBlend=0，不衰减，用于枪声等）
    public void PlaySfx2DAtPosition(string key, Vector3 worldPosition, float volumeScale = 1f)
    {
        AudioClip clip = ResolveClip(key);
        if (clip == null || _sfxPool == null) return;

        PooledOneShotAudio p = _sfxPool.Get(worldPosition, Quaternion.identity);
        AudioSource src = p.Source;
        src.clip = clip;
        src.volume = Mathf.Clamp01(volumeScale);
        src.spatialBlend = 0f;
        src.spatialize = false;
        src.minDistance = spatialMinDistance;
        src.maxDistance = spatialMaxDistance;
        src.outputAudioMixerGroup = mixerGroupSfx;
        src.Play();
        StartCoroutine(ReleasePooledWhenDone(p, clip.length));
    }

    // 世界空间 3D 音效（spatialBlend=1，随距离衰减，用于僵尸叫声等）
    public void PlaySfx3D(string key, Vector3 worldPosition, float volumeScale = 1f)
    {
        AudioClip clip = ResolveClip(key);
        if (clip == null || _sfxPool == null) return;

        PooledOneShotAudio p = _sfxPool.Get(worldPosition, Quaternion.identity);
        AudioSource src = p.Source;
        src.clip = clip;
        src.volume = Mathf.Clamp01(volumeScale);
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = spatialMinDistance;
        src.maxDistance = spatialMaxDistance;
        src.spatialize = true;
        src.outputAudioMixerGroup = mixerGroupSfx;
        src.Play();
        StartCoroutine(ReleasePooledWhenDone(p, clip.length));
    }

    // UI 音效，优先走 UI Mixer 分组
    public void PlayUi(string key, float volumeScale = 1f)
    {
        AudioClip clip = ResolveClip(key);
        if (clip == null || _sfxPool == null) return;

        PooledOneShotAudio p = _sfxPool.Get(GetListenerPosition(), Quaternion.identity);
        AudioSource src = p.Source;
        src.clip = clip;
        src.volume = Mathf.Clamp01(volumeScale);
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = mixerGroupUi != null ? mixerGroupUi : mixerGroupSfx;
        src.Play();
        StartCoroutine(ReleasePooledWhenDone(p, clip.length));
    }

    // 等音效播完后把 PooledOneShotAudio 实例回收到池；用 WaitForSecondsRealtime 保证暂停时也能回收
    IEnumerator ReleasePooledWhenDone(PooledOneShotAudio player, float clipLength)
    {
        float t = Mathf.Max(0.02f, clipLength);
        yield return new WaitForSecondsRealtime(t);
        if (player != null && _sfxPool != null)
            _sfxPool.Release(player);
    }

    // 播放 BGM，循环；重复调用会直接切换曲目
    public void PlayBgm(string key, float volumeScale = 1f)
    {
        AudioClip clip = ResolveClip(key);
        if (clip == null || _bgm == null) return;

        _bgm.clip = clip;
        _bgm.volume = Mathf.Clamp01(volumeScale);
        if (mixerGroupBgm != null)
            _bgm.outputAudioMixerGroup = mixerGroupBgm;
        _bgm.Play();
    }

    // 停止 BGM
    public void StopBgm()
    {
        if (_bgm != null) _bgm.Stop();
    }
}
