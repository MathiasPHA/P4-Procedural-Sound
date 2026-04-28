using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using InventorySystem.Building;

namespace ProceduralTerrain
{
    /// <summary>
    /// Manages persistence for player-placed structures (campfires, workbenches, etc.).
    ///
    /// Works alongside ChunkManager — structures are tracked per chunk coordinate
    /// so they load/unload with terrain. Uses its own save file (world_structures.json)
    /// to keep things self-contained without modifying existing ChunkPersistence.
    ///
    /// SETUP:
    ///   1. Attach to the same GameObject as ChunkManager (or a child).
    ///   2. Drag all your PlaceableData assets into the placeableDatabase list
    ///      (same list as on PlacementSystem — these are needed to look up prefabs on load).
    ///   3. That's it. PlacementSystem calls RegisterStructure() automatically.
    ///
    /// SAVE/LOAD FLOW:
    ///   - SaveAll() is called by SaveSystemManager during auto-save / quit / pause.
    ///   - When a chunk loads, ChunkManager calls LoadStructuresForChunk(coord).
    ///   - When a chunk unloads, ChunkManager calls UnloadStructuresForChunk(coord).
    ///
    /// HP PERSISTENCE:
    ///   PlacedStructure.CurrentHealth is captured at save time and restored on load
    ///   via PlacedStructure.RestoreHealth(int). Stored on the new health field of
    ///   StructureSaveData (0 = unset / load at full HP, backward compatible).
    /// </summary>
    public class PlacedStructureManager : MonoBehaviour
    {
        public static PlacedStructureManager Instance { get; private set; }

        [Header("Placeable Database")]
        [Tooltip("All PlaceableData assets — needed to look up prefabs when loading saved structures.")]
        [SerializeField] private List<PlaceableData> placeableDatabase = new();

        // =====================================================================
        // Runtime tracking
        // =====================================================================

        // Structures currently instantiated in loaded chunks
        private Dictionary<Vector2Int, List<PlacedStructure>> _activeStructures = new();

        // Full save data (includes unloaded chunks too)
        private Dictionary<Vector2Int, List<StructureSaveData>> _savedStructures = new();

        // Lookup: item ID → PlaceableData (for spawning on load)
        private Dictionary<string, PlaceableData> _placeableLookup = new();

        private const string SAVE_FILENAME = "world_structures.json";

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Build lookup
            foreach (var p in placeableDatabase)
            {
                if (p == null || p.item == null) continue;
                _placeableLookup[p.item.id] = p;
            }

            // Load saved structure data into memory in Awake (not Start) so that
            // ChunkManager.Start can call LoadStructuresForChunk for the initial
            // chunks around the player and actually find data. Otherwise, on a
            // scene reload (e.g. exiting a cave), the initial chunks finish
            // loading before _savedStructures is populated and silently spawn
            // nothing — only later chunks (loaded by walking) get their structures.
            LoadAllFromDisk();
        }

        // =====================================================================
        // Public API — called by PlacementSystem and ChunkManager
        // =====================================================================

        /// <summary>
        /// Register a newly placed structure. Called by PlacementSystem after instantiation.
        /// </summary>
        public void RegisterStructure(PlacedStructure structure)
        {
            if (structure == null || structure.sourceData == null)
            {
                Debug.LogWarning("[PlacedStructureManager] Cannot register structure — missing sourceData.");
                return;
            }

            Vector2Int chunkCoord = GetChunkCoord(structure.transform.position);

            // Add to active tracking
            if (!_activeStructures.ContainsKey(chunkCoord))
                _activeStructures[chunkCoord] = new List<PlacedStructure>();

            _activeStructures[chunkCoord].Add(structure);

            // Add to save data
            if (!_savedStructures.ContainsKey(chunkCoord))
                _savedStructures[chunkCoord] = new List<StructureSaveData>();

            _savedStructures[chunkCoord].Add(new StructureSaveData(
                structure.sourceData.item.id,
                structure.transform.position
            ));

            Debug.Log($"[PlacedStructureManager] Registered {structure.sourceData.item.id} in chunk {chunkCoord}");
        }

        /// <summary>
        /// Unregister a structure (e.g. if the player demolishes it).
        /// Removes from both active tracking and save data.
        /// </summary>
        public void UnregisterStructure(PlacedStructure structure)
        {
            if (structure == null) return;

            Vector2Int chunkCoord = GetChunkCoord(structure.transform.position);

            // Remove from active list
            if (_activeStructures.TryGetValue(chunkCoord, out var activeList))
                activeList.Remove(structure);

            // Remove matching entry from save data
            if (_savedStructures.TryGetValue(chunkCoord, out var saveList))
            {
                string itemId = structure.sourceData != null ? structure.sourceData.item.id : "";
                Vector3 pos = structure.transform.position;

                saveList.RemoveAll(s =>
                    s.itemId == itemId &&
                    Mathf.Approximately(s.posX, pos.x) &&
                    Mathf.Approximately(s.posY, pos.y)
                );
            }
        }

