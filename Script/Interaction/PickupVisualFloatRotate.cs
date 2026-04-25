using UnityEngine;

// 拾取物悬浮旋转特效：绕 Y 轴自转并上下浮动，提升场景中可拾取物品的可见性。
// 挂载：场景拾取物的可视子物体（如钥匙、医疗包模型节点），不影响 Collider。
public class PickupVisualFloatRotate : MonoBehaviour
{
    [Header("旋转")]
    [Tooltip("每秒绕 Y 轴旋转的角度")]
    public float rotateSpeed = 90f;

    [Header("浮动")]
    [Tooltip("上下浮动幅度（米）")]
    public float floatAmplitude = 0.08f;

    [Tooltip("浮动频率（Hz），越大上下越快")]
    public float floatFrequency = 2f;

    private Vector3 startLocalPosition;

    // 记录起始本地位置，浮动基于这个点偏移，不是相对 world position
    void Start()
    {
        startLocalPosition = transform.localPosition;
    }

    // 每帧旋转 + 正弦浮动
    void Update()
    {
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.Self);

        float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.localPosition = startLocalPosition + new Vector3(0f, yOffset, 0f);
    }
}
