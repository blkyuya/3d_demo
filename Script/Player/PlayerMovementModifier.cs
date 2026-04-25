using UnityEngine;
using StarterAssets;

// 移动速度修改器：根据瞄准状态动态调整角色移动和冲刺速度。
// 瞄准时减速（战术移动），松手立即恢复正常速度；死亡时清零速度。
// 优先从 PlayerStateManager 读取 IsTacticalAiming，没有状态机时降级到直接读 PlayerAimState.IsAiming。
// 挂载：PlayerArmature，与 ThirdPersonController、PlayerAimState 同节点或父层级。
public class PlayerMovementModifier : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("瞄准状态来源")]
    public PlayerAimState aimState;

    [Tooltip("Starter Assets 角色控制器，修改 MoveSpeed / SprintSpeed")]
    public ThirdPersonController thirdPersonController;

    [Tooltip("死亡后停止位移")]
    public PlayerHealth playerHealth;

    [Header("状态机（可选）")]
    [Tooltip("赋值后优先从状态机读瞄准减速，否则直接读 aimState.IsAiming")]
    public PlayerStateManager playerStateManager;

    [Header("速度设置")]
    [Tooltip("非瞄准时的普通移动速度")]
    public float normalMoveSpeed = 5f;

    [Tooltip("瞄准时的减速移动速度")]
    public float aimMoveSpeed = 2.5f;

    [Tooltip("非瞄准时的冲刺速度")]
    public float normalSprintSpeed = 5.335f;

    [Tooltip("瞄准时的冲刺速度（通常与 aimMoveSpeed 相同，瞄准不允许冲刺）")]
    public float aimSprintSpeed = 2.5f;

    // 每帧根据瞄准状态和死亡状态写入 ThirdPersonController 的速度字段
    void Update()
    {
        if (!GameStateManager.IsGameplayPlaying)
            return;

        // 死亡时强制清零，防止 ThirdPersonController 在死亡后仍驱动位移
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (thirdPersonController != null)
            {
                thirdPersonController.MoveSpeed = 0f;
                thirdPersonController.SprintSpeed = 0f;
            }
            return;
        }

        HandleMovementSpeed();
    }

    // 状态机优先；无状态机时直接读瞄准输入；写对应速度参数
    void HandleMovementSpeed()
    {
        if (aimState == null || thirdPersonController == null)
            return;

        bool useAimSlow = playerStateManager != null
            ? playerStateManager.IsTacticalAiming
            : aimState.IsAiming;

        if (useAimSlow)
        {
            thirdPersonController.MoveSpeed = aimMoveSpeed;
            thirdPersonController.SprintSpeed = aimSprintSpeed;
        }
        else
        {
            thirdPersonController.MoveSpeed = normalMoveSpeed;
            thirdPersonController.SprintSpeed = normalSprintSpeed;
        }
    }
}
