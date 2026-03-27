using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent carrier for world selection across scenes.
/// Handles save slot creation, loading, and metadata (seed) persistence.
/// Place in the main menu scene only — survives scene loads via DontDestroyOnLoad.
/// </summary>
public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    [Header("Active Session")]
    [Tooltip("Set at runtime by CreateSave() or LoadSave(). Fallback for editor testing.")]
    public string worldName = "default";
    public int seed = 12345;

    [Tooltip("Name of the game scene to load.")]
    public string gameSceneName = "Game";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -------------------------------------------------------------------------
    // Public API — call these from your main menu UI
    // -------------------------------------------------------------------------

    /// <summary>
    /// Create a brand new save with a random seed and immediately start the game.
    /// </summary>
    public void CreateSave(string world)
    {
        int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        SaveMetadata(world, newSeed);
        StartGame(world, newSeed);
    }

    /// <summary>
    /// Load an existing save slot and start the game with its stored seed.
    /// Returns false if the save does not exist.
    /// </summary>
    public bool LoadSave(string world)
    {
        var meta = ReadMetadata(world);
        if (meta == null)
        {
            Debug.LogWarning($"[GameSettings] No save found for world '{world}'.");
            return false;
        }

        StartGame(world, meta.seed);
        return true;
    }

    /// <summary>
    /// Returns all existing save slot names, sorted by last played (newest first).
    /// </summary>
    public List<SaveSlotInfo> GetSaveSlots()
    {
        var slots = new List<SaveSlotInfo>();
        string savesRoot = GetSavesRootPath();

        if (!Directory.Exists(savesRoot))
            return slots;

        foreach (var dir in Directory.GetDirectories(savesRoot))
        {
            string metaPath = Path.Combine(dir, "meta.json");
            if (!File.Exists(metaPath)) continue;

            try
            {
                var meta = JsonUtility.FromJson<SaveMetaFile>(File.ReadAllText(metaPath));
                slots.Add(new SaveSlotInfo
                {
                    worldName = meta.worldName,
                    seed = meta.seed,
                    lastPlayed = DateTime.FromBinary(meta.lastPlayedBinary)
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettings] Could not read metadata at {metaPath}: {e.Message}");
            }
        }

        slots.Sort((a, b) => b.lastPlayed.CompareTo(a.lastPlayed));
        return slots;
    }

    /// <summary>
    /// Delete a save slot entirely — wipes all world data and metadata.
    /// </summary>
    public void DeleteSave(string world)
    {
        string saveDir = GetSavePath(world);
        if (Directory.Exists(saveDir))
        {
            Directory.Delete(saveDir, recursive: true);
            Debug.Log($"[GameSettings] Deleted save '{world}'.");
        }
    }

    /// <summary>
    /// Returns true if a save slot with this name already exists.
    /// Useful for preventing duplicate world names in the UI.
    /// </summary>
    public bool SaveExists(string world)
    {
        return File.Exists(Path.Combine(GetSavePath(world), "meta.json"));
    }

    /// <summary>
    /// Updates the lastPlayed timestamp in the metadata file. Called on each save.
    /// </summary>
    public void UpdateLastPlayed(string world)
    {
        var meta = ReadMetadata(world);
        if (meta == null) return;
        meta.lastPlayedBinary = DateTime.Now.ToBinary();
        File.WriteAllText(
            Path.Combine(GetSavePath(world), "meta.json"),
            JsonUtility.ToJson(meta, prettyPrint: true)
        );
    }

    // -------------------------------------------------------------------------
    // Internal
    // -------------------------------------------------------------------------

    private void StartGame(string world, int gameSeed)
    {
        worldName = world;
        seed = gameSeed;
        SceneManager.LoadScene(gameSceneName);
    }

    private void SaveMetadata(string world, int gameSeed)
    {
        string saveDir = GetSavePath(world);
        Directory.CreateDirectory(saveDir);

        var meta = new SaveMetaFile
        {
            worldName = world,
            seed = gameSeed,
            lastPlayedBinary = DateTime.Now.ToBinary()
        };

        File.WriteAllText(
            Path.Combine(saveDir, "meta.json"),
            JsonUtility.ToJson(meta, prettyPrint: true)
        );
    }

    private SaveMetaFile ReadMetadata(string world)
    {
        string metaPath = Path.Combine(GetSavePath(world), "meta.json");
        if (!File.Exists(metaPath)) return null;

        try
        {
            return JsonUtility.FromJson<SaveMetaFile>(File.ReadAllText(metaPath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameSettings] Failed to read metadata for '{world}': {e.Message}");
            return null;
        }
    }

    private static string GetSavesRootPath()
    {
        return Path.Combine(Application.persistentDataPath, "worlds");
    }

    private static string GetSavePath(string world)
    {
        return Path.Combine(GetSavesRootPath(), world);
    }

    // -------------------------------------------------------------------------
    // Data classes
    // -------------------------------------------------------------------------

    [Serializable]
    private class SaveMetaFile
    {
        public string worldName;
        public int seed;
        public long lastPlayedBinary; // DateTime stored as long for JSON serialization
    }

    /// <summary>
    /// Returned by GetSaveSlots() for the main menu UI to display.
    /// </summary>
    public class SaveSlotInfo
    {
        public string worldName;
        public int seed;
        public DateTime lastPlayed;
    }
}