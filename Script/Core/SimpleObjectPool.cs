using UnityEngine;

// 轻量泛型对象池：根节点上须挂有 T 组件；内部复用 GameObjectPrefabPool，避免重复实现栈逻辑。
public sealed class SimpleObjectPool<T> where T : Component
{
    readonly GameObjectPrefabPool _gameObjects;

    // prefab：预制体根上的组件；parent：池父节点；prewarm：预热数量
    public SimpleObjectPool(T prefab, Transform parent, int prewarm)
    {
        _gameObjects = new GameObjectPrefabPool(prefab.gameObject, parent, prewarm);
    }

    // 从池中取出实例并激活，设置位置和旋转；实例上缺少组件时报错
    public T Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = _gameObjects.Get(position, rotation);
        T comp = go.GetComponent<T>();
        if (comp == null)
            Debug.LogError($"SimpleObjectPool: 实例上缺少 {typeof(T).Name}，预制体配置有误。");
        return comp;
    }

    // 回收实例，停用并压回栈
    public void Release(T instance)
    {
        if (instance == null) return;
        _gameObjects.Release(instance.gameObject);
    }

    // 当前闲置实例数量
    public int InactiveCount => _gameObjects.InactiveCount;
}
