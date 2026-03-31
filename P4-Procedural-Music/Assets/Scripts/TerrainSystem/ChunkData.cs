using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Holds the generated terrain data for a single chunk,
    /// plus any player modifications that override the base generation.
    /// </summary>
    public class ChunkData
    {
        public readonly Vector2Int ChunkCoord;
        public readonly int Size;
        public readonly TerrainType[,] BaseGrid;  // Generated terrain (never modified)
        public bool IsDirty { get; private set; }  // Has unsaved modifications

        // Modifications: world-tile-position → new terrain type
        // Only stores deltas from the base generation
        private Dictionary<Vector2Int, TerrainType> _modifications;

        public ChunkData(Vector2Int chunkCoord, int size, TerrainType[,] baseGrid)
        {
            ChunkCoord = chunkCoord;
            Size = size;
            BaseGrid = baseGrid;
            _modifications = new Dictionary<Vector2Int, TerrainType>();
        }

        /// <summary>
        /// Get the terrain at a local position (0..Size-1),
        /// accounting for any player modifications.
        /// </summary>
        public TerrainType GetTerrain(int localX, int localY)
        {
            var localPos = new Vector2Int(localX, localY);
            if (_modifications.TryGetValue(localPos, out var modified))
                return modified;
            return BaseGrid[localY, localX];
        }

        /// <summary>
        /// Apply a player modification at a local position.
        /// If the new type matches the base generation, removes the override.
        /// </summary>
        public void SetTerrain(int localX, int localY, TerrainType newType)
        {
            var localPos = new Vector2Int(localX, localY);

            if (BaseGrid[localY, localX] == newType)
            {
                // Matches base; remove the override if it exists
                _modifications.Remove(localPos);
            }
            else
            {
                _modifications[localPos] = newType;
            }
            IsDirty = true;
        }

        /// <summary>
        /// Returns true if this chunk has any player modifications.
        /// </summary>
        public bool HasModifications => _modifications.Count > 0;

        /// <summary>
        /// Get all modifications for serialization.
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, TerrainType> GetModifications() => _modifications;

        /// <summary>
        /// Apply loaded modifications (from save data).
        /// </summary>
        public void ApplyModifications(Dictionary<Vector2Int, TerrainType> mods)
        {
            _modifications = new Dictionary<Vector2Int, TerrainType>(mods);
            IsDirty = false;
        }

        public void MarkClean()
        {
            IsDirty = false;
        }
    }

    /// <summary>
    /// Serializable save data for a single chunk's modifications.
    /// </summary>
    [System.Serializable]
    public class ChunkSaveData
    {
        public int chunkX;
        public int chunkY;
        public List<TileModification> modifications = new List<TileModification>();
        public List<int> removedObjectIds = new List<int>();
        public List<int> depletedObjectIds = new List<int>();

        [System.Serializable]
        public struct TileModification
        {
            public int localX;
            public int localY;
            public int terrainType;
        }

        public static ChunkSaveData FromChunkData(ChunkData chunk, HashSet<int> removedObjects = null, HashSet<int> depletedObjects = null)
        {
            var data = new ChunkSaveData
            {
                chunkX = chunk.ChunkCoord.x,
                chunkY = chunk.ChunkCoord.y,
            };

            foreach (var kvp in chunk.GetModifications())
            {
                data.modifications.Add(new TileModification
                {
                    localX = kvp.Key.x,
                    localY = kvp.Key.y,
                    terrainType = (int)kvp.Value,
                });
            }

            if (removedObjects != null)
                data.removedObjectIds.AddRange(removedObjects);

            if (depletedObjects != null)
                data.depletedObjectIds.AddRange(depletedObjects);

            return data;
        }

        public Dictionary<Vector2Int, TerrainType> ToModifications()
        {
            var mods = new Dictionary<Vector2Int, TerrainType>(modifications.Count);
            foreach (var mod in modifications)
            {
                mods[new Vector2Int(mod.localX, mod.localY)] = (TerrainType)mod.terrainType;
            }
            return mods;
        }

        public HashSet<int> ToRemovedObjectIds()
        {
            return new HashSet<int>(removedObjectIds);
        }

        public HashSet<int> ToDepletedObjectIds()
        {
            return new HashSet<int>(depletedObjectIds);
        }

        public bool HasAnyData => modifications.Count > 0 || removedObjectIds.Count > 0 || depletedObjectIds.Count > 0;
    }

    /// <summary>
    /// Serializable container for all chunk modifications in the world.
    /// </summary>
    [System.Serializable]
    public class WorldSaveData
    {
        public List<ChunkSaveData> chunks = new List<ChunkSaveData>();
    }
}