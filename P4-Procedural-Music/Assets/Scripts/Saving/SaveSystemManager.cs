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
}