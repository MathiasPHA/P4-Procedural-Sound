using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton (same pattern as GameSettings).
/// Carries dungeon config between overworld and dungeon scenes.
/// Place on a GameObject in your main menu or boot scene.
/// </summary>
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string dungeonSceneName = "DungeonScene";

    // Runtime state — not serialized in Inspector
    public DungeonConfig ActiveConfig { get; private set; }
    public int DungeonSeed { get; private set; }
    public bool IsInDungeon { get; private set; }

    private string returnSceneName;
    private Vector3 returnPlayerPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Called by DungeonEntrance.Interact().
    /// Saves return info and loads the dungeon scene.
    /// </summary>
    public void EnterDungeon(DungeonConfig config, Vector3 entranceWorldPos, Vector3 playerPos)
    {
        ActiveConfig = config;
        DungeonSeed = GenerateSeed(entranceWorldPos);
        returnSceneName = SceneManager.GetActiveScene().name;
        returnPlayerPosition = playerPos;

        // Force-save overworld BEFORE setting IsInDungeon,
        // otherwise PlayerSaveSystem skips the position save
        if (SaveSystemManager.Instance != null)
            SaveSystemManager.Instance.SaveModifiedChunks();

        IsInDungeon = true;

        SceneManager.sceneLoaded += OnDungeonLoaded;
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadScene(dungeonSceneName);
        else
            SceneManager.LoadScene(dungeonSceneName); // fallback if transition prefab missing
    }

    /// <summary>
    /// Called by DungeonExit.Interact().
    /// Returns the player to the overworld at their saved position.
    /// </summary>
    public void ExitDungeon()
    {
        // Save dungeon changes (removed objects, placed structures)
        if (DungeonDeltaTracker.Instance != null)
            DungeonDeltaTracker.Instance.SaveDelta();

        // Save inventory so dungeon loot carries back
        SaveInventory();

        // Keep IsInDungeon true until OnOverworldLoaded —
        // prevents PlayerSaveSystem from overwriting the return position
        ActiveConfig = null;

        SceneManager.sceneLoaded += OnOverworldLoaded;
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadScene(returnSceneName);
        else
            SceneManager.LoadScene(returnSceneName); // fallback if transition prefab missing
    }

    private void OnDungeonLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnDungeonLoaded;

        // Restore inventory into the dungeon scene's player
        LoadInventory();
    }

    private void OnOverworldLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnOverworldLoaded;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            player.transform.position = returnPlayerPosition;

        // Restore inventory (with any dungeon loot) into the overworld player
        LoadInventory();

        // Now safe to clear dungeon state
        IsInDungeon = false;
    }

    // -------------------------------------------------------------------------
    // Inventory helpers — uses InventorySaveSystem if available,
    // waits one frame so scene singletons have initialized.
    // -------------------------------------------------------------------------

    private void SaveInventory()
    {
        string worldName = GameSettings.Instance != null ? GameSettings.Instance.worldName : "default";

        if (InventorySaveSystem.Instance != null)
            InventorySaveSystem.Instance.SaveInventory(worldName);
    }

    private void LoadInventory()
    {
        StartCoroutine(LoadInventoryNextFrame());
    }

    private System.Collections.IEnumerator LoadInventoryNextFrame()
    {
        // Wait a frame so the new scene's InventorySaveSystem.Awake() has run
        yield return null;

        string worldName = GameSettings.Instance != null ? GameSettings.Instance.worldName : "default";

        if (InventorySaveSystem.Instance != null)
            InventorySaveSystem.Instance.LoadInventory(worldName);
        else
            Debug.LogWarning("[DungeonManager] InventorySaveSystem not found in scene — inventory not loaded.");
    }

    /// <summary>
    /// Deterministic seed: same cave entrance in the same world = same dungeon.
    /// </summary>
    private int GenerateSeed(Vector3 entranceWorldPos)
    {
        int worldSeed = GameSettings.Instance != null ? GameSettings.Instance.seed : 0;
        int hash = 17;
        hash = hash * 31 + worldSeed;
        hash = hash * 31 + Mathf.RoundToInt(entranceWorldPos.x * 100);
        hash = hash * 31 + Mathf.RoundToInt(entranceWorldPos.y * 100);
        return hash;
    }
}