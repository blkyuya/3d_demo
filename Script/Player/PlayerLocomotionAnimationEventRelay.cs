using UnityEngine;

// Animation Event 转发器：挂在 unitychan（含 Animator 的子物体）上。
// Starter Assets 的 Walk_N/Run_N 片段上的 AnimationEvent 只在挂 Animator 的物体上找方法，
// 父物体的 ThirdPersonController 收不到；本脚本把 OnFootstep/OnLand 转发给父级。
// 由 ThirdPersonController 在运行时自动 AddComponent，无需手动挂载。
public sealed class PlayerLocomotionAnimationEventRelay : MonoBehaviour
{
    StarterAssets.ThirdPersonController _tpc;

    // 缓存父级 ThirdPersonController
    void Awake()
    {
        _tpc = GetComponentInParent<StarterAssets.ThirdPersonController>();
    }

    // 脚步声事件，转发给 ThirdPersonController 处理
    public void OnFootstep(AnimationEvent animationEvent)
    {
        if (_tpc == null)
            _tpc = GetComponentInParent<StarterAssets.ThirdPersonController>();
        if (_tpc != null)
            _tpc.OnFootstep(animationEvent);
    }

    // 落地事件，转发给 ThirdPersonController 处理
    public void OnLand(AnimationEvent animationEvent)
    {
        if (_tpc == null)
            _tpc = GetComponentInParent<StarterAssets.ThirdPersonController>();
        if (_tpc != null)
            _tpc.OnLand(animationEvent);
    }
}
