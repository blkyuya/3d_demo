using System.Collections.Generic;
using UnityEngine;

// 针对 GameObject 预制体的轻量池，内部用 Stack 管理闲置实例。
// 不依赖根节点具体组件类型，供 GameObjectPool（MonoBehaviour 包装层）使用。
public sealed class GameObjectPrefabPool
{
    readonly GameObject _prefab;
    readonly Stack<GameObject> _inactive = new Stack<GameObject>();
    readonly Transform _parent;

    // 构造时预热，预生成 prewarm 个停用实例挂在 parent 下
    public GameObjectPrefabPool(GameObject prefab, Transform parent, int prewarm)
    {
        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < prewarm; i++)
        {
            GameObject go = Object.Instantiate(_prefab, _parent);
            go.SetActive(false);
            _inactive.Push(go);
        }
    }

    // 有闲置实例就弹栈复用，没有就 Instantiate；设置位置后激活
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = _inactive.Count > 0
            ? _inactive.Pop()
            : Object.Instantiate(_prefab, _parent);

        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    // 停用实例并挂回父节点，压入栈等待复用
    public void Release(GameObject instance)
    {
        if (instance == null) return;
        instance.SetActive(false);
        instance.transform.SetParent(_parent);
        _inactive.Push(instance);
    }

    // 当前闲置数量
    public int InactiveCount => _inactive.Count;
}
