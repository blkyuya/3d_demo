using UnityEngine;

// 练习靶/木人：实现 IDamageable，受伤时材质短暂变红，血量归零后销毁并可选掉落物品。
// 挂载：场景中可攻击的非僵尸目标。
public class DamageableTarget : MonoBehaviour, IDamageable
{
    [Header("生命值")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("受击反馈")]
    [Tooltip("要变色的 Renderer，留空则自动找子物体")]
    public Renderer targetRenderer;
    public Color hitColor = Color.red;
    [Tooltip("变红持续时间（秒）")]
    public float hitFlashTime = 0.1f;

    [Header("掉落（可选）")]
    [Tooltip("血量归零时生成的预制体，留空则只销毁")]
    public GameObject dropPrefab;

    [Tooltip("相对本物体的掉落偏移")]
    public Vector3 dropOffset = new Vector3(0f, 0.35f, 0f);

    private Color originalColor;

    // 拿原始颜色备用，后面恢复闪红需要
    void Start()
    {
        currentHealth = maxHealth;

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null && targetRenderer.material.HasProperty("_Color"))
            originalColor = targetRenderer.material.color;
    }

    // 扣血，触发闪红，血量归零调 Die
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (targetRenderer != null && targetRenderer.material.HasProperty("_Color"))
        {
            StopAllCoroutines();
            StartCoroutine(HitFlash());
        }

        if (currentHealth <= 0)
            Die();
    }

    // 变红 → 等 hitFlashTime → 恢复原色
    private System.Collections.IEnumerator HitFlash()
    {
        targetRenderer.material.color = hitColor;
        yield return new WaitForSeconds(hitFlashTime);
        targetRenderer.material.color = originalColor;
    }

    // 先生成掉落物再销毁自身，顺序不能反，Destroy 后 position 就没了
    void Die()
    {
        if (dropPrefab != null)
            Instantiate(dropPrefab, transform.position + dropOffset, Quaternion.identity);
        Destroy(gameObject);
    }
}