        /// <summary>
        /// Spawn all saved structures for a chunk that just loaded.
        /// Called by ChunkManager during chunk loading.
        /// </summary>
        public void LoadStructuresForChunk(Vector2Int chunkCoord)
        {
            if (!_savedStructures.TryGetValue(chunkCoord, out var structures))
                return;

            if (structures.Count == 0)
                return;

            if (!_activeStructures.ContainsKey(chunkCoord))
                _activeStructures[chunkCoord] = new List<PlacedStructure>();

            foreach (var data in structures)
            {
                SpawnStructureFromSave(data, chunkCoord);
            }

            Debug.Log($"[PlacedStructureManager] Loaded {structures.Count} structures for chunk {chunkCoord}");
        }

        /// <summary>
        /// Clean up structures in a chunk that's being unloaded.
        /// Save data is preserved — only the GameObjects are destroyed.
        /// </summary>
        public void UnloadStructuresForChunk(Vector2Int chunkCoord)
        {
            if (!_activeStructures.TryGetValue(chunkCoord, out var structures))
                return;

            foreach (var s in structures)
            {
                if (s != null)
                    Destroy(s.gameObject);
            }

            _activeStructures.Remove(chunkCoord);
        }

        /// <summary>
        /// Update the saved state JSON for a single structure, in-place.
        /// Called by stateful structures (e.g. CampfireController) from OnDestroy
        /// so their state survives chunk unload between auto-saves.
        ///
        /// Safer than syncing the whole chunk during teardown — only touches the
        /// one entry, can't clobber sibling structures whose GameObjects may
        /// already be destroyed.
        /// </summary>
        public void UpdateStructureState(PlacedStructure structure, string stateJson)
        {
            if (structure == null || structure.sourceData == null || structure.sourceData.item == null)
                return;

            Vector2Int chunkCoord = GetChunkCoord(structure.transform.position);
            if (!_savedStructures.TryGetValue(chunkCoord, out var saveList))
                return;

            string itemId = structure.sourceData.item.id;
            Vector3 pos = structure.transform.position;

            // Find the matching save entry by id + position and update its stateJson.
            for (int i = 0; i < saveList.Count; i++)
            {
                var entry = saveList[i];
                if (entry.itemId == itemId &&
                    Mathf.Approximately(entry.posX, pos.x) &&
                    Mathf.Approximately(entry.posY, pos.y))
                {
                    entry.stateJson = stateJson ?? "";
                    return;
                }
            }
        }

        /// <summary>
        /// Save all structure data to disk.
        /// Called by SaveSystemManager during auto-save / quit / pause.
        /// </summary>
        public void SaveAll()
        {
            string worldName = SaveSystemManager.Instance != null
                ? SaveSystemManager.Instance.worldName
                : "default";

            SyncActiveToSaveData();
            SaveToDisk(worldName);
        }

        /// <summary>
        /// Clear all saved structures (used when deleting a world).
        /// </summary>
        public void ClearAll(string worldName = null)
        {
            if (worldName == null)
            {
                worldName = SaveSystemManager.Instance != null
                    ? SaveSystemManager.Instance.worldName
                    : "default";
            }

            _savedStructures.Clear();
            _activeStructures.Clear();

            string path = GetSavePath(worldName);
            if (File.Exists(path))
                File.Delete(path);

            Debug.Log("[PlacedStructureManager] Cleared all structure save data.");
        }

        // =====================================================================
        // Spawning
        // =====================================================================

        private void SpawnStructureFromSave(StructureSaveData data, Vector2Int chunkCoord)
        {
            if (!_placeableLookup.TryGetValue(data.itemId, out var placeableData))
            {
                Debug.LogWarning($"[PlacedStructureManager] Unknown item ID '{data.itemId}' — " +
                                 "make sure it's in the placeableDatabase list.");
                return;
            }

            if (placeableData.prefab == null)
            {
                Debug.LogWarning($"[PlacedStructureManager] PlaceableData for '{data.itemId}' has no prefab.");
                return;
            }

            Vector3 pos = data.GetPosition();
            var go = Instantiate(placeableData.prefab, pos, Quaternion.identity);

            var structure = go.GetComponent<PlacedStructure>();
            if (structure == null)
                structure = go.AddComponent<PlacedStructure>();

            structure.sourceData = placeableData;
            structure.wasSaveLoaded = true; // Flag so it doesn't re-register

            // Restore HP. 0 = unset (load at full HP). Runs after Awake so sourceData
            // is now assigned and PlacedStructure.RestoreHealth can pull maxHealth.
            structure.RestoreHealth(data.health);

            // Restore optional per-instance runtime state (campfire fuel, etc.)
            // Runs after Awake but before the first Start, so the structure boots
            // directly into its saved state — no extinguish-sound flash on load.
            if (!string.IsNullOrEmpty(data.stateJson))
            {
                var persistent = go.GetComponent<IPersistentStructureState>();
                if (persistent != null)
                    persistent.DeserializeState(data.stateJson);
            }

            _activeStructures[chunkCoord].Add(structure);
        }

