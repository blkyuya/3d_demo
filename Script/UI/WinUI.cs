using UnityEngine;
using UnityEngine.SceneManagement;

public class WinUI : BasePanel
{
    [Header("References")]
    public GameObject winPanel;

    bool _hasWon;

    protected override void OnPanelInit()
    {
        if (winPanel != null)
        {
            panelRoot = winPanel;
            winPanel.SetActive(false);
        }
    }

    public void ShowWin()
    {
        if (_hasWon) return;
        _hasWon = true;

        Debug.Log("You Win!");

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Win);

        if (UIManager.Instance != null)
            UIManager.Instance.PushPopup(this);
        else
            Show();
    }

    public void RestartScene()
    {
        if (GameStateManager.Instance != null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.PopPopup(this);
            GameStateManager.Instance.RestartCurrentScene();
            return;
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (UIManager.Instance != null)
            UIManager.Instance.PopPopup(this);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
