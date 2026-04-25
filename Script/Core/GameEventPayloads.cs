// 与 EventCenter 配合使用的游戏事件载荷，均为引用类型（class），便于泛型分发。
// 新增事件时在此加独立类型，避免用 string 魔法值导致跨模块依赖难追踪。

// 成功开火一发（弹匣已扣减）；携带武器类别，音频模块可据此播放不同枪声
public sealed class ShotFiredEvent
{
    // 兼容旧代码，等同手枪开火
    public static readonly ShotFiredEvent Default = Pistol;
    public static readonly ShotFiredEvent Pistol = new ShotFiredEvent(ShotSoundCategory.Pistol);
    public static readonly ShotFiredEvent Shotgun = new ShotFiredEvent(ShotSoundCategory.Shotgun);

    public readonly ShotSoundCategory Category;

    ShotFiredEvent(ShotSoundCategory category) { Category = category; }
}

// 开火音效分类，与 AudioKeys 中键名对应
public enum ShotSoundCategory
{
    Pistol,
    Shotgun,
}

// 空枪扣扳机（弹匣为 0 时仍尝试射击触发）
public sealed class DryFireEvent
{
    public static readonly DryFireEvent Default = new DryFireEvent();
    DryFireEvent() { }
}

// 换弹协程刚开始时发布一次，动画层可订阅来播换弹动画
public sealed class ReloadStartedEvent
{
    public static readonly ReloadStartedEvent Default = new ReloadStartedEvent();
    ReloadStartedEvent() { }
}

// 玩家死亡，与 PlayerHealth.OnDied C# event 同时发布，两条通知路径覆盖有/无直接引用的场景
public sealed class PlayerDiedEvent
{
    public static readonly PlayerDiedEvent Default = new PlayerDiedEvent();
    PlayerDiedEvent() { }
}

// 玩家血量变化，携带当前值和最大值，UI 和氛围系统订阅
public sealed class PlayerHealthChangedEvent
{
    public int Current { get; }
    public int Max { get; }

    public PlayerHealthChangedEvent(int current, int max)
    {
        Current = current;
        Max = max;
    }
}
