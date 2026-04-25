using UnityEngine;

// 瞄准转向控制：瞄准时将玩家根物体旋转到与相机水平朝向一致，实现越肩射击时角色面朝镜头方向。
// 非瞄准时由 ThirdPersonController 自行处理转向，本脚本不干预。
// 去掉 Y 分量后再 LookRotation，防止角色随相机仰角倾斜。
// 挂载：PlayerArmature，与 PlayerAimState / PlayerHealth 同节点。
public class PlayerRotationController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("瞄准状态来源")]
    public PlayerAimState aimState;

    [Tooltip("提供水平朝向的相机（一般是 Main Camera）")]
    public Camera mainCamera;

    [Tooltip("需要旋转的角色根 Transform（PlayerArmature）")]
    public Transform playerRoot;

    [Tooltip("死亡后停止转向")]
    public PlayerHealth playerHealth;

    [Header("状态机（可选）")]
    [Tooltip("赋值后优先从状态机读战术瞄准，否则直接读 aimState.IsAiming")]
    public PlayerStateManager playerStateManager;

    [Header("转向参数")]
    [Tooltip("Slerp 速度，越大转向越灵敏")]
    public float aimRotateSpeed = 12f;

    // 每帧检测死亡和游戏状态，再执行转向
    void Update()
    {
        if (!GameStateManager.IsGameplayPlaying) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        HandleCharacterAimRotation();
    }

    // 瞄准时读相机水平前方向，Slerp 让角色平滑转向而不是瞬间跳转
    void HandleCharacterAimRotation()
    {
        bool aiming = playerStateManager != null
            ? playerStateManager.IsTacticalAiming
            : aimState != null && aimState.IsAiming;

        if (!aiming || playerRoot == null || mainCamera == null) return;

        // 去掉 Y 分量，防止角色随相机仰角前倾后仰
        Vector3 cameraForward = mainCamera.transform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        if (cameraForward.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
        playerRoot.rotation = Quaternion.Slerp(
            playerRoot.rotation, targetRotation, aimRotateSpeed * Time.deltaTime);
    }
}
