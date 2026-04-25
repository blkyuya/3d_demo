using UnityEngine;

// 场景可拾取物（钥匙 / 医疗包 / 弹药）：玩家进入触发体后按 E 拾取，成功后播放音效、显示飘字、销毁自身。
// 类型由 PickupType 枚举决定，对应不同背包写入逻辑，无需在内部区分具体数据结构。
// 挂载：场景中各拾取物 GameObject 根节点，Collider 勾 IsTrigger。
public class PickupItem : MonoBehaviour
{
    public enum PickupType
    {
        Key,
        MedKit,
        Ammo
    }

    [Header("拾取设置")]
    public PickupType pickupType = PickupType.Key;

    [Tooltip("数量（医疗包 / 弹药有效，钥匙固定为 1）")]
    public int amount = 1;

    [Tooltip("仅当 Pickup Type 为 Ammo 时有效")]
    public AmmoType ammoType = AmmoType.Pistol9mm;

    private bool canPickup = false;
    private PlayerInventory currentPlayerInventory;
    private InteractionPromptUI promptUI;

    // 找 UI 引用，优先从 UIManager 拿，避免 FindObjectOfType 每帧开销
    void Start()
    {
        if (UIManager.Instance != null)
            promptUI = UIManager.Instance.interactionPromptUI;
        if (promptUI == null)
            promptUI = FindObjectOfType<InteractionPromptUI>();
    }

    // 检测 E 键触发拾取，非游玩状态下屏蔽
    void Update()
    {
        if (!GameStateManager.IsGameplayPlaying) return;
        if (canPickup && Input.GetKeyDown(KeyCode.E))
            Pickup();
    }

    // 写入背包，发飘字通知，播音效，销毁自身
    void Pickup()
    {
        if (currentPlayerInventory == null) return;

        string toast = BuildPickupToastMessage();

        switch (pickupType)
        {
            case PickupType.Key:    currentPlayerInventory.AddKey(); break;
            case PickupType.MedKit: currentPlayerInventory.AddMedKit(amount); break;
            case PickupType.Ammo:   currentPlayerInventory.AddAmmo(ammoType, amount); break;
        }

        if (!string.IsNullOrEmpty(toast))
            PickupNotificationHub.Publish(toast);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx3D(AudioKeys.ItemPickup, transform.position);

        if (promptUI != null)
            promptUI.HidePrompt();

        Destroy(gameObject);
    }

    // 拼接拾取成功后的飘字文案
    string BuildPickupToastMessage()
    {
        switch (pickupType)
        {
            case PickupType.Key:    return "获得：钥匙";
            case PickupType.MedKit: return amount > 1 ? $"获得：医疗包 ×{amount}" : "获得：医疗包";
            case PickupType.Ammo:   return $"获得：{GetAmmoToastName()} ×{amount}";
            default:                return "获得：物品";
        }
    }

    // 弹药类型转中文名
    string GetAmmoToastName()
    {
        switch (ammoType)
        {
            case AmmoType.Pistol9mm:    return "手枪弹药";
            case AmmoType.ShotgunShell: return "霰弹弹药";
            default:                    return "弹药";
        }
    }

    // 玩家进入触发区，记录引用，显示操作提示
    void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        canPickup = true;
        currentPlayerInventory = inventory;

        if (promptUI != null)
            promptUI.ShowPrompt(GetPickupPromptText());
    }

    // 玩家离开触发区，清引用，隐藏提示
    void OnTriggerExit(Collider other)
    {
        PlayerInventory inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null || inventory != currentPlayerInventory) return;

        canPickup = false;
        currentPlayerInventory = null;

        if (promptUI != null)
            promptUI.HidePrompt();
    }

    // 根据类型返回交互提示文案
    string GetPickupPromptText()
    {
        switch (pickupType)
        {
            case PickupType.Key:    return "按 E 拾取钥匙";
            case PickupType.MedKit: return "按 E 拾取医疗包";
            case PickupType.Ammo:   return "按 E 拾取弹药";
            default:                return "按 E 拾取";
        }
    }
}
