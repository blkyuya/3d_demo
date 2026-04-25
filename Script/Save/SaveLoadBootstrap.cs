using UnityEngine;

// 存档应用引导器：场景加载后首帧 LateUpdate 取出 SaveLoadPendingStore 里的待应用存档，交给 Applier 处理。
// 用 LateUpdate 保证所有 Start 已执行、场景对象已初始化，此时再覆盖数据不会被初始化逻辑覆盖。
// 挂载：每个游戏场景里挂一个实例，普通空物体即可。
[DefaultExecutionOrder(100)]
public class SaveLoadBootstrap : MonoBehaviour
{
    // 每帧检查是否有待应用存档，有则取出、清空、应用；正常游戏时 Pending 为 null，此函数几乎无开销
    void LateUpdate()
    {
        GameSaveData pending = SaveLoadPendingStore.Pending;
        if (pending == null) return;

        SaveLoadPendingStore.Pending = null;
        SaveLoadSceneApplier.Apply(pending);
    }
}
