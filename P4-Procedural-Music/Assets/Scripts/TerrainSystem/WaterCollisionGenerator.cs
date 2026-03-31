using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Generates PolygonCollider2D collision shapes for water tiles in a chunk,
    /// using the collision polygon data parsed from the .tsx tileset.
    /// 
    /// Call GenerateChunkCollision after ChunkRenderer paints tiles.
    /// Call ClearChunkCollision when unloading a chunk.
    /// </summary>
    public static class WaterCollisionGenerator
    {
        // Track collision GameObjects per chunk so we can clean them up
        private static Dictionary<Vector2Int, GameObject> _chunkColliders = new Dictionary<Vector2Int, GameObject>();

        /// <summary>
        /// Generate collision shapes for all water tiles in a chunk.
        /// Creates a child GameObject with a PolygonCollider2D under the given parent.
        /// </summary>
        public static void GenerateChunkCollision(
            ChunkData chunk,
            int chunkSize,
            float cellSize,
            TerrainGenerationConfig genConfig,
            int? seedOverride,
            Transform parent)
        {
            // Clean up existing collision for this chunk if any
            ClearChunkCollision(chunk.ChunkCoord);

            int worldOffsetX = chunk.ChunkCoord.x * chunkSize;
            int worldOffsetY = chunk.ChunkCoord.y * chunkSize;

            // Collect all polygon paths for this chunk
            var allPaths = new List<Vector2[]>();

            for (int ly = 0; ly < chunkSize; ly++)
            {
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    if (chunk.GetTerrain(lx, ly) != TerrainType.Water)
                        continue;

                    int wx = worldOffsetX + lx;
                    int wy = worldOffsetY + ly;

                    // Rebuild the same bitmask that ChunkRenderer uses
                    int bitmask = BuildBitmaskAt(lx, ly, chunk, chunkSize, genConfig, seedOverride);
                    int tsxId = WaterBitmaskResolver.Resolve(bitmask);

                    // Get collision paths for this tile variant
                    Vector2[][] tilePaths = WaterCollisionData.GetCollisionPaths(tsxId);
                    if (tilePaths == null)
                        continue;

                    // Transform normalized tile-local paths to world space
                    foreach (var path in tilePaths)
                    {
                        var worldPath = new Vector2[path.Length];
                        for (int i = 0; i < path.Length; i++)
                        {
                            // path vertices are normalized (0-1), Y-up
                            // Scale by cellSize and offset by cell world position
                            worldPath[i] = new Vector2(
                                (wx + path[i].x) * cellSize,
                                (wy + path[i].y) * cellSize
                            );
                        }
                        allPaths.Add(worldPath);
                    }
                }
            }

            if (allPaths.Count == 0)
                return;

            // Create collision GameObject
            var colliderObj = new GameObject($"WaterCollision_{chunk.ChunkCoord.x}_{chunk.ChunkCoord.y}");
            colliderObj.transform.SetParent(parent, false);
            colliderObj.transform.localPosition = Vector3.zero;
            colliderObj.layer = parent.gameObject.layer;

            var polyCollider = colliderObj.AddComponent<PolygonCollider2D>();
            polyCollider.pathCount = allPaths.Count;

            for (int i = 0; i < allPaths.Count; i++)
            {
                polyCollider.SetPath(i, allPaths[i]);
            }

            _chunkColliders[chunk.ChunkCoord] = colliderObj;
        }

        /// <summary>
        /// Remove collision shapes for a chunk.
        /// </summary>
        public static void ClearChunkCollision(Vector2Int chunkCoord)
        {
            if (_chunkColliders.TryGetValue(chunkCoord, out var obj))
            {
                if (obj != null)
                    Object.Destroy(obj);
                _chunkColliders.Remove(chunkCoord);
            }
        }

        /// <summary>
        /// Clear all tracked chunk colliders.
        /// </summary>
        public static void ClearAll()
        {
            foreach (var kvp in _chunkColliders)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value);
            }
            _chunkColliders.Clear();
        }

        // Duplicated bitmask logic from ChunkRenderer to keep collision generation independent
        private static int BuildBitmaskAt(int lx, int ly, ChunkData chunk, int chunkSize,
            TerrainGenerationConfig genConfig, int? seedOverride)
        {
            int wx = chunk.ChunkCoord.x * chunkSize + lx;
            int wy = chunk.ChunkCoord.y * chunkSize + ly;

            bool IsWater(int offsetX, int offsetY)
            {
                int nlx = lx + offsetX;
                int nly = ly + offsetY;

                if (nlx >= 0 && nlx < chunkSize && nly >= 0 && nly < chunkSize)
                    return chunk.GetTerrain(nlx, nly) == TerrainType.Water;

                return TerrainGenerator.SampleAt(wx + offsetX, wy + offsetY, genConfig, seedOverride) == TerrainType.Water;
            }

            return WaterBitmaskResolver.BuildBitmask(
                nw: IsWater(-1, 1),
                n: IsWater(0, 1),
                ne: IsWater(1, 1),
                w: IsWater(-1, 0),
                e: IsWater(1, 0),
                sw: IsWater(-1, -1),
                s: IsWater(0, -1),
                se: IsWater(1, -1)
            );
        }
    }
}
