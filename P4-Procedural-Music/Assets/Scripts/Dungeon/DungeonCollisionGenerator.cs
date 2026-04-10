using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProceduralTerrain
{
    /// <summary>
    /// Generates PolygonCollider2D collision for dungeon floor tiles using
    /// DungeonCollisionData (parsed from Dungeon_Tileset.tsx).
    ///
    /// Same approach as WaterCollisionGenerator but for dungeon floors:
    /// floor cells use water tiles (walkable), collision shapes on the
    /// edges block the player from walking into walls.
    /// </summary>
    public static class DungeonCollisionGenerator
    {
        private static GameObject _colliderObject;

        /// <summary>
        /// Build collision for the entire dungeon grid.
        /// Call after DungeonGenerator has resolved all tile IDs.
        /// </summary>
        public static void GenerateCollision(
            int[,] grid,
            int[] resolvedTileIds,
            int gridWidth,
            int gridHeight,
            float cellSize,
            Vector3Int tileOffset,
            Transform parent)
        {
            ClearCollision();

            // Read tile anchor from the Tilemap to match sprite rendering offset
            var tilemap = parent.GetComponent<Tilemap>();
            float anchorX = tilemap != null ? tilemap.tileAnchor.x : 0f;
            float anchorY = tilemap != null ? tilemap.tileAnchor.y : 0f;

            var allPaths = new List<Vector2[]>();

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    // Only floor cells have resolved tile IDs with collision
                    if (grid[x, y] != 1) continue;

                    int index = y * gridWidth + x;
                    int tsxId = resolvedTileIds[index];
                    if (tsxId < 0) continue;

                    Vector2[][] tilePaths = DungeonCollisionData.GetCollisionPaths(tsxId);
                    if (tilePaths == null) continue;

                    // World position of this tile (matching tilemap offset + anchor)
                    float worldX = (x + tileOffset.x + anchorX) * cellSize;
                    float worldY = (y + tileOffset.y + anchorY) * cellSize;

                    foreach (var path in tilePaths)
                    {
                        var worldPath = new Vector2[path.Length];
                        for (int i = 0; i < path.Length; i++)
                        {
                            worldPath[i] = new Vector2(
                                worldX + path[i].x * cellSize,
                                worldY + path[i].y * cellSize
                            );
                        }
                        allPaths.Add(worldPath);
                    }
                }
            }

            if (allPaths.Count == 0) return;

            _colliderObject = new GameObject("DungeonCollision");
            _colliderObject.transform.SetParent(parent, false);
            _colliderObject.transform.localPosition = Vector3.zero;

            var poly = _colliderObject.AddComponent<PolygonCollider2D>();
            poly.pathCount = allPaths.Count;

            for (int i = 0; i < allPaths.Count; i++)
                poly.SetPath(i, allPaths[i]);
        }

        public static void ClearCollision()
        {
            if (_colliderObject != null)
            {
                Object.Destroy(_colliderObject);
                _colliderObject = null;
            }
        }

    }
}