using UnityEngine;

// 医疗包使用逻辑：Q 键快捷使用，或背包 UI 右键指定格使用。
// 守卫条件：死亡、换弹中、满血、无医疗包 → 不消耗不治疗，体现「状态守卫」而非到处写 if。
// 挂载：PlayerArmature 根节点，与 PlayerHealth、PlayerInventory、HitscanShooter 同层。
public class PlayerHealAction : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("一般拖 PlayerArmature 上的 PlayerHealth")]
    public PlayerHealth playerHealth;

    [Tooltip("一般拖同物体上的 PlayerInventory")]
    public PlayerInventory playerInventory;

    [Tooltip("一般拖武器上的 HitscanShooter，用于换弹互斥")]
    public HitscanShooter hitscanShooter;

    [Header("治疗数值")]
    [Tooltip("每次使用医疗包恢复的生命值")]
    public int healAmount = 5;

    [Header("快捷使用（可选）")]
    [Tooltip("不打开背包时，可用快捷键立即使用一个医疗包")]
    public bool enableQuickUseKey = true;

    public KeyCode quickUseKey = KeyCode.Q;

    [Header("音效（可选）")]
    public AudioClip healSoundClip;

    // 组件引用补全，比直接在 Inspector 拖更省操作
    void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();
    }

    // 每帧检测快捷键，暂停/死亡时跳过
    void Update()
    {
        if (!enableQuickUseKey || playerHealth == null || playerHealth.IsDead)
            return;
        if (!GameStateManager.IsGameplayPlaying)
            return;
        if (Input.GetKeyDown(quickUseKey))
            TryUseMedKit();
    }

    // 尝试使用一个医疗包：通过全部守卫后扣库存并加血；失败静默返回 false
    public bool TryUseMedKit()
    {
        if (playerHealth == null || playerInventory == null)
            return false;
        if (playerHealth.IsDead)
            return false;
        // 换弹中不能用药，和射击动作冲突
        if (hitscanShooter != null && hitscanShooter.IsReloading)
            return false;
        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
            return false;
        if (playerInventory.medKitCount <= 0)
            return false;
        if (!playerInventory.TryConsumeMedKit(1))
            return false;

        playerHealth.Heal(healAmount);
        PlayHealSound();
        return true;
    }

    // 背包 UI 右键指定格使用医疗包，只扣这一格不跨格
    public bool TryUseMedKitFromSlot(int slotIndex)
    {
        if (playerHealth == null || playerInventory == null)
        {
            Debug.LogWarning("PlayerHealAction: PlayerHealth 或 PlayerInventory 未赋值。");
            return false;
        }
        if (playerHealth.IsDead)
            return false;
        if (hitscanShooter != null && hitscanShooter.IsReloading)
            return false;
        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
            return false;

        InventoryGridCell cell = playerInventory.GetGridCell(slotIndex);
        if (cell == null || cell.kind != InventoryGridItemKind.MedKit || cell.count < 1)
            return false;
        if (!playerInventory.TryConsumeMedKitFromSlot(slotIndex))
            return false;

        playerHealth.Heal(healAmount);
        PlayHealSound();
        return true;
    }

    // 优先用 AudioManager 播 3D 音效，没有时 fallback AudioSource.PlayClipAtPoint
    void PlayHealSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx3D(AudioKeys.HealSound, transform.position);
        else if (healSoundClip != null)
            AudioSource.PlayClipAtPoint(healSoundClip, transform.position, 1f);
    }
}
