using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Handles saving and loading chunk modifications to disk.
    /// Only chunks with player modifications are persisted — 
    /// unmodified chunks regenerate deterministically from noise.
    /// </summary>
    public static class ChunkPersistence
    {
        private const string SAVE_FILENAME = "world_chunks.json";

        private static string GetSavePath(string worldName = "default")
        {
            string dir = Path.Combine(Application.persistentDataPath, "worlds", worldName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, SAVE_FILENAME);
        }

        /// <summary>
        /// Save all modified chunks to disk, including removed object tracking.
        /// </summary>
        public static void SaveModifiedChunks(
            Dictionary<Vector2Int, ChunkData> loadedChunks,
            Dictionary<Vector2Int, ObjectSpawner.ChunkObjects> loadedObjects = null,
            string worldName = "default")
        {
            var saveData = new WorldSaveData();

            foreach (var kvp in loadedChunks)
            {
                HashSet<int> removedIds = null;
                HashSet<int> depletedIds = null;
                if (loadedObjects != null && loadedObjects.TryGetValue(kvp.Key, out var chunkObjs))
                {
                    removedIds = chunkObjs.GetRemovedIds();
                    depletedIds = chunkObjs.GetDepletedIds();
                }

                bool hasTerrainMods = kvp.Value.HasModifications;
                bool hasRemovedObjects = removedIds != null && removedIds.Count > 0;
                bool hasDepletedObjects = depletedIds != null && depletedIds.Count > 0;

                if (hasTerrainMods || hasRemovedObjects || hasDepletedObjects)
                {
                    Debug.Log($"[ChunkPersistence] Saving chunk {kvp.Key}: " +
                              $"terrainMods={hasTerrainMods}, " +
                              $"removed={removedIds?.Count ?? 0}, " +
                              $"depleted={depletedIds?.Count ?? 0}");
                    saveData.chunks.Add(ChunkSaveData.FromChunkData(kvp.Value, removedIds, depletedIds));
                    kvp.Value.MarkClean();
                }
            }

            // Merge with existing save (keep data for unloaded chunks)
            var existing = LoadSaveData(worldName);
            if (existing != null)
            {
                var loadedCoords = new HashSet<Vector2Int>(loadedChunks.Keys);
                foreach (var existingChunk in existing.chunks)
                {
                    var coord = new Vector2Int(existingChunk.chunkX, existingChunk.chunkY);
                    if (!loadedCoords.Contains(coord))
                    {
                        saveData.chunks.Add(existingChunk);
                    }
                }
            }

            if (saveData.chunks.Count == 0)
                return;

            string json = JsonUtility.ToJson(saveData, prettyPrint: false);
            File.WriteAllText(GetSavePath(worldName), json);
            Debug.Log($"[ChunkPersistence] Saved {saveData.chunks.Count} modified chunks.");
        }

        /// <summary>
        /// Load modifications for a specific chunk. Returns null if no save data exists.
        /// </summary>
        public static Dictionary<Vector2Int, TerrainType> LoadChunkModifications(Vector2Int chunkCoord, string worldName = "default")
        {
            var chunkSave = LoadChunkSaveData(chunkCoord, worldName);
            return chunkSave?.ToModifications();
        }

        /// <summary>
        /// Load removed object IDs for a specific chunk. Returns null if no save data exists.
        /// </summary>
        public static HashSet<int> LoadRemovedObjectIds(Vector2Int chunkCoord, string worldName = "default")
        {
            var chunkSave = LoadChunkSaveData(chunkCoord, worldName);
            if (chunkSave == null || chunkSave.removedObjectIds.Count == 0)
                return null;
            return chunkSave.ToRemovedObjectIds();
        }

        /// <summary>
        /// Load depleted object IDs for a specific chunk. Returns null if no save data exists.
        /// </summary>
        public static HashSet<int> LoadDepletedObjectIds(Vector2Int chunkCoord, string worldName = "default")
        {
            var chunkSave = LoadChunkSaveData(chunkCoord, worldName);
            if (chunkSave == null || chunkSave.depletedObjectIds.Count == 0)
                return null;
            return chunkSave.ToDepletedObjectIds();
        }

        private static ChunkSaveData LoadChunkSaveData(Vector2Int chunkCoord, string worldName)
        {
            var saveData = LoadSaveData(worldName);
            if (saveData == null) return null;

            foreach (var chunk in saveData.chunks)
            {
                if (chunk.chunkX == chunkCoord.x && chunk.chunkY == chunkCoord.y)
                    return chunk;
            }
            return null;
        }

        /// <summary>
        /// Load all save data from disk.
        /// </summary>
        private static WorldSaveData LoadSaveData(string worldName)
        {
            string path = GetSavePath(worldName);
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<WorldSaveData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ChunkPersistence] Failed to load save: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Delete all save data for a world.
        /// </summary>
        public static void DeleteWorld(string worldName = "default")
        {
            string path = GetSavePath(worldName);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}