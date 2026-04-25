using UnityEngine;

// 暂停菜单面板：由 GameStateManager 在 Paused 状态时通过 UIManager.PushPopup 弹出。
// 提供「继续」「重开」「退出」三个按钮，对应 OnResumeClicked / OnRestartClicked / OnQuitClicked。
// 挂载：暂停面板 Canvas 节点，需与 GameStateManager、UIManager 在同场景。
public class PauseMenuUI : BasePanel
{
    [Header("面板根（留空则用本物体或第一个子物体）")]
    [SerializeField]
    GameObject pausePanelRoot;

    // 初始化找面板根节点并隐藏，避免关掉挂脚本的本体
    protected override void OnPanelInit()
    {
        if (pausePanelRoot != null)
            panelRoot = pausePanelRoot;
        else if (transform.childCount > 0)
            panelRoot = transform.GetChild(0).gameObject;
        else
            panelRoot = gameObject;

        // 单层结构时请用空父物体挂本脚本，否则关掉本体后脚本也失效
        if (panelRoot != null && panelRoot != gameObject)
            panelRoot.SetActive(false);
    }

    // 继续游戏按钮：切回 Playing 状态，GameStateManager 会恢复 timeScale 和光标
    public void OnResumeClicked()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Playing);
    }

    // 重开本关按钮：GameStateManager 统一处理 timeScale 重置和场景加载
    public void OnRestartClicked()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.RestartCurrentScene();
    }

    // 退出游戏按钮：编辑器下停止 Play Mode，构建后退出
    public void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
