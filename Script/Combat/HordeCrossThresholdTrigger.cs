using System.Collections;
using UnityEngine;

// 过阈值触发器：玩家进入触发范围且密码门已开时，延迟后启动 HordeWaveManager 刷尸潮，仅触发一次。
// 需要 Collider 并勾 IsTrigger，Reset 时自动设置。
// 挂载：密码门后方的不可见触发区域空物体。
[RequireComponent(typeof(Collider))]
public class HordeCrossThresholdTrigger : MonoBehaviour
{
    [Header("条件")]
    [Tooltip("场景中的密码门；门未开时不触发尸潮")]
    public PasswordDoorController passwordDoor;

    [Tooltip("要启动的尸潮管理器")]
    public HordeWaveManager waveManager;

    [Header("时机")]
    [Tooltip("跨过阈值后延迟多少秒开始刷怪")]
    public float delaySeconds = 1.5f;

    bool _fired;

    // Reset 时自动勾选 IsTrigger，防止忘了设置
    void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    // 检测玩家进入、密码门条件和尸潮管理器是否就绪，全部通过后启动延时协程
    void OnTriggerEnter(Collider other)
    {
        if (_fired || !GameStateManager.IsGameplayPlaying) return;
        if (!other.GetComponentInParent<PlayerInventory>()) return;
        if (passwordDoor != null && !passwordDoor.IsDoorOpened) return;
        if (waveManager == null) return;

        _fired = true;
        StartCoroutine(DelayedStart());
    }

    // 延迟触发，给玩家一点反应时间
    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(delaySeconds);
        if (waveManager != null)
            waveManager.BeginHorde();
    }
}
