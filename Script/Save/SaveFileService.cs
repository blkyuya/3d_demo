using System;
using System.IO;
using UnityEngine;

// 本地 5 槽存档文件读写：以 Application.persistentDataPath 为根目录，存 JSON 格式。
// 静态工具类，无需挂载，SaveGameCollector 和 SaveLoadSceneApplier 直接调用。
public static class SaveFileService
{
    public const int SlotCount = 5;
    const string FilePrefix = "save_slot_";
    const string FileExt = ".json";

    // 根据槽位索引拼出完整文件路径
    static string SlotPath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"{FilePrefix}{slotIndex}{FileExt}");
    }

    // 检查指定槽位是否有存档文件，UI 上显示灰色/亮色槽时用
    public static bool SlotExists(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return false;
        return File.Exists(SlotPath(slotIndex));
    }

    // 读取整份存档并反序列化，失败时返回 null 而不是抛异常
    public static GameSaveData TryLoad(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return null;
        string path = SlotPath(slotIndex);
        if (!File.Exists(path))
            return null;
        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<GameSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveFileService: 读取失败 " + e.Message);
            return null;
        }
    }

    // 序列化存档数据并写入文件（覆盖同槽），同时记录保存时间戳
    public static bool TrySave(int slotIndex, GameSaveData data)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount || data == null)
            return false;
        try
        {
            data.savedUtcTicks = DateTime.UtcNow.Ticks;
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SlotPath(slotIndex), json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveFileService: 写入失败 " + e.Message);
            return false;
        }
    }

    // 读取存档时间戳用于列表预览，没有文件或解析失败返回 null
    public static DateTime? TryGetSavedTimeUtc(int slotIndex)
    {
        var d = TryLoad(slotIndex);
        if (d == null) return null;
        try
        {
            return new DateTime(d.savedUtcTicks, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }
}
