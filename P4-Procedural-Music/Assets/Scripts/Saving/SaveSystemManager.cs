using System.IO;
using ProceduralTerrain;
using UnityEngine;

public class SaveSystemManager : MonoBehaviour
{
    [Header("Save")]
    [Tooltip("Fallback world name if GameSettings is not present.")]
    public string worldName = "default";

    [Tooltip("Auto-save interval in seconds. 0 = manual only.")]
    public float autoSaveInterval = 60f;

    public static SaveSystemManager Instance { get; private set; }

    private float _saveTimer;

    /// <summary>
    /// Latched true after a permadeath wipe. While set, SaveModifiedChunks is a
    /// no-op so that autosave / OnApplicationQuit can't re-create files between
    /// deletion and the scene transition back to the main menu.
    /// </summary>
    private bool _worldDeleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Read worldName and seed from GameSettings if available
        if (GameSettings.Instance != null)
        {
            worldName = GameSettings.Instance.worldName;
        }
        else
        {
            Debug.LogWarning("[SaveSystemManager] GameSettings not found — using fallback worldName.");
        }
    }

    private void Start()
    {
        // Load inventory once everything is initialized
        if (InventorySaveSystem.Instance != null)
            InventorySaveSystem.Instance.LoadInventory(worldName);
        else
            Debug.LogWarning("[SaveSystemManager] InventorySaveSystem not found — inventory not loaded.");

        // Load player position and happiness
        if (PlayerSaveSystem.Instance != null)
            PlayerSaveSystem.Instance.LoadPlayer(worldName);
        else
            Debug.LogWarning("[SaveSystemManager] PlayerSaveSystem not found — player not loaded.");

        // Load time of day
        if (DayNightMaster.Instance != null)
            DayNightMaster.Instance.LoadTime(worldName);
    }

    private void Update()
    {
        if (autoSaveInterval > 0)
        {
            _saveTimer += Time.deltaTime;
            if (_saveTimer >= autoSaveInterval)
            {
                _saveTimer = 0f;
                SaveModifiedChunks();
            }
        }
    }

    private void OnApplicationQuit()
    {
        SaveModifiedChunks();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveModifiedChunks();
    }

    /// <summary>
    /// Force save all modified chunks now.
    /// </summary>
    public void SaveModifiedChunks()
    {
        // Permadeath gate: once the world's been wiped, never write again.
        if (_worldDeleted) return;

        // Save chunks (only if ChunkManager exists — won't in dungeon scenes)
        if (ChunkManager.Instance != null)
        {
            ChunkPersistence.SaveModifiedChunks(
                ChunkManager.Instance.LoadedChunks,
                ChunkManager.Instance.LoadedObjects,
                worldName
            );
        }

        // Save inventory
        if (InventorySaveSystem.Instance != null)
            InventorySaveSystem.Instance.SaveInventory(worldName);
        else
            Debug.LogWarning("[SaveSystemManager] InventorySaveSystem not found — inventory not saved.");

        // Save player position and happiness
        if (PlayerSaveSystem.Instance != null)
            PlayerSaveSystem.Instance.SavePlayer(worldName);
        else
            Debug.LogWarning("[SaveSystemManager] PlayerSaveSystem not found — player not saved.");

        // Save player-placed structures
        if (PlacedStructureManager.Instance != null)
            PlacedStructureManager.Instance.SaveAll();

        // Save time of day
        if (DayNightMaster.Instance != null)
            DayNightMaster.Instance.SaveTime(worldName);

        // Keep lastPlayed timestamp fresh in the metadata file
        if (GameSettings.Instance != null)
            GameSettings.Instance.UpdateLastPlayed(worldName);
    }

    // ───────────────────────── Permadeath ─────────────────────────

    /// <summary>
    /// Permanently deletes the current world's entire save folder
    /// (worlds/{worldName}/ — chunks, structures, inventory, player, time,
    /// metadata, everything). Disarms further saves so the in-memory state
    /// can't resurrect files before the scene unloads.
    ///
    /// Call this from your death flow for permadeath. Returns true if a
    /// folder was actually deleted.
    /// </summary>
    public bool DeleteCurrentWorld()
    {
        // Latch FIRST so any save call mid-deletion is a no-op.
        _worldDeleted = true;
        return DeleteWorld(worldName);
    }

    /// <summary>
    /// Delete a named world's save folder from disk. Static so a main-menu
    /// "Delete World" button can also call it without an active SaveSystemManager.
    /// </summary>
    public static bool DeleteWorld(string targetWorldName)
    {
        if (string.IsNullOrWhiteSpace(targetWorldName))
        {
            Debug.LogWarning("[SaveSystemManager] Cannot delete world — name is empty.");
            return false;
        }

        // Safety: never accept a name that could escape the worlds folder.
        if (targetWorldName.Contains("..") ||
            targetWorldName.Contains("/") ||
            targetWorldName.Contains("\\"))
        {
            Debug.LogError($"[SaveSystemManager] Refusing suspicious world name: '{targetWorldName}'");
            return false;
        }

        string worldsRoot = Path.Combine(Application.persistentDataPath, "worlds");
        string worldPath = Path.Combine(worldsRoot, targetWorldName);

        // Belt-and-braces: confirm the resolved path is actually inside worldsRoot.
        string fullWorldPath = Path.GetFullPath(worldPath);
        string fullRoot = Path.GetFullPath(worldsRoot);
        if (!fullWorldPath.StartsWith(fullRoot))
        {
            Debug.LogError($"[SaveSystemManager] Path escape blocked: {fullWorldPath}");
            return false;
        }

        if (!Directory.Exists(fullWorldPath))
        {
            Debug.LogWarning($"[SaveSystemManager] World folder not found: {fullWorldPath}");
            return false;
        }

        try
        {
            Directory.Delete(fullWorldPath, recursive: true);
            Debug.Log($"[SaveSystemManager] Deleted world: {targetWorldName}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystemManager] Failed to delete '{targetWorldName}': {e.Message}");
            return false;
        }
    }

    // ───────────────────────── Editor Tools ─────────────────────────

    /// <summary>
    /// Delete all save data and regenerate. Use if world is corrupted.
    /// Available from Inspector right-click menu.
    /// </summary>
    [ContextMenu("Clear Save Data")]
    public void ClearSaveData()
    {
        ChunkPersistence.DeleteWorld(worldName);

        if (PlacedStructureManager.Instance != null)
            PlacedStructureManager.Instance.ClearAll(worldName);

        Debug.Log($"[SaveSystemManager] Cleared save data for world '{worldName}'");
    }

    /// <summary>
    /// Full filesystem wipe — same as the permadeath path. Use this from the
    /// editor when you want a clean slate (kills inventory/player/time/metadata
    /// files that the partial 'Clear Save Data' leaves behind).
    /// </summary>
    [ContextMenu("Delete World Folder (Full Wipe)")]
    public void EditorDeleteWorldFolder()
    {
        DeleteWorld(worldName);
    }
}