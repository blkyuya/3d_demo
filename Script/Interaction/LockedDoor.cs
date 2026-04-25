using UnityEngine;
using UnityEngine.AI;

// 钥匙门：玩家持有钥匙时按 E 开门，门扇平滑旋转到目标角度。
// 开门完成后同时关闭：
//   - 本物体上的交互 Trigger（防止射线/物理仍命中门框）
//   - 门扇上的实体碰撞体（阻挡物理的那些，跳过 Trigger 类型）
//   - NavMeshObstacle（停止 Carve，NavMesh 路径恢复可走，AI 不再绕道）
// 挂载：钥匙门触发体根节点，根节点 Collider 设为 IsTrigger 用于检测玩家进出。
public class LockedDoor : MonoBehaviour
{
    [Header("门扇设置")]
    [Tooltip("执行旋转动画的门扇 Transform")]
    public Transform doorObject;

    [Tooltip("绕 Y 轴的开门角度（度），正值向里推，负值向外开")]
    public float openAngle = 90f;

    [Tooltip("开门旋转插值速度")]
    public float openSpeed = 2f;

    public bool isOpen = false;

    private bool canInteract = false;
    private bool isOpening = false;
    private PlayerInventory currentPlayerInventory;
    private InteractionPromptUI promptUI;

    private Quaternion closedRotation;
    private Quaternion targetRotation;

    // 开门后关闭本触发器，防止门框区域仍然响应 E 键
    private Collider _interactionCollider;

    // 缓存 Collider，Start 前就可能触发 OnTriggerEnter
    void Awake()
    {
        _interactionCollider = GetComponent<Collider>();
    }

    // 找 UI 引用，计算开门目标角度（在关门状态的基础上旋转 openAngle 度）
    void Start()
    {
        if (UIManager.Instance != null)
            promptUI = UIManager.Instance.interactionPromptUI;
        if (promptUI == null)
            promptUI = FindObjectOfType<InteractionPromptUI>();

        if (doorObject != null)
        {
            closedRotation = doorObject.rotation;
            targetRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }
    }

    // 每帧处理 E 键交互和开门旋转动画
    void Update()
    {
        if (!GameStateManager.IsGameplayPlaying)
            return;

        if (canInteract && Input.GetKeyDown(KeyCode.E))
            TryOpenDoor();

        // 旋转到位后结算开门状态，关闭碰撞体和障碍
        if (isOpening && doorObject != null)
        {
            doorObject.rotation = Quaternion.Slerp(
                doorObject.rotation, targetRotation, openSpeed * Time.deltaTime);

            if (Quaternion.Angle(doorObject.rotation, targetRotation) < 0.5f)
            {
                doorObject.rotation = targetRotation;
                isOpening = false;
                isOpen = true;

                if (promptUI != null)
                    promptUI.HidePrompt();

                if (_interactionCollider != null)
                    _interactionCollider.enabled = false;

                // 关门扇上的实体碰撞体和 NavMeshObstacle，AI 就能通行了
                SetDoorBlockingCollidersAndObstacles(false);
            }
        }
    }

    // 开门后关闭门扇子物体上的非 Trigger 碰撞体与 NavMeshObstacle
    // NavMeshObstacle 的 Carve 功能会动态挖洞，禁用后挖洞消失，NavMesh 路径恢复
    void SetDoorBlockingCollidersAndObstacles(bool enable)
    {
        if (doorObject == null)
            return;

        var colliders = doorObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i].isTrigger)
                continue;
            colliders[i].enabled = enable;
        }

        var obstacles = doorObject.GetComponentsInChildren<NavMeshObstacle>(true);
        for (int i = 0; i < obstacles.Length; i++)
        {
            if (obstacles[i] != null)
                obstacles[i].enabled = enable;
        }
    }

    // 检查是否有钥匙，有则开门，没有则提示
    void TryOpenDoor()
    {
        if (isOpen || isOpening)
            return;
        if (currentPlayerInventory == null)
            return;

        if (currentPlayerInventory.hasKey)
        {
            if (promptUI != null)
                promptUI.HidePrompt();

            if (doorObject != null)
            {
                isOpening = true;
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySfx3D(AudioKeys.DoorCreak, doorObject.position);
            }
            else
            {
                Debug.LogWarning("LockedDoor: doorObject 未赋值，无法执行开门动画。", this);
            }
        }
        else
        {
            if (promptUI != null)
                promptUI.ShowPrompt("门已上锁，需要钥匙");
        }
    }

    // 玩家进入触发区域，记录背包引用，显示操作提示
    void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null)
            return;

        canInteract = true;
        currentPlayerInventory = inventory;

        if (promptUI != null)
        {
            if (isOpen)
                promptUI.HidePrompt();
            else
                promptUI.ShowPrompt("按 E 开门");
        }
    }

    // 玩家离开触发区域，清空引用，隐藏提示
    void OnTriggerExit(Collider other)
    {
        PlayerInventory inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null || inventory != currentPlayerInventory)
            return;

        canInteract = false;
        currentPlayerInventory = null;

        if (promptUI != null)
            promptUI.HidePrompt();
    }
}
