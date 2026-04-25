// 僵尸 FSM 状态接口：StateMachine 只依赖这个接口，不关心具体状态的实现细节。
// OnEnter：进入该状态时调用一次，做初始化（如停止 Agent、重置计时器）。
// OnUpdate：每帧由 ZombieStateMachine.Update 驱动，处理状态逻辑和转移条件。
// OnExit：离开该状态时调用一次，做清理。
public interface IZombieState
{
    void OnEnter(ZombieStateMachine machine);
    void OnUpdate(ZombieStateMachine machine);
    void OnExit(ZombieStateMachine machine);
}
