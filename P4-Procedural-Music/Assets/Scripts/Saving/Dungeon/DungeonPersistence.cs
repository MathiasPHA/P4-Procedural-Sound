using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Minimal delta save for a single dungeon visit.
/// Only stores what changed — removed object indices and placed structures.
/// Saved to worlds/{worldName}/dungeons/{seed}.json
/// </summary>
[Serializable]
public class DungeonSaveData
{
    public List<int> removedIndices = new List<int>();
    public List<DungeonPlacedEntry> placedStructures = new List<DungeonPlacedEntry>();
}

[Serializable]
public class DungeonPlacedEntry
{
    public string itemId;
    public float x;
    public float y;
}

/// <summary>
/// Static utility for saving/loading dungeon deltas.
/// </summary>
public static class DungeonPersistence
{
    public static DungeonSaveData Load(string worldName, int seed)
    {
        string path = GetPath(worldName, seed);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonUtility.FromJson<DungeonSaveData>(File.ReadAllText(path));
        }
        catch
        {
            Debug.LogWarning($"[DungeonPersistence] Corrupt save for seed {seed}, ignoring.");
            return null;
        }
    }

    public static void Save(string worldName, int seed, DungeonSaveData data)
    {
        string path = GetPath(worldName, seed);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(data));
    }

    public static void Delete(string worldName, int seed)
    {
        string path = GetPath(worldName, seed);
        if (File.Exists(path)) File.Delete(path);
    }

    private static string GetPath(string worldName, int seed)
    {
        return Path.Combine(
            Application.persistentDataPath, "worlds", worldName,
            "dungeons", $"{seed}.json");
    }
}
