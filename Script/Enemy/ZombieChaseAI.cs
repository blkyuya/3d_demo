using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 僵尸追击 AI（第一版，基础实现）：检测玩家进入追击范围后调用 NavMeshAgent.SetDestination 追击，
// 靠近攻击距离后原地停止。
// 若同物体上挂载了 ZombieStateMachine，则本脚本在 Update 中完全退让，由 FSM 接管，避免双重控制 Agent。
// 挂载：僵尸预制体根节点，与 NavMeshAgent 同级。
[RequireComponent(typeof(NavMeshAgent))]
public class ZombieChaseAI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家目标，一般拖 PlayerArmature；留空则 Start 时按 Tag 查找")]
    public Transform playerTarget;

    [Tooltip("可选：僵尸可见模型根节点（用于朝向旋转）")]
    public Transform modelRoot;

    [Tooltip("僵尸攻击组件，用于读取攻击距离")]
    public ZombieAttack zombieAttack;

    [Header("感知")]
    [Tooltip("开始追击玩家的检测距离")]
    public float detectionRange = 12f;

    [Header("移动")]
    [Tooltip("追击速度")]
    public float moveSpeed = 2.2f;

    [Tooltip("转向速度")]
    public float angularSpeed = 360f;

    [Header("NavMesh 校正")]
    [Tooltip("开局将 Agent 吸附到可走网格表面，修正脚底不在 NavMesh 上导致的陷地问题")]
    [SerializeField]
    private bool snapToNavMeshOnStart = true;

    [Tooltip("从当前位置向上取采样起点的高度（米）")]
    [SerializeField]
    private float navMeshSampleUp = 3f;

    [Tooltip("水平搜索可走点的最大半径（米）")]
    [SerializeField]
    private float navMeshSampleMaxDistance = 8f;

    private NavMeshAgent agent;

    // 死亡后停止追击，避免依赖组件禁用顺序
    private ZombieHealth _zombieHealth;

    // 若同一对象挂了 ZombieStateMachine，本脚本 Update 逻辑完全退让
    private ZombieStateMachine _stateMachine;

    // Awake 缓存引用，早于 Start
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        _zombieHealth = GetComponent<ZombieHealth>();
        _stateMachine = GetComponent<ZombieStateMachine>();
    }

    // 协程 Start：等一帧让 NavMeshAgent 完成注册后再执行吸附，避免首帧报错
    IEnumerator Start()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.angularSpeed = angularSpeed;
        }

        if (playerTarget == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                playerTarget = player.transform;
        }

        yield return null;
        if (snapToNavMeshOnStart)
            TrySnapToNavMeshSurface();
    }

    // 将本体 Warp 到附近 NavMesh 合法点，修正 Y 坐标与可走区域不一致的情况
    // 烘焙时模型和地面有偏差会导致僵尸陷地，这里用 SamplePosition 找最近可走点
    void TrySnapToNavMeshSurface()
    {
        if (agent == null) return;

        Vector3 p = transform.position;
        if (!NavMesh.SamplePosition(p + Vector3.up * navMeshSampleUp, out NavMeshHit hit,
                navMeshSampleMaxDistance, NavMesh.AllAreas))
            return;

        float dy = Mathf.Abs(hit.position.y - p.y);
        if (dy > 0.01f || !agent.isOnNavMesh)
            agent.Warp(hit.position);
    }

    // ZombieStateMachine 存在时完全退让；否则按距离驱动 Agent
    void Update()
    {
        if (_stateMachine != null) return;
        if (_zombieHealth != null && _zombieHealth.IsDead) return;
        if (playerTarget == null || agent == null) return;

        HandleChase();
    }

    // 在检测范围内追击；进入攻击距离后停止，让 ZombieAttack 接管
    void HandleChase()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float stopDistance = zombieAttack != null ? zombieAttack.attackRange : 1.8f;

        if (distanceToPlayer <= detectionRange)
        {
            if (distanceToPlayer > stopDistance)
            {
                agent.isStopped = false;
                agent.stoppingDistance = stopDistance;
                agent.SetDestination(playerTarget.position);
            }
            else
            {
                agent.isStopped = true;
            }
        }
        else
        {
            agent.isStopped = true;
        }
    }

    // 朝向玩家 Slerp 旋转，供没有 FSM 时使用（FSM 模式下由 ZombieAttackState.FacePlayer 处理）
    void HandleFacing()
    {
        if (playerTarget == null) return;

        Transform targetToRotate = modelRoot != null ? modelRoot : transform;
        Vector3 direction = playerTarget.position - targetToRotate.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        targetToRotate.rotation = Quaternion.Slerp(
            targetToRotate.rotation, targetRotation, 8f * Time.deltaTime);
    }

    // Scene 视图绘制检测范围，方便与 ZombieStateMachine 的范围对比
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
