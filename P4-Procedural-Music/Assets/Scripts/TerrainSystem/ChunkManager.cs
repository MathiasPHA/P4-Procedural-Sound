using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProceduralTerrain
{
    /// <summary>
    /// Manages chunk lifecycle: generation, rendering, loading, unloading, and persistence.
    /// Attach to a GameObject with two child Tilemaps (ground + water).
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        public static ChunkManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("The player (or camera) Transform to track.")]
        public Transform player;

        [Tooltip("Tilemap for ground tiles (grass).")]
        public Tilemap groundTilemap;

        [Tooltip("Tilemap for water tiles (rendered above ground).")]
        public Tilemap waterTilemap;

        [Tooltip("Tileset reference mapping tsx IDs to Unity TileBase assets.")]
        public TilesetReference tileset;

        [Tooltip("Terrain generation configuration.")]
        public TerrainGenerationConfig generationConfig;

        [Header("Chunk Settings")]
        [Tooltip("Size of each chunk in tiles.")]
        public int chunkSize = 16;

        [Tooltip("How many chunks to keep loaded around the player.")]
        [Range(1, 8)]
        public int loadRadius = 3;

        [Tooltip("Chunks beyond this radius get unloaded.")]
        [Range(2, 10)]
        public int unloadRadius = 5;

        [Header("Object Spawning")]
        [Tooltip("Configuration for spawning trees, rocks, props, etc. Leave empty to skip object spawning.")]
        public ObjectSpawnConfig objectSpawnConfig;

        // Runtime seed — overrides generationConfig.seed without touching the asset
        private int _runtimeSeed;

        // Exposed so SaveSystemManager can access them
        internal Dictionary<Vector2Int, ChunkData> LoadedChunks => _loadedChunks;
        internal Dictionary<Vector2Int, ObjectSpawner.ChunkObjects> LoadedObjects => _loadedObjects;

        private Dictionary<Vector2Int, ChunkData> _loadedChunks = new Dictionary<Vector2Int, ChunkData>();
        private Dictionary<Vector2Int, ObjectSpawner.ChunkObjects> _loadedObjects = new Dictionary<Vector2Int, ObjectSpawner.ChunkObjects>();
        private Vector2Int _lastPlayerChunk;
        private Grid _grid;

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
            if (player == null)
            {
                Debug.LogError("[ChunkManager] No player Transform assigned!");
                enabled = false;
                return;
            }

            // Override seed from GameSettings if available, without modifying the ScriptableObject asset
            _runtimeSeed = GameSettings.Instance != null
                ? GameSettings.Instance.seed
                : generationConfig.seed;

            Debug.Log($"[ChunkManager] Using seed: {_runtimeSeed}");

            _grid = groundTilemap.layoutGrid;
            if (_grid == null)
            {
                Debug.LogError("[ChunkManager] No Grid component found on tilemap!");
                enabled = false;
                return;
            }

            Debug.Log($"[ChunkManager] Grid cell size: {_grid.cellSize}");

            groundTilemap.ClearAllTiles();
            waterTilemap.ClearAllTiles();

            _lastPlayerChunk = WorldToChunkCoord(player.position);
            Debug.Log($"[ChunkManager] Player at {player.position}, cell {groundTilemap.WorldToCell(player.position)}, chunk {_lastPlayerChunk}");
            UpdateChunks(_lastPlayerChunk);
        }

        private void Update()
        {
            Vector2Int currentChunk = WorldToChunkCoord(player.position);

            if (currentChunk != _lastPlayerChunk)
            {
                _lastPlayerChunk = currentChunk;
                UpdateChunks(currentChunk);
            }
        }

        /// <summary>
        /// Convert a world position to chunk coordinate.
        /// </summary>
        public Vector2Int WorldToChunkCoord(Vector3 worldPos)
        {
            Vector3Int cell = groundTilemap.WorldToCell(worldPos);
            return new Vector2Int(
                Mathf.FloorToInt((float)cell.x / chunkSize),
                Mathf.FloorToInt((float)cell.y / chunkSize)
            );
        }

        /// <summary>
        /// Modify a tile at a world position. Handles persistence automatically.
        /// </summary>
        public void ModifyTerrain(Vector3 worldPos, TerrainType newType)
        {
            Vector3Int cell = groundTilemap.WorldToCell(worldPos);
            int tileX = cell.x;
            int tileY = cell.y;

            Vector2Int chunkCoord = new Vector2Int(
                Mathf.FloorToInt((float)tileX / chunkSize),
                Mathf.FloorToInt((float)tileY / chunkSize)
            );

            if (!_loadedChunks.TryGetValue(chunkCoord, out var chunk))
            {
                Debug.LogWarning($"[ChunkManager] Cannot modify unloaded chunk {chunkCoord}");
                return;
            }

            int localX = ((tileX % chunkSize) + chunkSize) % chunkSize;
            int localY = ((tileY % chunkSize) + chunkSize) % chunkSize;

            chunk.SetTerrain(localX, localY, newType);
            RerenderChunkAndNeighbors(chunkCoord);
        }

        /// <summary>
        /// Get the terrain type at a world position.
        /// </summary>
        public TerrainType GetTerrainAt(Vector3 worldPos)
        {
            Vector3Int cell = groundTilemap.WorldToCell(worldPos);
            int tileX = cell.x;
            int tileY = cell.y;

            Vector2Int chunkCoord = new Vector2Int(
                Mathf.FloorToInt((float)tileX / chunkSize),
                Mathf.FloorToInt((float)tileY / chunkSize)
            );

            if (_loadedChunks.TryGetValue(chunkCoord, out var chunk))
            {
                int localX = ((tileX % chunkSize) + chunkSize) % chunkSize;
                int localY = ((tileY % chunkSize) + chunkSize) % chunkSize;
                return chunk.GetTerrain(localX, localY);
            }

            return TerrainGenerator.SampleAt(tileX, tileY, generationConfig, _runtimeSeed);
        }

        /// <summary>
        /// Fully remove a spawned object — it will never come back.
        /// </summary>
        public void RemoveSpawnedObject(Vector3 objectWorldPos, int spawnId)
        {
            Vector2Int chunkCoord = WorldToChunkCoord(objectWorldPos);
            if (_loadedObjects.TryGetValue(chunkCoord, out var chunkObjects))
            {
                ObjectSpawner.RemoveObject(chunkObjects, spawnId);
            }
        }

        /// <summary>
        /// Mark a spawned object as depleted.
        /// </summary>
        public void DepleteSpawnedObject(Vector3 objectWorldPos, int spawnId)
        {
            Vector2Int chunkCoord = WorldToChunkCoord(objectWorldPos);
            if (_loadedObjects.TryGetValue(chunkCoord, out var chunkObjects))
            {
                ObjectSpawner.DepleteObject(chunkObjects, spawnId);
            }
        }

        /// <summary>
        /// Helper to extract spawnId from an object spawned by the system.
        /// </summary>
        public static int GetSpawnIdFromObject(GameObject obj)
        {
            string name = obj.name;
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
            {
                if (int.TryParse(name.Substring(lastUnderscore + 1), out int id))
                    return id;
            }
            return 0;
        }

        private void UpdateChunks(Vector2Int centerChunk)
        {
            for (int dy = -loadRadius; dy <= loadRadius; dy++)
            {
                for (int dx = -loadRadius; dx <= loadRadius; dx++)
                {
                    Vector2Int coord = new Vector2Int(centerChunk.x + dx, centerChunk.y + dy);
                    if (!_loadedChunks.ContainsKey(coord))
                        LoadChunk(coord);
                }
            }

            var toUnload = new List<Vector2Int>();
            foreach (var coord in _loadedChunks.Keys)
            {
                int dist = Mathf.Max(
                    Mathf.Abs(coord.x - centerChunk.x),
                    Mathf.Abs(coord.y - centerChunk.y)
                );
                if (dist > unloadRadius)
                    toUnload.Add(coord);
            }

            foreach (var coord in toUnload)
                UnloadChunk(coord);
        }

        private void LoadChunk(Vector2Int coord)
        {
            // Pass runtime seed as override — never mutate the ScriptableObject asset
            var baseGrid = TerrainGenerator.GenerateChunk(coord, chunkSize, generationConfig, _runtimeSeed);
            var chunk = new ChunkData(coord, chunkSize, baseGrid);

            int waterCount = 0;
            for (int y = 0; y < chunkSize; y++)
                for (int x = 0; x < chunkSize; x++)
                    if (chunk.GetTerrain(x, y) == TerrainType.Water) waterCount++;

            int cellStartX = coord.x * chunkSize;
            int cellStartY = coord.y * chunkSize;
            Debug.Log($"[ChunkManager] Loading chunk {coord} → cells ({cellStartX},{cellStartY}) to ({cellStartX + chunkSize - 1},{cellStartY + chunkSize - 1}), water: {waterCount}/{chunkSize * chunkSize}");

            var mods = ChunkPersistence.LoadChunkModifications(coord, SaveSystemManager.Instance.worldName);
            if (mods != null)
                chunk.ApplyModifications(mods);

            _loadedChunks[coord] = chunk;

            ChunkRenderer.RenderChunk(chunk, groundTilemap, waterTilemap, tileset, generationConfig, chunkSize, _runtimeSeed);

            // Generate water collision shapes from .tsx collision data
            float collisionCellSize = _grid != null ? _grid.cellSize.x : 1f;
            WaterCollisionGenerator.GenerateChunkCollision(
                chunk, chunkSize, collisionCellSize,
                generationConfig, _runtimeSeed, waterTilemap.transform);

            if (objectSpawnConfig != null && objectSpawnConfig.rules.Count > 0)
            {
                var removedIds = ChunkPersistence.LoadRemovedObjectIds(coord, SaveSystemManager.Instance.worldName);
                var depletedIds = ChunkPersistence.LoadDepletedObjectIds(coord, SaveSystemManager.Instance.worldName);

                var chunkParent = new GameObject($"Chunk_{coord.x}_{coord.y}_Objects").transform;
                chunkParent.SetParent(transform);

                float cellSize = _grid != null ? _grid.cellSize.x : 1f;

                var chunkObjects = ObjectSpawner.SpawnChunk(
                    coord, chunkSize, chunk, objectSpawnConfig,
                    _runtimeSeed, chunkParent, cellSize, removedIds, depletedIds);

                _loadedObjects[coord] = chunkObjects;
            }

            // Spawn saved player-placed structures
            if (PlacedStructureManager.Instance != null)
            {
                PlacedStructureManager.Instance.LoadStructuresForChunk(coord);
            }
        }

        private void UnloadChunk(Vector2Int coord)
        {
            if (_loadedChunks.TryGetValue(coord, out var chunk))
            {
                if (chunk.IsDirty)
                    SaveSystemManager.Instance.SaveModifiedChunks();

                if (_loadedObjects.TryGetValue(coord, out var chunkObjects))
                {
                    ObjectSpawner.DespawnChunk(chunkObjects);
                    _loadedObjects.Remove(coord);
                }

                // Clean up placed structures in this chunk
                if (PlacedStructureManager.Instance != null)
                {
                    PlacedStructureManager.Instance.UnloadStructuresForChunk(coord);
                }

                ChunkRenderer.ClearChunk(coord, groundTilemap, waterTilemap, chunkSize);
                WaterCollisionGenerator.ClearChunkCollision(coord);
                _loadedChunks.Remove(coord);
            }
        }

        private void RerenderChunkAndNeighbors(Vector2Int center)
        {
            float collisionCellSize = _grid != null ? _grid.cellSize.x : 1f;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var coord = new Vector2Int(center.x + dx, center.y + dy);
                    if (_loadedChunks.TryGetValue(coord, out var chunk))
                    {
                        ChunkRenderer.RenderChunk(chunk, groundTilemap, waterTilemap, tileset, generationConfig, chunkSize, _runtimeSeed);
                        WaterCollisionGenerator.GenerateChunkCollision(
                            chunk, chunkSize, collisionCellSize,
                            generationConfig, _runtimeSeed, waterTilemap.transform);
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (player == null || groundTilemap == null) return;

            Vector2Int center = WorldToChunkCoord(player.position);
            var grid = groundTilemap.layoutGrid;
            float cellX = grid != null ? grid.cellSize.x : 1f;
            float cellY = grid != null ? grid.cellSize.y : 1f;
            float sx = chunkSize * cellX;
            float sy = chunkSize * cellY;

            Gizmos.color = new Color(0, 1, 0, 0.2f);
            for (int dy = -loadRadius; dy <= loadRadius; dy++)
                for (int dx = -loadRadius; dx <= loadRadius; dx++)
                {
                    var pos = new Vector3((center.x + dx) * sx + sx * 0.5f, (center.y + dy) * sy + sy * 0.5f, 0);
                    Gizmos.DrawWireCube(pos, new Vector3(sx, sy, 0));
                }

            Gizmos.color = new Color(1, 0, 0, 0.15f);
            for (int dy = -unloadRadius; dy <= unloadRadius; dy++)
                for (int dx = -unloadRadius; dx <= unloadRadius; dx++)
                {
                    int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    if (dist > loadRadius)
                    {
                        var pos = new Vector3((center.x + dx) * sx + sx * 0.5f, (center.y + dy) * sy + sy * 0.5f, 0);
                        Gizmos.DrawWireCube(pos, new Vector3(sx, sy, 0));
                    }
                }
        }
#endif
    }
}