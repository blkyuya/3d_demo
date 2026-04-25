using UnityEngine;

// 数字键碰撞体标记：挂在密码键盘每个数字键的碰撞体上，让 PasswordDoorController 射线识别时能取到 digit 值。
[DisallowMultipleComponent]
public class PuzzleDigitKey : MonoBehaviour
{
    [Header("数字")]
    [Tooltip("0-9")]
    [Range(0, 9)]
    public int digit = 0;
}
