using UnityEngine;

// 可破坏箱子：挂在场景箱体根节点，被打到血量归零后生成掉落物并销毁自身。
// 掉落物走对象池（GameObjectPool），池找不到时 fallback Instantiate，不会崩。
public class BreakableCrate : MonoBehaviour, IDamageable
{
    [Header("生命值")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("掉落配置")]
    [Tooltip("打爆后生成的掉落物预制体")]
    public GameObject dropPrefab;

    [Tooltip("场景中的对象池，留空且开启自动查找时按预制体名称匹配")]
    public GameObjectPool dropPool;

    [Tooltip("开启后自动在场景里找预制体匹配的 GameObjectPool")]
    public bool autoBindDropPoolInScene = true;

    [Tooltip("掉落物生成位置相对本物体的偏移")]
    public Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);

    // Awake 里初始化血量，防止 TakeDamage 在 Start 之前调用时拿到 0
    void Awake()
    {
        currentHealth = maxHealth;
        TryAutoBindDropPool();
    }

    // 实现 IDamageable，HitscanShooter 射线命中后调这里扣血
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
            Break();
    }

    // 血量归零触发，生成掉落物后销毁自身
    void Break()
    {
        if (dropPrefab != null)
        {
            TryAutoBindDropPool();
            Vector3 pos = transform.position + dropOffset;

            // 对象池可用且预制体匹配时走池，避免频繁 Instantiate
            if (dropPool != null && dropPool.prefab != null && IsSameDropPrefab(dropPool.prefab, dropPrefab))
                dropPool.Spawn(pos, Quaternion.identity);
            else
            {
                if (dropPool != null && dropPool.prefab != null && !IsSameDropPrefab(dropPool.prefab, dropPrefab))
                    Debug.LogWarning("BreakableCrate: Drop Pool 预制体与 Drop Prefab 不一致，改用 Instantiate。", this);
                Instantiate(dropPrefab, pos, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }

    // 没有手动指定对象池时，遍历场景按预制体引用或名称自动绑定
    void TryAutoBindDropPool()
    {
        if (!autoBindDropPoolInScene || dropPool != null || dropPrefab == null)
            return;

        foreach (GameObjectPool pool in FindObjectsOfType<GameObjectPool>(true))
        {
            if (pool == null || pool.prefab == null)
                continue;
            if (IsSameDropPrefab(pool.prefab, dropPrefab))
            {
                dropPool = pool;
                return;
            }
        }
    }

    // 先比引用，相同直接返回；不同再比名字做 fallback，防止 Inspector 引用偶发不一致
    static bool IsSameDropPrefab(GameObject poolPrefab, GameObject dropPrefabRef)
    {
        if (poolPrefab == null || dropPrefabRef == null)
            return false;
        if (poolPrefab == dropPrefabRef)
            return true;
        return poolPrefab.name == dropPrefabRef.name;
    }
}
