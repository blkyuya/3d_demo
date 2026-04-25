// 跨 LoadScene 传递待应用的存档数据（静态存储）。
// 流程：SaveLoadPanelUI 读档 → 把数据写入 Pending → LoadScene → SaveLoadBootstrap 在新场景取出并清空。
public static class SaveLoadPendingStore
{
    public static GameSaveData Pending { get; set; }
}
