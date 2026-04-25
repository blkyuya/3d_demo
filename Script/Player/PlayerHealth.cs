using UnityEngine;
using System;

// 玩家血量：管理当前/最大生命值，受伤和治疗通过方法调用，变化通过 C# event 通知 UI 和其他系统。
// 血量归零时触发 OnDied，GameStateManager 订阅后切换到 GameOver。
// 挂载：PlayerArmature 根节点。
public class PlayerHealth : MonoBehaviour
{
    [Header("生命值")]
    public int maxHealth = 10;

    [SerializeField]
    private int currentHealth;

    // 血量变化（当前值, 最大值），PlayerHealthUI 订阅这个刷新血条
    public event Action<int, int> OnHealthChanged;

    // 死亡时触发一次，GameStateManager 和 PlayerStateManager 都订阅了这个
    public event Action OnDied;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    // 死亡后各系统读这个来 guard 自己的逻辑，比如射击死亡后就不再处理输入
    public bool IsDead { get; private set; }

    // 初始化血量，通知 UI 第一帧同步显示
    void Start()
    {
        currentHealth = maxHealth;
        IsDead = false;
        NotifyHealthChanged();
    }

    // 受到伤害；死亡状态下调这个直接无视
    public void TakeDamage(int damage)
    {
        if (IsDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        NotifyHealthChanged();

        if (currentHealth <= 0)
            Die();
    }

    // 使用医疗包；死亡状态下同样无视，上限 maxHealth
    public void Heal(int amount)
    {
        if (IsDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        NotifyHealthChanged();
    }

    // 同时发 C# event 和 EventCenter 事件，两条路各自覆盖有引用和无引用的订阅方
    void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        EventCenter.Publish(new PlayerHealthChangedEvent(currentHealth, maxHealth));
    }

    // 标记死亡，触发事件；OnDied 只触发一次，IsDead guard 防止重复
    void Die()
    {
        if (IsDead) return;

        IsDead = true;
        OnDied?.Invoke();
        EventCenter.Publish(PlayerDiedEvent.Default);
    }

    // 读档后直接覆盖血量，不走 Die 逻辑，读档流程自己处理后续恢复
    public void ApplyLoadedHealthState(int health, int maxHp, bool markDead)
    {
        if (maxHp > 0)
            maxHealth = maxHp;
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
        IsDead = markDead || currentHealth <= 0;
        NotifyHealthChanged();
    }
}
