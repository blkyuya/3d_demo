using TMPro;
using UnityEngine;

// 玩家状态变化反馈：订阅 PlayerStateManager.OnStateChanged，进入新状态时播放对应音效和刷新 UI 文案。
// 不修改状态机本身，只做展示层反馈，可选配置，全留空也不影响游戏逻辑。
// 挂载：PlayerArmature 或场景任意位置。
public class PlayerStateChangeFeedback : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空则在 Start 时 FindObjectOfType")]
    public PlayerStateManager stateManager;

    [Tooltip("指定则用该源播放；否则用 PlayClipAtPoint 挂在主相机附近")]
    public AudioSource audioSource;

    [Header("音效（进入该状态时播放，可空）")]
    public AudioClip enterExploreClip;
    public AudioClip enterAimClip;
    public AudioClip enterReloadClip;
    public AudioClip enterInteractClip;
    public AudioClip enterHealClip;
    public AudioClip enterDeadClip;

    [Range(0f, 1f)]
    public float soundVolume = 0.65f;

    [Header("UI（可选，仅调试用）")]
    [Tooltip("显示当前玩法状态中文名，留空则不显示")]
    public TextMeshProUGUI stateHintText;

    // 找到状态机，订阅事件，初始化一次 UI 显示
    void Start()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<PlayerStateManager>();

        if (stateManager == null)
        {
            Debug.LogWarning("PlayerStateChangeFeedback: 未找到 PlayerStateManager，本组件将无效。");
            return;
        }

        stateManager.OnStateChanged += OnPlayerStateChanged;
        ApplyUi(stateManager.CurrentState);
    }

    // 销毁时取消订阅
    void OnDestroy()
    {
        if (stateManager != null)
            stateManager.OnStateChanged -= OnPlayerStateChanged;
    }

    // 状态变化时刷新 UI 并播音效
    void OnPlayerStateChanged(PlayerState from, PlayerState to)
    {
        ApplyUi(to);
        PlayEnterClip(to);
    }

    // 把状态枚举转成中文更新文本组件
    void ApplyUi(PlayerState state)
    {
        if (stateHintText == null) return;
        stateHintText.text = "状态：" + GetStateDisplayName(state);
    }

    // 根据进入状态选对应音效并播放
    void PlayEnterClip(PlayerState state)
    {
        AudioClip clip = PickClip(state);
        if (clip == null) return;

        if (audioSource != null)
            audioSource.PlayOneShot(clip, soundVolume);
        else if (Camera.main != null)
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, soundVolume);
    }

    // 状态 → 音效映射
    AudioClip PickClip(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Explore:  return enterExploreClip;
            case PlayerState.Aim:      return enterAimClip;
            case PlayerState.Reload:   return enterReloadClip;
            case PlayerState.Interact: return enterInteractClip;
            case PlayerState.Heal:     return enterHealClip;
            case PlayerState.Dead:     return enterDeadClip;
            default:                   return null;
        }
    }

    // 状态枚举转中文名，供 UI 显示或日志使用
    public static string GetStateDisplayName(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Explore:  return "探索";
            case PlayerState.Aim:      return "瞄准";
            case PlayerState.Reload:   return "换弹";
            case PlayerState.Interact: return "交互";
            case PlayerState.Heal:     return "治疗";
            case PlayerState.Dead:     return "死亡";
            default:                   return state.ToString();
        }
    }
}
