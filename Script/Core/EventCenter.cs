using System;
using System.Collections.Generic;

// 轻量全局事件中心：用于没有直接引用时的跨模块通知，和各组件上的 C# event 并存，各取所需。
// 使用流程：定义载荷类（class）→ Subscribe → 业务处 Publish → 监听方 OnDestroy 里必须 Unsubscribe。
// 不依赖第三方库，直接用泛型 Type 做 key，Delegate.Combine/Remove 管委托链。
public static class EventCenter
{
    static readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

    // 订阅某类型事件；同一委托可以多次 Subscribe（不推荐，会触发多次）
    public static void Subscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null)
            return;

        Type key = typeof(T);
        if (_handlers.TryGetValue(key, out Delegate existing))
            _handlers[key] = Delegate.Combine(existing, handler);
        else
            _handlers[key] = handler;
    }

    // 取消订阅；注意匿名 lambda 若没保存引用则无法移除，建议用具名方法
    public static void Unsubscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null)
            return;

        Type key = typeof(T);
        if (!_handlers.TryGetValue(key, out Delegate existing))
            return;

        Delegate result = Delegate.Remove(existing, handler);
        if (result == null)
            _handlers.Remove(key);
        else
            _handlers[key] = result;
    }

    // 发布事件；无监听者时直接 return，不产生任何副作用
    public static void Publish<T>(T payload) where T : class
    {
        if (payload == null)
            return;

        Type key = typeof(T);
        if (!_handlers.TryGetValue(key, out Delegate del))
            return;

        if (del is Action<T> action)
            action.Invoke(payload);
    }

    // 清空全部订阅，切场景或单测前调用，防止旧引用泄漏
    public static void ClearAll()
    {
        _handlers.Clear();
        Cleared?.Invoke();
    }

    // 与 ClearAll 配对：DontDestroyOnLoad 单例可在这里重新订阅
    public static event Action Cleared;
}
