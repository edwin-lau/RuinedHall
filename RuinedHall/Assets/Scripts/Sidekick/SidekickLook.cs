using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 玩家在 TestBody 里保存的 Sidekick 外观，落到 persistentDataPath 的 JSON。
/// </summary>
[Serializable]
public sealed class SidekickLook
{
    public const string FileName = "sidekick-look.json";

    public string[] slots = Array.Empty<string>();
    public string[] parts = Array.Empty<string>();
    public float skinny;
    public float heavy;
    public float muscles;
    public float faceBlend;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool Exists => File.Exists(FilePath);

    /// <summary>从当前装备槽和身材参数生成一份可序列化外观。</summary>
    public static SidekickLook From(
        IReadOnlyDictionary<string, string> equipped,
        float skinny,
        float heavy,
        float muscles,
        float faceBlend)
    {
        var look = new SidekickLook
        {
            skinny = skinny,
            heavy = heavy,
            muscles = muscles,
            faceBlend = faceBlend,
        };

        if (equipped == null || equipped.Count == 0)
            return look;

        look.slots = new string[equipped.Count];
        look.parts = new string[equipped.Count];
        int i = 0;
        foreach (KeyValuePair<string, string> pair in equipped)
        {
            look.slots[i] = pair.Key;
            look.parts[i] = pair.Value ?? "";
            i++;
        }

        return look;
    }

    /// <summary>把保存的槽位写回运行时装备字典。</summary>
    public void WriteTo(Dictionary<string, string> equipped)
    {
        if (equipped == null || slots == null || parts == null)
            return;

        int count = Mathf.Min(slots.Length, parts.Length);
        for (int i = 0; i < count; i++)
        {
            if (string.IsNullOrEmpty(slots[i]))
                continue;
            equipped[slots[i]] = parts[i] ?? "";
        }
    }

    /// <summary>覆盖写入 JSON。</summary>
    public void Save()
    {
        File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
        Debug.Log("SidekickLook: 已保存到 " + FilePath);
    }

    /// <summary>读盘；文件坏了返回 null。</summary>
    public static SidekickLook Load()
    {
        if (!Exists)
            return null;

        try
        {
            return JsonUtility.FromJson<SidekickLook>(File.ReadAllText(FilePath));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("SidekickLook: 读取失败。 " + ex.Message);
            return null;
        }
    }
}
