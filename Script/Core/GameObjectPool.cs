using UnityEngine;

// Inspector 可配的 GameObject 对象池：拖预制体和预热数量，供 BreakableCrate 等引用。
// 内部依赖 GameObjectPrefabPool 实现具体池逻辑，本脚本只是 MonoBehaviour 的包装层。
// 挂载：场景中专用的 Pool 空物体，Awake 时预热，对象回收到本物体下保持场景干净。
[DisallowMultipleComponent]
public class GameObjectPool : MonoBehaviour
{
    [Header("池配置")]
    [Tooltip("与要生成的掉落物预制体一致")]
    public GameObject prefab;

    [Min(0)]
    [Tooltip("开始时预生成的闲置数量")]
    public int prewarmCount = 4;

    GameObjectPrefabPool _pool;

    // Awake 而非 Start，保证其他脚本在 Start 里调 Spawn 时池已就绪
    void Awake()
    {
        if (prefab == null)
        {
            Debug.LogWarning("GameObjectPool: prefab 未赋值。", this);
            return;
        }
        _pool = new GameObjectPrefabPool(prefab, transform, prewarmCount);
    }

    // 从池中取实例并激活；池未初始化时 fallback Instantiate，不会崩
    public GameObject Spawn(Vector3 position, Quaternion rotation)
    {
        if (_pool == null)
        {
            if (prefab == null) return null;
            return Instantiate(prefab, position, rotation);
        }
        return _pool.Get(position, rotation);
    }

    // 回收实例；池未初始化时直接 Destroy
    public void Despawn(GameObject instance)
    {
        if (_pool == null)
        {
            if (instance != null) Destroy(instance);
            return;
        }
        _pool.Release(instance);
    }
}
