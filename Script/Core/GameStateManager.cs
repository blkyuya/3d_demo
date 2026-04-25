using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 全局游戏流程管理：维护 Playing / Paused / GameOver / Win 四种状态，集中处理 timeScale、光标与暂停菜单。
// 死亡由 PlayerHealth.OnDied 切入 GameOver；胜利由 WinUI 调用 SetState(Win)。
// 用 DefaultExecutionOrder(-100) 保证它在普通脚本之前 Start，其他脚本读 IsGameplayPlaying 时状态已就绪。
// 挂载：场景内唯一空物体，与 UIManager 同级。
[DefaultExecutionOrder(-100)]
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    // 没有管理器或当前为 Playing 时视为允许操作，兼容未挂脚本的测试场景
    public static bool IsGameplayPlaying =>
        Instance == null ||
        (Instance.CurrentState == GameState.Playing && !SaveLoadPanelUI.IsOpenBlockingGameplay);

    [Header("引用（可空，运行时尽量自动查找）")]
    [Tooltip("用于订阅死亡 → GameOver")]
    [SerializeField]
    PlayerHealth playerHealth;

    [Tooltip("暂停面板（Esc）；留空则仅冻结时间与光标，无 UI")]
    [SerializeField]
    PauseMenuUI pauseMenuUI;

    [Tooltip("暂停时若背包仍打开则先关闭")]
    [SerializeField]
    PlayerInventoryBagUI inventoryBagUI;

#if ENABLE_INPUT_SYSTEM
    [Header("输入（新 Input System）")]
    [Tooltip("玩家身上的 PlayerInput；留空则在 Start 时自动查找")]
    [SerializeField]
    PlayerInput playerInput;
#endif

    StarterAssets.StarterAssetsInputs _starterInputs;

    public GameState CurrentState { get; private set; } = GameState.Playing;

    // 状态变化后触发一次，UI 层（WinPanel、GameOverPanel）订阅这里切换显示
    public event Action<GameState> OnGameStateChanged;

    bool _pauseMenuPushed;

    // 单例初始化，第二个实例直接销毁
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 补引用，订阅死亡事件，强制开局为游玩态并锁定光标
    void Start()
    {
        if (inventoryBagUI == null && UIManager.Instance != null)
            inventoryBagUI = UIManager.Instance.inventoryBagUI;
        if (pauseMenuUI == null)
            pauseMenuUI = FindObjectOfType<PauseMenuUI>();

        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.OnDied += HandlePlayerDied;

#if ENABLE_INPUT_SYSTEM
        if (playerInput == null)
            playerInput = FindObjectOfType<PlayerInput>();
        if (playerInput != null)
            _starterInputs = playerInput.GetComponent<StarterAssets.StarterAssetsInputs>();
#endif

        Time.timeScale = 1f;
        CurrentState = GameState.Playing;
        ApplyPlayerGameplayInput(true);
        // 确保开局鼠标锁定隐藏，防止编辑器进入游戏时鼠标漂出窗口
        ApplyGameplayCursor();
    }

    // 物体销毁时取消订阅，清空静态实例引用
    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnDied -= HandlePlayerDied;

        if (Instance == this)
            Instance = null;
    }

    // 处理 Esc 键暂停/恢复，存档界面优先于暂停菜单
    void Update()
    {
        if (CurrentState == GameState.GameOver || CurrentState == GameState.Win)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 存档界面开着时先处理它，不触发暂停
            if (SaveLoadPanelUI.Instance != null && SaveLoadPanelUI.Instance.IsOpenForInput())
            {
                SaveLoadPanelUI.Instance.OnEscapePressed();
                return;
            }

            if (CurrentState == GameState.Playing)
            {
                // 背包开着时 Esc 先关背包，不弹暂停菜单
                if (inventoryBagUI != null && inventoryBagUI.IsBagOpen)
                {
                    inventoryBagUI.SetBagOpen(false);
                    return;
                }
                SetState(GameState.Paused);
            }
            else if (CurrentState == GameState.Paused)
            {
                SetState(GameState.Playing);
            }
        }
    }

    // 收到死亡事件切到 GameOver
    void HandlePlayerDied()
    {
        SetState(GameState.GameOver);
    }

    // 切换全局状态：应用 timeScale、光标、暂停 UI，然后通知各系统
    public void SetState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        GameState previous = CurrentState;
        CurrentState = newState;

        switch (newState)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                ApplyGameplayCursor();
                if (previous == GameState.Paused)
                    ClosePauseMenuIfNeeded();
                break;

            case GameState.Paused:
                // 暂停前先关背包，不然背包 UI 和暂停 UI 叠在一起
                if (inventoryBagUI != null && inventoryBagUI.IsBagOpen)
                    inventoryBagUI.SetBagOpen(false);
                Time.timeScale = 0f;
                ApplyMenuCursor();
                OpenPauseMenuIfNeeded();
                break;

            case GameState.GameOver:
            case GameState.Win:
                if (previous == GameState.Paused)
                    ClosePauseMenuIfNeeded();
                if (inventoryBagUI != null && inventoryBagUI.IsBagOpen)
                    inventoryBagUI.SetBagOpen(false);
                Time.timeScale = 0f;
                ApplyMenuCursor();
                break;
        }

        ApplyPlayerGameplayInput(newState == GameState.Playing);
        OnGameStateChanged?.Invoke(newState);
    }

    // 游玩态外关闭 PlayerInput，避免 timeScale=0 时鼠标仍驱动第三人称视角
    // Starter Assets 对鼠标 delta 不用 timeScale 缩放，必须主动停
    void ApplyPlayerGameplayInput(bool gameplayActive)
    {
#if ENABLE_INPUT_SYSTEM
        if (playerInput != null)
        {
            if (gameplayActive)
                playerInput.ActivateInput();
            else
                playerInput.DeactivateInput();
        }
#endif
        // 直接清零缓存值，防止非游玩态下仍有残留输入
        if (_starterInputs != null && !gameplayActive)
        {
            _starterInputs.move = Vector2.zero;
            _starterInputs.look = Vector2.zero;
            _starterInputs.jump = false;
            _starterInputs.sprint = false;
        }
    }

    // 重载关卡：恢复时间和光标，清弹窗栈，清事件中心，加载场景
    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        ApplyPlayerGameplayInput(true);
        ApplyGameplayCursor();
        if (UIManager.Instance != null)
            UIManager.Instance.ClearPopupStack();
        _pauseMenuPushed = false;
        // 切场景前清空事件中心，避免静态 delegate 仍引用已销毁物体
        EventCenter.ClearAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // 打开暂停面板并压入 UI 栈，保证弹窗层级正确
    void OpenPauseMenuIfNeeded()
    {
        if (pauseMenuUI == null || UIManager.Instance == null)
            return;
        UIManager.Instance.PushPopup(pauseMenuUI);
        _pauseMenuPushed = true;
    }

    // 从 UI 栈弹出暂停面板
    void ClosePauseMenuIfNeeded()
    {
        if (!_pauseMenuPushed || pauseMenuUI == null || UIManager.Instance == null)
        {
            _pauseMenuPushed = false;
            return;
        }
        UIManager.Instance.PopPopup(pauseMenuUI);
        _pauseMenuPushed = false;
    }

    // 锁定并隐藏鼠标，游玩态专用
    static void ApplyGameplayCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 释放鼠标，菜单/暂停/GameOver/Win 时调用
    static void ApplyMenuCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

// 游戏全局流程状态枚举
public enum GameState
{
    Playing,
    Paused,
    GameOver,
    Win
}
