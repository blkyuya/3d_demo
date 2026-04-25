using UnityEngine;
using UnityEngine.SceneManagement;

// Game Over UI：监听 GameStateManager 状态变化，进入 GameOver 状态时弹出面板、解锁鼠标，提供重开关卡按钮。
// 挂载：Popup 层 Canvas 下的 GameOver 面板节点。
public class GameOverUI : BasePanel
{
    [Header("引用")]
    [Tooltip("Game Over 面板根节点")]
    public GameObject gameOverPanel;

    // 初始化：把面板藏起来，订阅状态事件
    protected override void OnPanelInit()
    {
        if (gameOverPanel != null)
        {
            panelRoot = gameOverPanel;
            gameOverPanel.SetActive(false);
        }

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnGameStateChanged += OnGameStateChanged;
    }

    // 取消订阅
    void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnGameStateChanged -= OnGameStateChanged;
    }

    // 只响应 GameOver 状态，通过 UIManager 的弹窗栈显示
    void OnGameStateChanged(GameState state)
    {
        if (state != GameState.GameOver) return;

        if (UIManager.Instance != null)
            UIManager.Instance.PushPopup(this);
        else
            Show();
    }

    // 重开关卡：由 GameStateManager 统一恢复时间缩放与光标状态
    public void RestartScene()
    {
        if (GameStateManager.Instance != null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.PopPopup(this);
            GameStateManager.Instance.RestartCurrentScene();
            return;
        }

        // 没有 GameStateManager 时的兜底
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (UIManager.Instance != null)
            UIManager.Instance.PopPopup(this);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
