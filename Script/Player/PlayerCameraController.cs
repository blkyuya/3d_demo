using UnityEngine;
using Cinemachine;

// 第三人称镜头参数控制：瞄准时切到肩射视角（更近距离、更小 FOV），松开右键平滑还原普通视角。
// 只控制 Cinemachine 虚拟相机参数，不移动 Main Camera；渲染由 CinemachineBrain 同步到物理相机。
// 挂载：PlayerArmature，与 PlayerAimState 同节点；virtualCamera 拖入场景中的 PlayerFollowCamera。
public class PlayerCameraController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("瞄准状态来源")]
    public PlayerAimState aimState;

    [Tooltip("死亡后强制退出瞄准镜头")]
    public PlayerHealth playerHealth;

    [Header("状态机（可选）")]
    [Tooltip("赋值后优先从状态机读战术瞄准，否则直接读 aimState.IsAiming")]
    public PlayerStateManager playerStateManager;

    [Tooltip("Cinemachine 跟随相机（PlayerFollowCamera）")]
    public CinemachineVirtualCamera virtualCamera;

    [Tooltip("普通探索视角参考空物体（localPosition 即肩膀偏移）")]
    public Transform normalCameraPoint;

    [Tooltip("瞄准肩射视角参考空物体")]
    public Transform aimCameraPoint;

    [Header("镜头参数")]
    [Tooltip("普通状态镜头距离")]
    public float normalCameraDistance = 2.2f;

    [Tooltip("瞄准状态镜头距离，比普通近一些形成拉近感")]
    public float aimCameraDistance = 1.65f;

    [Tooltip("普通状态视野角（FOV）")]
    public float normalFOV = 50f;

    [Tooltip("瞄准状态视野角，略小产生拉近感")]
    public float aimFOV = 37f;

    [Tooltip("镜头参数插值速度，越大切换越快")]
    public float cameraLerpSpeed = 10f;

    private Cinemachine3rdPersonFollow thirdPersonFollow;

    // 缓存 3rdPersonFollow 组件，开局立即设置普通视角避免首帧参数错误
    void Start()
    {
        if (virtualCamera == null)
        {
            Debug.LogWarning("PlayerCameraController: virtualCamera 未赋值。", this);
            return;
        }

        thirdPersonFollow = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();

        if (thirdPersonFollow == null)
        {
            Debug.LogWarning("PlayerCameraController: 未在虚拟相机上找到 Cinemachine3rdPersonFollow。", this);
            return;
        }

        ApplyInstantCameraState(false);
    }

    // 每帧根据瞄准状态平滑切换镜头参数
    void Update()
    {
        bool isDead = playerHealth != null && playerHealth.IsDead;
        bool isAiming;
        if (isDead)
            isAiming = false;
        else if (playerStateManager != null)
            isAiming = playerStateManager.IsTacticalAiming;
        else
            isAiming = aimState != null && aimState.IsAiming;

        UpdateCameraState(isAiming);
    }

    // 用 Lerp 平滑过渡距离、肩部偏移和 FOV，避免切换时镜头跳变
    // 肩部偏移来自两个参考空物体的本地位置，Inspector 可视化调整无需改代码
    void UpdateCameraState(bool isAiming)
    {
        if (virtualCamera == null || thirdPersonFollow == null)
            return;

        float targetDistance = isAiming ? aimCameraDistance : normalCameraDistance;
        float targetFOV = isAiming ? aimFOV : normalFOV;

        Vector3 targetShoulderOffset = Vector3.zero;
        if (isAiming && aimCameraPoint != null)
            targetShoulderOffset = aimCameraPoint.localPosition;
        else if (!isAiming && normalCameraPoint != null)
            targetShoulderOffset = normalCameraPoint.localPosition;

        thirdPersonFollow.CameraDistance = Mathf.Lerp(
            thirdPersonFollow.CameraDistance, targetDistance, cameraLerpSpeed * Time.deltaTime);

        thirdPersonFollow.ShoulderOffset = Vector3.Lerp(
            thirdPersonFollow.ShoulderOffset, targetShoulderOffset, cameraLerpSpeed * Time.deltaTime);

        virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(
            virtualCamera.m_Lens.FieldOfView, targetFOV, cameraLerpSpeed * Time.deltaTime);
    }

    // 跳过插值直接应用目标参数，用于初始化或特殊场景（如复活时需要瞬间还原）
    void ApplyInstantCameraState(bool isAiming)
    {
        if (virtualCamera == null || thirdPersonFollow == null)
            return;

        virtualCamera.m_Lens.FieldOfView = isAiming ? aimFOV : normalFOV;
        thirdPersonFollow.CameraDistance = isAiming ? aimCameraDistance : normalCameraDistance;

        if (isAiming && aimCameraPoint != null)
            thirdPersonFollow.ShoulderOffset = aimCameraPoint.localPosition;
        else if (!isAiming && normalCameraPoint != null)
            thirdPersonFollow.ShoulderOffset = normalCameraPoint.localPosition;
    }
}
