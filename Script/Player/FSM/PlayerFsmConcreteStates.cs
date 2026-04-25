using UnityEngine;

// 探索状态：无特殊逻辑，占位便于后期扩展（如体力回复、环境音效切换）
public sealed class PlayerExploreFsmState : IPlayerState
{
    public void OnEnter(PlayerStateManager owner) { }
    public void OnUpdate(PlayerStateManager owner) { }
    public void OnExit(PlayerStateManager owner) { }
}

// 瞄准状态：实际逻辑由 PlayerAimState 驱动，这里只是 FSM 的钩子占位
public sealed class PlayerAimFsmState : IPlayerState
{
    public void OnEnter(PlayerStateManager owner) { }
    public void OnUpdate(PlayerStateManager owner) { }
    public void OnExit(PlayerStateManager owner) { }
}

// 换弹状态：与 HitscanShooter 协程同步，换弹期间其他模块读 IsReloading 做互斥
public sealed class PlayerReloadFsmState : IPlayerState
{
    public void OnEnter(PlayerStateManager owner) { }
    public void OnUpdate(PlayerStateManager owner) { }
    public void OnExit(PlayerStateManager owner) { }
}

// 交互/背包打开状态：光标已在 UIManager 侧解锁，射击在 HitscanShooter 侧禁止
public sealed class PlayerInteractFsmState : IPlayerState
{
    public void OnEnter(PlayerStateManager owner) { }
    public void OnUpdate(PlayerStateManager owner) { }
    public void OnExit(PlayerStateManager owner) { }
}

// 使用道具硬直状态（预留）：当前解析器不主动返回这个状态，留给后续扩展
public sealed class PlayerHealFsmState : IPlayerState
{
    public void OnEnter(PlayerStateManager owner) { }
    public void OnUpdate(PlayerStateManager owner) { }
    public void OnExit(PlayerStateManager owner) { }
}

// 死亡状态：OnEnter 里记一次 Log 方便排查；可在这里集中触发 GameOver UI 或通知
public sealed class PlayerDeadFsmState : IPlayerState
{
    private bool _logged;

    public void OnEnter(PlayerStateManager owner)
    {
        if (_logged) return;
        _logged = true;
        Debug.Log("[PlayerStateManager] 进入 Dead 状态。");
    }

    public void OnUpdate(PlayerStateManager owner) { }

    // 退出死亡状态时重置标记，防止复活逻辑扩展后 Log 被屏蔽
    public void OnExit(PlayerStateManager owner)
    {
        _logged = false;
    }
}
