using UnityEngine;

// 玩家瞄准状态：持续检测鼠标右键，维护 IsAiming 属性并同步准星显示。
// 不直接处理移动减速或镜头切换；PlayerCameraController 和 PlayerMovementModifier 各自读这里的 IsAiming 来响应。
// 这样瞄准状态只有一个来源，避免多处判断 Input.GetMouseButton(1) 导致逻辑不一致。
// 挂载：PlayerArmature 根节点，与 HitscanShooter 同物体。
public class PlayerAimState : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("瞄准时显示的屏幕准星 UI（可以是 Image 的父 GameObject）")]
    public GameObject crosshair;

    [Tooltip("死亡后强制退出瞄准；留空则自动查找")]
    public PlayerHealth playerHealth;

    // 当前帧是否处于右键瞄准状态，供外部脚本读取
    public bool IsAiming { get; private set; }

    // 初始帧同步一次准星显示，防止 Start 前准星处于错误状态
    void Start()
    {
        UpdateCrosshair();
    }

    // 暂停/结算时强制退出瞄准避免准星残留，正常游玩时检测右键
    void Update()
    {
        if (!GameStateManager.IsGameplayPlaying)
        {
            IsAiming = false;
            UpdateCrosshair();
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            IsAiming = false;
            UpdateCrosshair();
            return;
        }

        HandleAimInput();
        UpdateCrosshair();
    }

    // 读鼠标右键状态，每帧刷新
    void HandleAimInput()
    {
        IsAiming = Input.GetMouseButton(1);
    }

    // IsAiming 变化时同步准星 GameObject 的激活状态
    void UpdateCrosshair()
    {
        if (crosshair != null)
            crosshair.SetActive(IsAiming);
    }
}
