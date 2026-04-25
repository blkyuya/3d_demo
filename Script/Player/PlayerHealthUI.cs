using UnityEngine;
using UnityEngine.UI;

// 玩家血量 UI：订阅 PlayerHealth 血量变化事件，实时更新血条填充比例。
// 挂载：HUD 上的血条面板节点。
public class PlayerHealthUI : BasePanel
{
    [Header("引用")]
    [Tooltip("玩家血量组件")]
    public PlayerHealth playerHealth;

    [Tooltip("血条填充图片（Image 的 Image Type 需设为 Filled）")]
    public Image healthBarFill;

    // 初始化：找面板根节点，订阅血量变化事件，同步初始血量显示
    protected override void OnPanelInit()
    {
        if (healthBarFill != null && healthBarFill.transform.parent != null)
            panelRoot = healthBarFill.transform.parent.gameObject;

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;
            UpdateHealthBar(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    // 销毁时取消订阅，防止 delegate 持有已销毁引用
    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthBar;
    }

    // 把血量比值映射到 fillAmount，血量为 0 时 fill=0（空血条）
    void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (healthBarFill == null || maxHealth <= 0) return;
        healthBarFill.fillAmount = (float)currentHealth / maxHealth;
    }
}
