using UnityEngine;

// 存档检查点：玩家进入触发范围后按 E 打开存档 UI（SaveLoadPanelUI）。
// 需要 Collider 并勾 IsTrigger；按键和提示文本可在 Inspector 自定义。
// 挂载：场景中打字机或存档点对象。
public class CheckpointStation : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("场景中的 SaveLoadPanelUI；留空则运行时查找")]
    public SaveLoadPanelUI saveLoadPanel;

    [Tooltip("交互提示；留空则用 UIManager")]
    public InteractionPromptUI promptUI;

    [Header("交互")]
    public KeyCode interactKey = KeyCode.E;

    bool _playerInside;

    // 补全引用，优先从 UIManager 拿
    void Start()
    {
        if (saveLoadPanel == null)
            saveLoadPanel = FindObjectOfType<SaveLoadPanelUI>();
        if (promptUI == null && UIManager.Instance != null)
            promptUI = UIManager.Instance.interactionPromptUI;
        if (promptUI == null)
            promptUI = FindObjectOfType<InteractionPromptUI>();
    }

    // 玩家在范围内且游戏状态为 Playing 时检测按键
    void Update()
    {
        if (!_playerInside || saveLoadPanel == null) return;
        if (!GameStateManager.IsGameplayPlaying) return;
        if (Input.GetKeyDown(interactKey))
            saveLoadPanel.OpenPanel();
    }

    // 玩家进入显示提示
    void OnTriggerEnter(Collider other)
    {
        if (!other.GetComponent<PlayerHealth>()) return;
        _playerInside = true;
        if (promptUI != null)
            promptUI.ShowPrompt($"按 {interactKey} 使用打字机存档");
    }

    // 玩家离开隐藏提示
    void OnTriggerExit(Collider other)
    {
        if (!other.GetComponent<PlayerHealth>()) return;
        _playerInside = false;
        if (promptUI != null)
            promptUI.HidePrompt();
    }
}
