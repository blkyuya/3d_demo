using System;
using System.Collections.Generic;
using UnityEngine;

// 逻辑键 → AudioClip 映射表：策划在 Project 里创建此资源并拖入音频文件。
// AudioManager 启动时调用 RebuildLookup 建立字典，查找时支持大小写和下划线/空格差异，
// 避免音频文件名命名习惯不一致时每次都要改代码。
[CreateAssetMenu(menuName = "游戏/音频目录", fileName = "AudioCatalog")]
public class AudioCatalogSO : ScriptableObject
{
    [Serializable]
    public struct NamedClip
    {
        [Tooltip("与 AudioKeys 中常量一致的字符串，例如 pistol_shot")]
        public string key;
        public AudioClip clip;
    }

    [Header("键值对列表")]
    [Tooltip("key 需唯一；例如 pistol_shot 对应手枪射击音效")]
    public NamedClip[] entries = Array.Empty<NamedClip>();

    readonly Dictionary<string, AudioClip> _map = new Dictionary<string, AudioClip>();

    // 从 entries 数组构建 Dictionary，跳过 key 或 clip 为空的行
    public void RebuildLookup()
    {
        _map.Clear();
        if (entries == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            NamedClip e = entries[i];
            if (string.IsNullOrEmpty(e.key) || e.clip == null) continue;
            _map[e.key] = e.clip;
        }
    }

    // 先精确匹配，再忽略大小写/空格/下划线差异做模糊匹配
    public bool TryGet(string key, out AudioClip clip)
    {
        clip = null;
        if (string.IsNullOrEmpty(key)) return false;

        // 精确匹配
        if (_map.TryGetValue(key, out clip)) return true;

        // 忽略大小写和下划线差异：door_creak ↔ door creak ↔ Door Creak
        string nk = NormalizeKey(key);
        foreach (var kvp in _map)
        {
            if (NormalizeKey(kvp.Key) == nk)
            {
                clip = kvp.Value;
                return true;
            }
        }

        return false;
    }

    // 把 key 统一为小写+下划线形式，方便模糊匹配
    public static string NormalizeKey(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.Trim().ToLowerInvariant().Replace(' ', '_');
        while (s.Contains("__"))
            s = s.Replace("__", "_");
        return s;
    }

    // Inspector 里改完表后即时刷新字典，方便调试时不用重新运行
    void OnValidate()
    {
        RebuildLookup();
    }
}
