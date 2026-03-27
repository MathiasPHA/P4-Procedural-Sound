using InventorySystem;
using InventorySystem.Data;
using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Handles saving and loading the player inventory to disk.
/// Wraps Inventory.ToSaveData() / LoadFromSaveData() with file I/O.
/// </summary>
public class InventorySaveSystem : MonoBehaviour
{
    public static InventorySaveSystem Instance { get; private set; }

    [Tooltip("The ItemDatabase asset. Must be the same one used by InventoryBootstrap.")]
    [SerializeField] private ItemDatabase itemDatabase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        itemDatabase?.Initialise();
    }

    public void SaveInventory(string worldName)
    {
        var inventory = InventoryBootstrap.PlayerInventory;
        if (inventory == null)
        {
            Debug.LogWarning("[InventorySaveSystem] PlayerInventory is null — nothing to save.");
            return;
        }

        // Wrap in a class so JsonUtility serializes the nested array correctly
        var wrapper = new SaveWrapper { data = inventory.ToSaveData() };
        File.WriteAllText(GetSavePath(worldName), JsonUtility.ToJson(wrapper, prettyPrint: true));
        Debug.Log($"[InventorySaveSystem] Inventory saved for world '{worldName}'.");
    }

    public void LoadInventory(string worldName)
    {
        var inventory = InventoryBootstrap.PlayerInventory;
        if (inventory == null)
        {
            Debug.LogWarning("[InventorySaveSystem] PlayerInventory is null — cannot load.");
            return;
        }

        string path = GetSavePath(worldName);
        if (!File.Exists(path))
        {
            Debug.Log($"[InventorySaveSystem] No inventory save found for '{worldName}' — starting fresh.");
            return;
        }

        var wrapper = JsonUtility.FromJson<SaveWrapper>(File.ReadAllText(path));
        if (wrapper == null)
        {
            Debug.LogWarning("[InventorySaveSystem] Inventory save data was empty or corrupt.");
            return;
        }

        inventory.LoadFromSaveData(wrapper.data, itemDatabase);
        Debug.Log($"[InventorySaveSystem] Inventory loaded for world '{worldName}'.");
    }

    private static string GetSavePath(string worldName)
    {
        string dir = Path.Combine(Application.persistentDataPath, "worlds", worldName);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "inventory.json");
    }

    /// <summary>
    /// Class wrapper around InventorySaveData so JsonUtility
    /// correctly serializes the nested slots array.
    /// </summary>
    [Serializable]
    private class SaveWrapper
    {
        public InventorySaveData data;
    }
}