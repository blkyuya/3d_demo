// 玩家状态机单状态接口：三个钩子分别对应进入、每帧更新、退出。
// 首版各钩子可以是空实现，后续可在 OnEnter 里播 UI、音效等，不影响状态机骨架。
public interface IPlayerState
{
    void OnEnter(PlayerStateManager owner);
    void OnUpdate(PlayerStateManager owner);
    void OnExit(PlayerStateManager owner);
}
