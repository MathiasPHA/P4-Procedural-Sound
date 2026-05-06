using System.Collections;
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

    /// <summary>
    /// GLOBAL kill switch for the entire save layer. Once true, NO save
    /// subsystem should write to disk — they bail at the top of every save
    /// method with `if (SaveSystemManager.IsSavingDisabled) return;`.
    /// Set automatically by DeleteCurrentWorld().
    /// </summary>
    public static bool IsSavingDisabled { get; private set; }

    private float _saveTimer;
    private bool _worldDeleted;

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Static fields persist across play-mode entries in the editor — reset.
        IsSavingDisabled = false;

        if (GameSettings.Instance != null)
            worldName = GameSettings.Instance.worldName;
        else
            Debug.LogWarning("[SaveSystemManager] GameSettings not found — using fallback worldName.");
    }

    private void Start()
    {
        // Re-enable any subsystems that may have been disabled by a previous
        // session's permadeath wipe. DontDestroyOnLoad singletons (DayNightMaster,
        // etc.) survive scene loads with enabled=false if ShutdownSaveSubsystems()
        // was called — they must be re-enabled here before we load from them.
        ReenableSaveSubsystems();

        if (InventorySaveSystem.Instance != null)
            InventorySaveSystem.Instance.LoadInventory(worldName);
        else
            Debug.LogWarning("[SaveSystemManager] InventorySaveSystem not found — inventory not loaded.");

        if (PlayerSaveSystem.Instance != null)
            PlayerSaveSystem.Instance.LoadPlayer(worldName);
        else
            Debug.LogWarning("[SaveSystemManager] PlayerSaveSystem not found — player not loaded.");

        if (DayNightMaster.Instance != null)
            DayNightMaster.Instance.LoadTime(worldName);
    }

    private void Update()
    {
        if (IsSavingDisabled) return;

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
        if (IsSavingDisabled) return;
        SaveModifiedChunks();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && !IsSavingDisabled)
            SaveModifiedChunks();
    }

    // ───────────────────────── Saving ─────────────────────────

    public void SaveModifiedChunks()
    {
        if (IsSavingDisabled || _worldDeleted) return;

        if (ChunkManager.Instance != null)
        {
            ChunkPersistence.SaveModifiedChunks(
                ChunkManager.Instance.LoadedChunks,
                ChunkManager.Instance.LoadedObjects,
                worldName
            );
        }

        if (InventorySaveSystem.Instance != null)
            InventorySaveSystem.Instance.SaveInventory(worldName);
        else
            Debug.LogWarning("[SaveSystemManager] InventorySaveSystem not found — inventory not saved.");

        if (PlayerSaveSystem.Instance != null)
            PlayerSaveSystem.Instance.SavePlayer(worldName);
        else
            Debug.LogWarning("[SaveSystemManager] PlayerSaveSystem not found — player not saved.");

        if (PlacedStructureManager.Instance != null)
            PlacedStructureManager.Instance.SaveAll();

        if (DayNightMaster.Instance != null)
            DayNightMaster.Instance.SaveTime(worldName);

        if (GameSettings.Instance != null)
            GameSettings.Instance.UpdateLastPlayed(worldName);
    }

    // ───────────────────────── Permadeath ─────────────────────────

    /// <summary>
    /// Permadeath wipe. Order matters:
    ///   1. Trip the global IsSavingDisabled gate FIRST so any save call
    ///      mid-deletion bails before writing.
    ///   2. Disable every save-subsystem MonoBehaviour we know about — stops
    ///      their Update loops and any timed/queued saves.
    ///   3. Delete the world folder from disk.
    ///   4. Watch the folder for ~2 seconds. If anything re-creates a file
    ///      inside it, we log the filename so we can hunt down which
    ///      subsystem is bypassing the gate.
    /// </summary>
    public bool DeleteCurrentWorld()
    {
        Debug.Log("[SaveSystemManager] PERMADEATH — wiping world.");

        // 1. GLOBAL gate up.
        IsSavingDisabled = true;
        _worldDeleted = true;

        // 2. Aggressively shut down every save-related component.
        ShutdownSaveSubsystems();

        // 3. Delete the folder.
        bool deleted = DeleteWorld(worldName);

        // 4. Monitor for resurrection.
        if (deleted)
            StartCoroutine(MonitorForResurrection(worldName, watchSeconds: 2f));

        return deleted;
    }

    /// <summary>
    /// Disables every save subsystem component we can reach. This stops their
    /// Update loops, but does NOT stop someone from calling their public Save
    /// methods directly — that's what the IsSavingDisabled gate is for.
    /// </summary>
    private void ShutdownSaveSubsystems()
    {
        if (InventorySaveSystem.Instance != null)
            InventorySaveSystem.Instance.enabled = false;

        if (PlayerSaveSystem.Instance != null)
            PlayerSaveSystem.Instance.enabled = false;

        if (PlacedStructureManager.Instance != null)
            PlacedStructureManager.Instance.enabled = false;

        if (DayNightMaster.Instance != null)
            DayNightMaster.Instance.enabled = false;

        if (ChunkManager.Instance != null)
            ChunkManager.Instance.enabled = false;

        autoSaveInterval = 0f;
    }

    /// <summary>
    /// Re-enables any subsystems that ShutdownSaveSubsystems() disabled during
    /// a previous session's permadeath wipe. DontDestroyOnLoad singletons
    /// (DayNightMaster, etc.) survive scene loads and will still be disabled
    /// when the player starts a new game — this is the counterpart that brings
    /// them back to life at the start of each new game scene.
    /// </summary>
    private void ReenableSaveSubsystems()
    {
        // Only DontDestroyOnLoad objects need re-enabling here — scene-local
        // objects (InventorySaveSystem, PlayerSaveSystem, PlacedStructureManager,
        // ChunkManager) are freshly instantiated each scene load, so they always
        // start enabled. We only need to repair persistent singletons.

        if (DayNightMaster.Instance != null && !DayNightMaster.Instance.enabled)
        {
            DayNightMaster.Instance.enabled = true;
            Debug.Log("[SaveSystemManager] Re-enabled DayNightMaster after permadeath.");
        }
    }

    /// <summary>
    /// Watches the world folder after deletion. If a file shows up, we know
    /// some subsystem bypassed the IsSavingDisabled gate and we log which
    /// file came back so we can patch that subsystem.
    /// </summary>
    private IEnumerator MonitorForResurrection(string watchedWorld, float watchSeconds)
    {
        string fullPath = Path.Combine(Application.persistentDataPath, "worlds", watchedWorld);
        float elapsed = 0f;
        bool reportedRecreation = false;

        while (elapsed < watchSeconds)
        {
            if (!reportedRecreation && Directory.Exists(fullPath))
            {
                reportedRecreation = true;
                Debug.LogError(
                    $"[SaveSystemManager] !!! World folder was RECREATED after deletion: {fullPath}\n" +
                    "Something is bypassing the IsSavingDisabled gate. Files inside:");

                foreach (var file in Directory.GetFiles(fullPath))
                    Debug.LogError($"   ↳ {Path.GetFileName(file)}");
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!reportedRecreation)
            Debug.Log($"[SaveSystemManager] Permadeath wipe confirmed clean after {watchSeconds:F1}s.");
    }

    public static bool DeleteWorld(string targetWorldName)
    {
        if (string.IsNullOrWhiteSpace(targetWorldName))
        {
            Debug.LogWarning("[SaveSystemManager] Cannot delete world — name is empty.");
            return false;
        }

        if (targetWorldName.Contains("..") ||
            targetWorldName.Contains("/") ||
            targetWorldName.Contains("\\"))
        {
            Debug.LogError($"[SaveSystemManager] Refusing suspicious world name: '{targetWorldName}'");
            return false;
        }

        string worldsRoot = Path.Combine(Application.persistentDataPath, "worlds");
        string worldPath = Path.Combine(worldsRoot, targetWorldName);

        string fullWorldPath = Path.GetFullPath(worldPath);
        string fullRoot = Path.GetFullPath(worldsRoot);
        if (!fullWorldPath.StartsWith(fullRoot))
        {
            Debug.LogError($"[SaveSystemManager] Path escape blocked: {fullWorldPath}");
            return false;
        }

        // Log the path EVERY time so we can verify it's the right one.
        Debug.Log($"[SaveSystemManager] Attempting to delete: {fullWorldPath}");

        if (!Directory.Exists(fullWorldPath))
        {
            Debug.LogWarning(
                $"[SaveSystemManager] World folder not found at {fullWorldPath} — " +
                "either the path is wrong or the world was never saved. " +
                $"Look in: {Application.persistentDataPath}");
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
            Debug.LogError(
                $"[SaveSystemManager] Failed to delete '{targetWorldName}': {e.Message}\n" +
                "If this is a sharing-violation error, a file is still open. " +
                "One of the save subsystems likely holds a FileStream — close it before deletion.");
            return false;
        }
    }

    // ───────────────────────── Editor Tools ─────────────────────────

    [ContextMenu("Clear Save Data")]
    public void ClearSaveData()
    {
        ChunkPersistence.DeleteWorld(worldName);

        if (PlacedStructureManager.Instance != null)
            PlacedStructureManager.Instance.ClearAll(worldName);

        Debug.Log($"[SaveSystemManager] Cleared save data for world '{worldName}'");
    }

    [ContextMenu("Delete World Folder (Full Wipe)")]
    public void EditorDeleteWorldFolder()
    {
        DeleteWorld(worldName);
    }

    [ContextMenu("Print Persistent Data Path")]
    public void PrintPersistentDataPath()
    {
        Debug.Log($"[SaveSystemManager] persistentDataPath = {Application.persistentDataPath}");
        Debug.Log($"[SaveSystemManager] worlds folder      = {Path.Combine(Application.persistentDataPath, "worlds")}");
    }
}