using UnityEngine;
using TMPro;

public class InteractionPromptUI : BasePanel
{
    [Header("References")]
    public GameObject promptRoot;
    public TextMeshProUGUI promptText;

    protected override void OnPanelInit()
    {
        if (promptRoot != null)
            panelRoot = promptRoot;
        HidePrompt();
    }

    public void ShowPrompt(string message)
    {
        if (promptRoot != null)
            promptRoot.SetActive(true);

        if (promptText != null)
            promptText.text = message;
    }

    public void HidePrompt()
    {
        if (promptRoot != null)
            promptRoot.SetActive(false);
    }
}
