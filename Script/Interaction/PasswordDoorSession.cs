using StarterAssets;
using UnityEngine;

// 密码门输入会话：开始输入时解锁鼠标并关闭 StarterAssets 的视角旋转，避免点数字键时镜头乱转。
// 静态工具类，PasswordDoorController 调用 Begin/End，不需要挂载。
public static class PasswordDoorSession
{
    public static bool IsActive { get; private set; }

    static StarterAssetsInputs _inputs;
    static bool _savedLook;
    static CursorLockMode _savedLock;
    static bool _savedVisible;

    // 开始密码输入模式：保存当前鼠标和视角状态，然后解锁鼠标、禁用视角输入
    public static void Begin()
    {
        if (IsActive) return;
        IsActive = true;

        if (_inputs == null)
            _inputs = Object.FindObjectOfType<StarterAssetsInputs>();
        if (_inputs != null)
        {
            _savedLook = _inputs.cursorInputForLook;
            _inputs.cursorInputForLook = false;
        }

        _savedLock = Cursor.lockState;
        _savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 结束密码输入模式：恢复保存的鼠标状态和视角输入
    public static void End()
    {
        if (!IsActive) return;
        IsActive = false;

        if (_inputs != null)
            _inputs.cursorInputForLook = _savedLook;
        Cursor.lockState = _savedLock;
        Cursor.visible = _savedVisible;
    }
}
