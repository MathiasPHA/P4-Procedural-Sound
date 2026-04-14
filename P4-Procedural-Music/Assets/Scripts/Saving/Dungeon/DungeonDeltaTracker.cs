using InventorySystem.Building;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that lives in the dungeon scene.
/// Collects removals and placements during the visit,
/// then saves to disk when the player exits.
///
/// Setup: Add to the same GameObject as DungeonGenerator.
/// </summary>
public class DungeonDeltaTracker : MonoBehaviour
{
    public static DungeonDeltaTracker Instance { get; private set; }

    private DungeonSaveData _data = new DungeonSaveData();
    private int _seed;
    private string _worldName;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Call after dungeon generation to initialize tracking.
    /// Loads existing delta if the player has visited before.
    /// </summary>
    public DungeonSaveData Initialize(int seed)
    {
        _seed = seed;
        _worldName = GameSettings.Instance != null ? GameSettings.Instance.worldName : "default";

        var existing = DungeonPersistence.Load(_worldName, _seed);
        if (existing != null)
            _data = existing;

        return _data;
    }

    /// <summary>
    /// Re-place structures saved from a previous visit.
    /// Call after Initialize, once PlacementSystem is ready.
    /// </summary>
    public void RePlaceStructures()
    {
        if (_data.placedStructures.Count == 0) return;

        var placementSystem = InventorySystem.Building.PlacementSystem.Instance;
        if (placementSystem == null)
        {
            Debug.LogWarning("[DungeonDeltaTracker] PlacementSystem not found — can't re-place structures.");
            return;
        }

        int placed = 0;
        foreach (var entry in _data.placedStructures)
        {
            var placeableData = placementSystem.GetPlaceableByItemId(entry.itemId);
            if (placeableData == null || placeableData.prefab == null)
            {
                Debug.LogWarning($"[DungeonDeltaTracker] No prefab found for item '{entry.itemId}', skipping.");
                continue;
            }

            var go = Object.Instantiate(
                placeableData.prefab,
                new Vector3(entry.x, entry.y, 0f),
                Quaternion.identity);

            var structure = go.GetComponent<PlacedStructure>();
            if (structure != null)
                structure.sourceData = placeableData;

            placed++;
        }

        if (placed > 0)
            Debug.Log($"[DungeonDeltaTracker] Re-placed {placed} structures from previous visit.");
    }

    /// <summary>
    /// Called by DungeonSpawnedObject.OnDestroy when a spawned object is removed.
    /// </summary>
    public static void RecordRemoval(int spawnIndex)
    {
        if (Instance == null) return;
        if (!Instance._data.removedIndices.Contains(spawnIndex))
            Instance._data.removedIndices.Add(spawnIndex);
    }

    /// <summary>
    /// Call when the player places a structure in the dungeon.
    /// </summary>
    public static void RecordPlacement(string itemId, Vector3 position)
    {
        if (Instance == null) return;
        Instance._data.placedStructures.Add(new DungeonPlacedEntry
        {
            itemId = itemId,
            x = position.x,
            y = position.y
        });
    }

    /// <summary>
    /// Call when a player-placed structure is removed in the dungeon.
    /// </summary>
    public static void RemovePlacement(Vector3 position)
    {
        if (Instance == null) return;
        Instance._data.placedStructures.RemoveAll(
            p => Mathf.Approximately(p.x, position.x) &&
                 Mathf.Approximately(p.y, position.y));
    }

    /// <summary>
    /// Save the current delta to disk.
    /// </summary>
    public void SaveDelta()
    {
        // Only save if something changed
        if (_data.removedIndices.Count == 0 && _data.placedStructures.Count == 0)
            return;

        DungeonPersistence.Save(_worldName, _seed, _data);
        Debug.Log($"[DungeonDeltaTracker] Saved delta: {_data.removedIndices.Count} removed, {_data.placedStructures.Count} placed.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}