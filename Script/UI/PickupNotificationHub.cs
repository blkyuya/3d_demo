using System;

// 拾取物通知（观察者模式）：发布简短文案，由 PickupToastUI 等订阅者自行展示。
// 静态工具类，无需挂载，任何地方调用 Publish 都能触发 UI 飘字。
public static class PickupNotificationHub
{
    // 拾取成功等提示文案，PickupToastUI 订阅这里
    public static event Action<string> OnMessage;

    // 发布一条拾取提示；同时保证 PickupToastUI 实例存在
    public static void Publish(string message)
    {
        PickupToastUI.EnsureExists();
        OnMessage?.Invoke(message);
    }
}
