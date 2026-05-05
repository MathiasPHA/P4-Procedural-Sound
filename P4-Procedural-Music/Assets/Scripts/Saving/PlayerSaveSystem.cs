using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Saves and loads the player's world position, happiness, hunger, and mood.
/// Attach to the player GameObject alongside HappinessSystem, HungerSystem, MoodSystem.
///
/// Save data is backwards-compatible: old saves without hunger/mood fields
/// will load with safe defaults (hunger = 0.8, mood = 0.6).
/// </summary>
public class PlayerSaveSystem : MonoBehaviour
{
    public static PlayerSaveSystem Instance { get; private set; }

    /// <summary>
    /// Fires after LoadPlayer finishes successfully applying saved state to the player.
    /// Subscribers can read playerTransform.position safely from here.
    /// Does NOT fire if no save exists, the save is corrupt, or load is skipped (e.g. in a dungeon).
    /// </summary>
    public static event Action OnPlayerLoaded;

    [Tooltip("The player's Transform to save/restore position from.")]
    [SerializeField] private Transform playerTransform;

    private HappinessSystem happinessSystem;
    private HungerSystem hungerSystem;
    private MoodSystem moodSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerTransform == null)
            playerTransform = transform;
    }

    private void Start()
    {
        happinessSystem = HappinessSystem.Instance;
        if (happinessSystem == null)
            happinessSystem = FindObjectOfType<HappinessSystem>();

        hungerSystem = HungerSystem.Instance;
        if (hungerSystem == null)
            hungerSystem = FindObjectOfType<HungerSystem>();

        moodSystem = MoodSystem.Instance;
        if (moodSystem == null)
            moodSystem = FindObjectOfType<MoodSystem>();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void SavePlayer(string worldName)
    {
        bool inDungeon = DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon;
        string path = GetSavePath(worldName);
        PlayerSaveData data;

        if (inDungeon)
        {
            // In a dungeon: preserve the saved overworld position but update stats.
            data = File.Exists(path)
                ? JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path)) ?? new PlayerSaveData()
                : new PlayerSaveData();
        }
        else
        {
            if (playerTransform == null)
            {
                Debug.LogWarning("[PlayerSaveSystem] No player Transform — nothing to save.");
                return;
            }
            data = new PlayerSaveData
            {
                positionX = playerTransform.position.x,
                positionY = playerTransform.position.y,
            };
        }

        data.happiness = happinessSystem != null ? happinessSystem.Happiness : 0.5f;
        data.hunger = hungerSystem != null ? hungerSystem.Hunger : 0.8f;
        data.mood = moodSystem != null ? moodSystem.Mood : 0.6f;

        File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: true));
        Debug.Log($"[PlayerSaveSystem] Saved player (inDungeon={inDungeon}) " +
                  $"happiness={data.happiness:F2}, hunger={data.hunger:F2}, mood={data.mood:F2}");
    }

    public void LoadPlayer(string worldName)
    {
        // Don't restore overworld position when in a dungeon
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        string path = GetSavePath(worldName);
        if (!File.Exists(path))
        {
            Debug.Log($"[PlayerSaveSystem] No player save found for '{worldName}' — using defaults.");
            return;
        }

        var data = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
        if (data == null)
        {
            Debug.LogWarning("[PlayerSaveSystem] Player save data was corrupt.");
            return;
        }

        if (playerTransform != null)
            playerTransform.position = new Vector3(data.positionX, data.positionY, 0f);

        if (happinessSystem != null)
            happinessSystem.SetHappiness(data.happiness);

        if (hungerSystem != null)
            hungerSystem.SetHunger(data.hunger);

        if (moodSystem != null)
            moodSystem.SetMood(data.mood);

        Debug.Log($"[PlayerSaveSystem] Loaded player at ({data.positionX:F1}, {data.positionY:F1}), " +
                  $"happiness={data.happiness:F2}, hunger={data.hunger:F2}, mood={data.mood:F2}");

        OnPlayerLoaded?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Internal
    // -------------------------------------------------------------------------

    private static string GetSavePath(string worldName)
    {
        string dir = Path.Combine(Application.persistentDataPath, "worlds", worldName);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "player.json");
    }

    // -------------------------------------------------------------------------
    // Data
    // -------------------------------------------------------------------------

    [Serializable]
    private class PlayerSaveData
    {
        public float positionX;
        public float positionY;
        public float happiness;
        // New fields — default values ensure old saves load cleanly
        public float hunger = 0.8f;
        public float mood = 0.6f;
    }
}