using UnityEngine;
using UnityEngine.UI;

// 数字键盘按钮标记组件：挂在每个按钮上，用 actionId 区分行为。
// PasswordKeypadUI.Awake 遍历子物体时自动绑定 Button.onClick，不用手动拖。
// actionId：0-9 为数字，10 为确定，11 为取消。
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class PasswordKeypadButtonBinder : MonoBehaviour
{
    [Tooltip("0-9：数字键；10：确定；11：取消")]
    public int actionId;
}
