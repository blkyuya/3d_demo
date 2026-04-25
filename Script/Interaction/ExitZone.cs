using UnityEngine;

// 出口胜利触发区：玩家进入触发体后触发胜利结算。
// 若配置了 HordeWaveManager，必须等尸潮全部击杀（IsCleared = true）才允许通关，否则仅提示。
// 挂载：关卡出口区域空物体，需要 Collider 且勾选 IsTrigger。
public class ExitZone : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("胜利界面")]
    public WinUI winUI;

    [Header("尸潮前置条件（可选）")]
    [Tooltip("若赋值，则必须等 HordeWaveManager.IsCleared 为 true 才允许通关")]
    public HordeWaveManager hordeGate;

    // 防止重复触发（玩家在区域内徘徊时可能多次 Enter）
    bool _triggered;

    // 通过 PlayerInventory 识别玩家（不依赖 Tag，更健壮）
    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;

        PlayerInventory inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null) return;

        // 尸潮还没清空，拒绝通关并提示玩家
        if (hordeGate != null && !hordeGate.IsCleared)
        {
            PickupNotificationHub.Publish("清完威胁后才能撤离");
            return;
        }

        _triggered = true;

        if (winUI != null)
            winUI.ShowWin();
    }
}
