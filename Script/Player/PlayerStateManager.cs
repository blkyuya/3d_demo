using System;
using UnityEngine;

// 玩家玩法状态机（镜像模式）：从现有组件同步权威状态，不删除 PlayerAimState / HitscanShooter 的逻辑。
// 在 LateUpdate 末尾解析，保证当帧内瞄准输入和换弹标志已经更新完毕，再决定新状态。
// 状态优先级（高→低）：Dead → Reload → Interact（背包打开）→ Aim → Explore
// Animator 状态机管骨骼和融合树；本类管玩法互斥和扩展钩子，两者分工不重叠。
// 挂载：PlayerArmature 根节点。
[DisallowMultipleComponent]
public class PlayerStateManager : MonoBehaviour
{
    [Header("权威数据源（镜像自这些组件）")]
    [Tooltip("用于判断 Dead 状态")]
    public PlayerHealth playerHealth;

    [Tooltip("用于判断 Reload 状态")]
    public HitscanShooter hitscanShooter;

    [Tooltip("用于判断 Aim 状态")]
    public PlayerAimState aimState;

    [Tooltip("用于判断 Interact 状态（背包打开）；可空")]
    public PlayerInventoryBagUI inventoryBagUI;

    // 当前玩法状态，外部脚本只读
    public PlayerState CurrentState { get; private set; }

    // 上一次状态，状态切换时保存，供监听方做差异处理
    public PlayerState PreviousState { get; private set; }

    // 是否处于战术瞄准（移动减速、肩射镜头）；背包开着时为 false
    public bool IsTacticalAiming => CurrentState == PlayerState.Aim;

    // 状态变化（旧, 新），供 UI / 音频等订阅
    public event Action<PlayerState, PlayerState> OnStateChanged;

    private IPlayerState _explore;
    private IPlayerState _aim;
    private IPlayerState _reload;
    private IPlayerState _interact;
    private IPlayerState _heal;
    private IPlayerState _dead;

    private bool _inited;

    // 补引用，实例化状态处理类，解析初始状态并 OnEnter
    void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (hitscanShooter == null)
            hitscanShooter = GetComponent<HitscanShooter>();
        if (aimState == null)
            aimState = GetComponent<PlayerAimState>();
        if (inventoryBagUI == null && UIManager.Instance != null)
            inventoryBagUI = UIManager.Instance.inventoryBagUI;
        if (inventoryBagUI == null)
            inventoryBagUI = FindObjectOfType<PlayerInventoryBagUI>();

        _explore = new PlayerExploreFsmState();
        _aim = new PlayerAimFsmState();
        _reload = new PlayerReloadFsmState();
        _interact = new PlayerInteractFsmState();
        _heal = new PlayerHealFsmState();
        _dead = new PlayerDeadFsmState();

        CurrentState = ResolveState();
        PreviousState = CurrentState;
        GetHandler(CurrentState)?.OnEnter(this);
        _inited = true;
    }

    // LateUpdate：所有输入组件已在 Update 里更新，这里解析状态切换
    void LateUpdate()
    {
        if (!_inited) return;

        PlayerState next = ResolveState();
        if (next != CurrentState)
        {
            GetHandler(CurrentState)?.OnExit(this);
            PreviousState = CurrentState;
            CurrentState = next;
            OnStateChanged?.Invoke(PreviousState, CurrentState);
            GetHandler(CurrentState)?.OnEnter(this);
        }

        GetHandler(CurrentState)?.OnUpdate(this);
    }

    // 判断是否处于指定状态，多条件时可扩展
    public bool IsInState(PlayerState state)
    {
        return CurrentState == state;
    }

    // 按优先级从高到低依次检查各条件，返回当前应处于的状态
    PlayerState ResolveState()
    {
        if (playerHealth != null && playerHealth.IsDead)
            return PlayerState.Dead;

        if (hitscanShooter != null && hitscanShooter.IsReloading)
            return PlayerState.Reload;

        if (inventoryBagUI != null && inventoryBagUI.IsBagOpen)
            return PlayerState.Interact;

        if (aimState != null && aimState.IsAiming)
            return PlayerState.Aim;

        return PlayerState.Explore;
    }

    // 根据状态枚举返回对应的处理类实例
    IPlayerState GetHandler(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Explore:  return _explore;
            case PlayerState.Aim:      return _aim;
            case PlayerState.Reload:   return _reload;
            case PlayerState.Interact: return _interact;
            case PlayerState.Heal:     return _heal;
            case PlayerState.Dead:     return _dead;
            default:                   return _explore;
        }
    }
}
