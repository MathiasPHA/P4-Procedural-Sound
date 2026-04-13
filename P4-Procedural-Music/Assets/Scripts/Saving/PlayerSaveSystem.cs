using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Saves and loads the player's world position and happiness value.
/// Attach to the player GameObject alongside ComfortSystem.
/// </summary>
public class PlayerSaveSystem : MonoBehaviour
{
    public static PlayerSaveSystem Instance { get; private set; }

    [Tooltip("The player's Transform to save/restore position from.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("The ComfortSystem to save/restore happiness from.")]
    [SerializeField] private ComfortSystem comfortSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Fall back to this GameObject's own components if not assigned
        if (playerTransform == null)
            playerTransform = transform;

        if (comfortSystem == null)
            comfortSystem = GetComponent<ComfortSystem>();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void SavePlayer(string worldName)
    {
        // Don't overwrite overworld position while in a dungeon
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        if (playerTransform == null)
        {
            Debug.LogWarning("[PlayerSaveSystem] No player Transform — nothing to save.");
            return;
        }

        var data = new PlayerSaveData
        {
            positionX = playerTransform.position.x,
            positionY = playerTransform.position.y,
            happiness = comfortSystem != null ? comfortSystem.Happiness : 0.5f
        };

        File.WriteAllText(GetSavePath(worldName), JsonUtility.ToJson(data, prettyPrint: true));
        Debug.Log($"[PlayerSaveSystem] Saved player at {playerTransform.position} for world '{worldName}'.");
    }

    public void LoadPlayer(string worldName)
    {
        // Don't restore overworld position when in a dungeon
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        string path = GetSavePath(worldName);
        if (!File.Exists(path))
        {
            Debug.Log($"[PlayerSaveSystem] No player save found for '{worldName}' — using spawn position.");
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

        if (comfortSystem != null)
            comfortSystem.SetHappiness(data.happiness);

        Debug.Log($"[PlayerSaveSystem] Loaded player at ({data.positionX}, {data.positionY}), happiness {data.happiness:F2}.");
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
    }
}