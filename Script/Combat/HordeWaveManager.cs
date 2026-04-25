using System;
using System.Collections;
using UnityEngine;

// 尸潮管理：被触发器（HordeTriggerZone）激活后，分批生成僵尸并追踪存活数。
// 全部僵尸死亡后 IsCleared 变为 true，ExitZone 读这个决定是否允许玩家通关。
// 生成策略：场上存活数超过 maxAlive 时暂停补刷，低于阈值才继续，防止同时刷出太多怪卡死帧率。
// 挂载：场景中空物体，与生成点 Transform 配合使用。
public class HordeWaveManager : MonoBehaviour
{
    [Header("生成")]
    [Tooltip("僵尸预制体，需要带 ZombieHealth 组件")]
    public GameObject zombiePrefab;

    [Tooltip("生成点列表，为空则用本物体位置")]
    public Transform[] spawnPoints;

    [Tooltip("本波总生成数量")]
    public int totalCount = 8;

    [Tooltip("场上同时存活上限，超过这个数量时暂停补刷")]
    public int maxAlive = 4;

    [Tooltip("每次补刷间隔（秒）")]
    public float spawnInterval = 1.2f;

    int _spawned;
    int _alive;
    bool _started;
    bool _cleared;

    // ExitZone 读这个决定是否允许玩家通关
    public bool IsCleared => _cleared;

    // 外部（如 HordeTriggerZone）判断是否已激活，防止重复触发
    public bool HasStarted => _started;

    // 尸潮全清时触发，可以挂音效或特效
    public event Action OnHordeCleared;

    // 由阈值触发器（HordeTriggerZone）调用，启动分批生成协程
    public void BeginHorde()
    {
        if (_started || _cleared || zombiePrefab == null) return;
        _started = true;
        if (totalCount <= 0)
        {
            Complete();
            return;
        }
        StartCoroutine(SpawnLoop());
    }

    // 分批刷怪主循环：场上存活数超限时等待，否则补刷一只，全部生成完毕后等待最后几只死亡
    IEnumerator SpawnLoop()
    {
        int cap = Mathf.Max(1, maxAlive);
        while (_spawned < totalCount)
        {
            // 存活数达到上限，每帧检测直到有怪死亡
            while (_alive >= cap && _spawned < totalCount)
                yield return null;

            if (_spawned >= totalCount) break;

            SpawnOne();
            yield return new WaitForSeconds(spawnInterval);
        }

        // 生成完毕，等最后几只死亡
        while (_alive > 0)
            yield return null;

        Complete();
    }

    // 轮询生成点生成一只僵尸，绑定死亡回调
    void SpawnOne()
    {
        Vector3 pos = transform.position;
        Quaternion rot = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // 循环复用生成点：第 N 只用第 N % 生成点数量 号位置
            Transform sp = spawnPoints[_spawned % spawnPoints.Length];
            if (sp != null)
            {
                pos = sp.position;
                rot = sp.rotation;
            }
        }

        GameObject go = Instantiate(zombiePrefab, pos, rot);
        _spawned++;
        _alive++;

        // 绑定死亡回调以计数；找不到 ZombieHealth 则直接减掉这只的存活计数，防止计数卡死
        var zh = go.GetComponent<ZombieHealth>();
        if (zh == null)
            zh = go.GetComponentInChildren<ZombieHealth>();
        if (zh != null)
            BindDeath(zh);
        else
        {
            _alive--;
            Debug.LogWarning("HordeWaveManager: 预制体上未找到 ZombieHealth，无法计数死亡。");
        }
    }

    // 用 lambda 捕获对应僵尸的引用，死亡后自动解绑，避免 handler 一直挂着
    void BindDeath(ZombieHealth zh)
    {
        Action handler = null;
        handler = () =>
        {
            zh.OnDied -= handler;
            _alive = Mathf.Max(0, _alive - 1);
            // 生成完毕且存活归零时立即判定完成，不用等协程轮询
            if (_started && _spawned >= totalCount && _alive <= 0 && !_cleared)
                Complete();
        };
        zh.OnDied += handler;
    }

    // 标记尸潮完成，触发事件，通知玩家可以撤离
    void Complete()
    {
        if (_cleared) return;
        _cleared = true;
        OnHordeCleared?.Invoke();
        PickupNotificationHub.Publish("威胁已清除，可以撤离");
    }
}
