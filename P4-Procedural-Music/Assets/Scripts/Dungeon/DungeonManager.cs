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
        IsInDungeon = true;

        // Force-save overworld before leaving
        if (SaveSystemManager.Instance != null)
            SaveSystemManager.Instance.SaveModifiedChunks();

        SceneManager.LoadScene(dungeonSceneName);
    }

    /// <summary>
    /// Called by DungeonExit.Interact().
    /// Returns the player to the overworld at their saved position.
    /// </summary>
    public void ExitDungeon()
    {
        IsInDungeon = false;
        ActiveConfig = null;

        SceneManager.sceneLoaded += OnOverworldLoaded;
        SceneManager.LoadScene(returnSceneName);
    }

    private void OnOverworldLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnOverworldLoaded;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            player.transform.position = returnPlayerPosition;
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