        // =====================================================================
        // Serialization
        // =====================================================================

        [Serializable]
        private class WorldStructureSaveData
        {
            public List<ChunkStructureEntry> chunks = new();
        }

        [Serializable]
        private class ChunkStructureEntry
        {
            public int chunkX;
            public int chunkY;
            public List<StructureSaveData> structures = new();
        }

        private void SyncActiveToSaveData()
        {
            // Rebuild save data for loaded chunks from live GameObjects
            foreach (var kvp in _activeStructures)
            {
                var coord = kvp.Key;
                var liveList = kvp.Value;

                // Remove destroyed structures
                liveList.RemoveAll(s => s == null);

                // Rebuild save entries for this chunk
                var saveEntries = new List<StructureSaveData>();
                foreach (var s in liveList)
                {
                    if (s.sourceData == null || s.sourceData.item == null) continue;

                    // Capture optional per-instance runtime state (campfire fuel, etc.)
                    string stateJson = "";
                    var persistent = s.GetComponent<IPersistentStructureState>();
                    if (persistent != null)
                        stateJson = persistent.SerializeState() ?? "";

                    // Capture HP. Demolished structures (HP=0) never reach here because
                    // Demolish() unregisters them, but guard anyway.
                    int hp = s.CurrentHealth;
                    if (hp <= 0) hp = s.MaxHealth;

                    saveEntries.Add(new StructureSaveData(
                        s.sourceData.item.id,
                        s.transform.position,
                        stateJson,
                        hp
                    ));
                }

                _savedStructures[coord] = saveEntries;
            }
        }

        private void SaveToDisk(string worldName)
        {
            var saveData = new WorldStructureSaveData();

            foreach (var kvp in _savedStructures)
            {
                if (kvp.Value.Count == 0) continue;

                saveData.chunks.Add(new ChunkStructureEntry
                {
                    chunkX = kvp.Key.x,
                    chunkY = kvp.Key.y,
                    structures = kvp.Value
                });
            }

            if (saveData.chunks.Count == 0)
            {
                // Delete file if nothing to save
                string path = GetSavePath(worldName);
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            string json = JsonUtility.ToJson(saveData, prettyPrint: false);
            File.WriteAllText(GetSavePath(worldName), json);
            Debug.Log($"[PlacedStructureManager] Saved {saveData.chunks.Count} chunks with structures.");
        }

        private void LoadAllFromDisk()
        {
            string worldName = SaveSystemManager.Instance != null
                ? SaveSystemManager.Instance.worldName
                : "default";

            string path = GetSavePath(worldName);

            if (!File.Exists(path))
                return;

            try
            {
                string json = File.ReadAllText(path);
                var saveData = JsonUtility.FromJson<WorldStructureSaveData>(json);

                if (saveData?.chunks == null) return;

                foreach (var entry in saveData.chunks)
                {
                    var coord = new Vector2Int(entry.chunkX, entry.chunkY);
                    _savedStructures[coord] = entry.structures ?? new List<StructureSaveData>();
                }

                Debug.Log($"[PlacedStructureManager] Loaded structure data for {saveData.chunks.Count} chunks.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlacedStructureManager] Failed to load structures: {e.Message}");
            }
        }

        private string GetSavePath(string worldName)
        {
            string dir = Path.Combine(Application.persistentDataPath, "worlds", worldName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, SAVE_FILENAME);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private Vector2Int GetChunkCoord(Vector3 worldPos)
        {
            // Use ChunkManager's method — it uses the tilemap grid for accurate conversion
            if (ChunkManager.Instance != null)
                return ChunkManager.Instance.WorldToChunkCoord(worldPos);

            // Fallback (should never hit this in normal gameplay)
            Debug.LogWarning("[PlacedStructureManager] ChunkManager.Instance is null, using fallback coord calc.");
            return Vector2Int.zero;
        }

        // =====================================================================
        // Editor
        // =====================================================================

#if UNITY_EDITOR
        [ContextMenu("Clear Structure Save Data")]
        private void EditorClearSaveData()
        {
            ClearAll();
        }
#endif
    }
}