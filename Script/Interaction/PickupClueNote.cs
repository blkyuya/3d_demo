using UnityEngine;

// 桌上纸条拾取：按 E 将密码/线索文本写入背包一格（ClueNote 类型）。
// Awake 里自动把 Collider 改为 Trigger，并添加运动学刚体（CharacterController 需要这个才能稳定收到 OnTriggerEnter）。
// 挂载：场景中纸条道具根节点，需要 Collider。
[RequireComponent(typeof(Collider))]
public class PickupClueNote : MonoBehaviour
{
    [Header("纸条内容")]
    [Tooltip("显示在背包右键查看中的文本，一般为四位数字密码")]
    public string notePayload = "1234";

    [Header("拾取提示")]
    public string pickupPrompt = "按 E 拾取纸条";

    bool _canPickup;
    PlayerInventory _inv;
    InteractionPromptUI _prompt;

    // 确保 Collider 是 Trigger，并添加运动学刚体
    void Awake()
    {
        var c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
        {
            Debug.LogWarning("PickupClueNote: 「" + gameObject.name + "」的 Collider 应勾选 Is Trigger，否则无法稳定触发拾取。");
            c.isTrigger = true;
        }

        // CharacterController 与静态 Trigger 组合时，需要运动学刚体才能稳定触发 OnTriggerEnter
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    // 补 UI 引用
    void Start()
    {
        if (UIManager.Instance != null)
            _prompt = UIManager.Instance.interactionPromptUI;
        if (_prompt == null)
            _prompt = FindObjectOfType<InteractionPromptUI>();
    }

    // 检测 E 键拾取，暂停状态下屏蔽
    void Update()
    {
        if (!GameStateManager.IsGameplayPlaying) return;
        if (_canPickup && Input.GetKeyDown(KeyCode.E))
            TryPickup();
    }

    // 写入背包，背包满时提示；成功后播音效销毁
    void TryPickup()
    {
        if (_inv == null) return;

        if (_inv.TryAddClueNote(notePayload))
        {
            PickupNotificationHub.Publish("获得：线索纸条");
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx3D(AudioKeys.ItemPickup, transform.position);
            if (_prompt != null) _prompt.HidePrompt();
            Destroy(gameObject);
        }
        else
            PickupNotificationHub.Publish("背包已满");
    }

    // 玩家进入触发区，找父物体上的 PlayerInventory（CharacterController 结构下 Collider 可能在子物体）
    void OnTriggerEnter(Collider other)
    {
        var inv = other.GetComponent<PlayerInventory>();
        if (inv == null) inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null) return;

        _canPickup = true;
        _inv = inv;
        if (_prompt != null) _prompt.ShowPrompt(pickupPrompt);
    }

    // 玩家离开触发区，清引用
    void OnTriggerExit(Collider other)
    {
        var inv = other.GetComponent<PlayerInventory>();
        if (inv == null) inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null || inv != _inv) return;

        _canPickup = false;
        _inv = null;
        if (_prompt != null) _prompt.HidePrompt();
    }
}
