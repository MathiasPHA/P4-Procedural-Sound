using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Spawns objects (trees, rocks, props) within a chunk based on noise-driven density rules.
    /// Deterministic: same seed + chunk = same object placement every time.
    /// Tracks removed objects for persistence.
    /// </summary>
    public static class ObjectSpawner
    {
        /// <summary>
        /// Holds the spawned objects for a chunk, plus tracking of removed objects.
        /// </summary>
        public class ChunkObjects
        {
            public readonly Vector2Int ChunkCoord;
            public readonly Transform Parent;
            public readonly List<SpawnedObject> Objects = new List<SpawnedObject>();
            private HashSet<int> _removedIds;
            private HashSet<int> _depletedIds;

            public ChunkObjects(Vector2Int chunkCoord, Transform parent, HashSet<int> removedIds = null, HashSet<int> depletedIds = null)
            {
                ChunkCoord = chunkCoord;
                Parent = parent;
                _removedIds = removedIds ?? new HashSet<int>();
                _depletedIds = depletedIds ?? new HashSet<int>();
            }

            public bool IsRemoved(int spawnId) => _removedIds.Contains(spawnId);
            public bool IsDepleted(int spawnId) => _depletedIds.Contains(spawnId);
            public bool IsGone(int spawnId) => _removedIds.Contains(spawnId) || _depletedIds.Contains(spawnId);

            public void MarkRemoved(int spawnId)
            {
                _removedIds.Add(spawnId);
                _depletedIds.Remove(spawnId); // Can't be both
            }

            public void MarkDepleted(int spawnId)
            {
                _depletedIds.Add(spawnId);
                _removedIds.Remove(spawnId); // Can't be both
            }

            public HashSet<int> GetRemovedIds() => _removedIds;
            public HashSet<int> GetDepletedIds() => _depletedIds;
        }

        public struct SpawnedObject
        {
            public int SpawnId;        // Deterministic ID for persistence
            public GameObject Instance;
            public string RuleName;
        }

        /// <summary>
        /// Spawn objects for a chunk. Call after terrain is generated.
        /// </summary>
        public static ChunkObjects SpawnChunk(
            Vector2Int chunkCoord,
            int chunkSize,
            ChunkData chunkData,
            ObjectSpawnConfig config,
            int worldSeed,
            Transform chunkParent,
            float cellSize,
            HashSet<int> removedIds = null,
            HashSet<int> depletedIds = null)
        {
            var chunkObjects = new ChunkObjects(chunkCoord, chunkParent, removedIds, depletedIds);

            // Track occupied cells across all rules to prevent overlap
            var occupiedCells = new HashSet<Vector2Int>();

            int worldOffsetX = chunkCoord.x * chunkSize;
            int worldOffsetY = chunkCoord.y * chunkSize;

            // Build a shuffled list of cell positions so we don't iterate row-by-row
            // (row-by-row + minSpacing creates visible line patterns)
            var cellPositions = new List<Vector2Int>(chunkSize * chunkSize);
            for (int ly = 0; ly < chunkSize; ly++)
                for (int lx = 0; lx < chunkSize; lx++)
                    cellPositions.Add(new Vector2Int(lx, ly));

            // Deterministic shuffle based on chunk coord + seed
            var shuffleRng = new System.Random(HashSpawnId(chunkCoord.x, chunkCoord.y, 9999, worldSeed));
            for (int i = cellPositions.Count - 1; i > 0; i--)
            {
                int j = shuffleRng.Next(i + 1);
                var tmp = cellPositions[i];
                cellPositions[i] = cellPositions[j];
                cellPositions[j] = tmp;
            }

            for (int ruleIdx = 0; ruleIdx < config.rules.Count; ruleIdx++)
            {
                var rule = config.rules[ruleIdx];
                if (!rule.enabled) continue;
                if (rule.prefabs == null || rule.prefabs.Count == 0) continue;

                // Track positions for minimum spacing within this rule
                var spawnedPositions = new List<Vector2>();

                foreach (var cell in cellPositions)
                {
                    int lx = cell.x;
                    int ly = cell.y;

                    // Check terrain type
                    TerrainType terrain = chunkData.GetTerrain(lx, ly);
                    if (!rule.allowedTerrain.Contains(terrain)) continue;

                    int wx = worldOffsetX + lx;
                    int wy = worldOffsetY + ly;

                    // Deterministic spawn ID (unique per world position + rule)
                    int spawnId = HashSpawnId(wx, wy, ruleIdx, worldSeed);

                    // Fully removed = skip entirely (picked up twigs, etc.)
                    if (chunkObjects.IsRemoved(spawnId)) continue;

                    // Deterministic RNG for this cell + rule
                    System.Random rng = new System.Random(spawnId);

                    // Sample density noise
                    float densityNoise = Mathf.PerlinNoise(
                        (wx + worldSeed * 3.7f + rule.seedOffset * 137.3f) * rule.densityNoiseScale,
                        (wy + worldSeed * 7.1f + rule.seedOffset * 241.7f) * rule.densityNoiseScale
                    );

                    // Below cutoff = no spawning in this area
                    if (densityNoise < rule.densityCutoff) continue;

                    // Calculate spawn chance: base * density multiplier in dense areas
                    float densityFactor = Mathf.InverseLerp(rule.densityCutoff, 1f, densityNoise);
                    float spawnChance = rule.baseChance * Mathf.Lerp(1f, rule.densityMultiplier, densityFactor);

                    float roll = (float)rng.NextDouble();
                    if (roll > spawnChance) continue;

                    // Check minimum spacing against already-spawned positions for this rule
                    Vector2 candidatePos = new Vector2(lx, ly);
                    bool tooClose = false;
                    foreach (var existing in spawnedPositions)
                    {
                        if (Vector2.Distance(candidatePos, existing) < rule.minSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    // Check if cell is already occupied by a previous rule
                    var cellKey = new Vector2Int(lx, ly);
                    if (occupiedCells.Contains(cellKey)) continue;

                    // Pick a prefab deterministically (even for depleted, to keep RNG in sync)
                    int prefabIdx = rng.Next(rule.prefabs.Count);

                    // Calculate world position with jitter (same position for stump or original)
                    float jitterX = ((float)rng.NextDouble() * 2f - 1f) * rule.positionJitter;
                    float jitterY = ((float)rng.NextDouble() * 2f - 1f) * rule.positionJitter;

                    Vector3 worldPos = new Vector3(
                        (wx + 0.5f + jitterX) * cellSize,
                        (wy + 0.5f + jitterY) * cellSize,
                        0f
                    );

                    // Determine what to spawn: depleted stump or the original
                    GameObject prefab;
                    if (chunkObjects.IsDepleted(spawnId))
                    {
                        // Spawn the stump/depleted version instead
                        if (rule.depletedPrefab == null) continue; // No stump defined, skip
                        prefab = rule.depletedPrefab;
                    }
                    else
                    {
                        prefab = rule.prefabs[prefabIdx];
                        if (prefab == null) continue;
                    }

                    // Spawn
                    GameObject instance = Object.Instantiate(prefab, worldPos, Quaternion.identity, chunkParent);
                    instance.name = $"{rule.name}_{spawnId}";

                    chunkObjects.Objects.Add(new SpawnedObject
                    {
                        SpawnId = spawnId,
                        Instance = instance,
                        RuleName = rule.name,
                    });

                    spawnedPositions.Add(candidatePos);
                    occupiedCells.Add(cellKey);
                }
            }

            return chunkObjects;
        }

        /// <summary>
        /// Destroy all spawned objects for a chunk.
        /// </summary>
        public static void DespawnChunk(ChunkObjects chunkObjects)
        {
            if (chunkObjects == null) return;

            foreach (var obj in chunkObjects.Objects)
            {
                if (obj.Instance != null)
                    Object.Destroy(obj.Instance);
            }
            chunkObjects.Objects.Clear();

            if (chunkObjects.Parent != null)
                Object.Destroy(chunkObjects.Parent.gameObject);
        }

        /// <summary>
        /// Fully remove an object — it will never spawn again (picked up twigs, mushrooms, etc.)
        /// </summary>
        public static void RemoveObject(ChunkObjects chunkObjects, int spawnId)
        {
            chunkObjects.MarkRemoved(spawnId);
            DestroyTrackedObject(chunkObjects, spawnId);
        }

        /// <summary>
        /// Mark an object as depleted — on next load, the stump/depleted prefab spawns instead.
        /// The original GameObject is NOT destroyed here (HarvestableResource handles that + spawns the stump).
        /// </summary>
        public static void DepleteObject(ChunkObjects chunkObjects, int spawnId)
        {
            chunkObjects.MarkDepleted(spawnId);

            // Remove from tracked list (the GO is already being destroyed by HarvestableResource)
            for (int i = chunkObjects.Objects.Count - 1; i >= 0; i--)
            {
                if (chunkObjects.Objects[i].SpawnId == spawnId)
                {
                    chunkObjects.Objects.RemoveAt(i);
                    break;
                }
            }
        }

        private static void DestroyTrackedObject(ChunkObjects chunkObjects, int spawnId)
        {
            for (int i = chunkObjects.Objects.Count - 1; i >= 0; i--)
            {
                if (chunkObjects.Objects[i].SpawnId == spawnId)
                {
                    if (chunkObjects.Objects[i].Instance != null)
                        Object.Destroy(chunkObjects.Objects[i].Instance);
                    chunkObjects.Objects.RemoveAt(i);
                    break;
                }
            }
        }

        private static int HashSpawnId(int wx, int wy, int ruleIdx, int seed)
        {
            // xxHash-style bit mixing for good distribution
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)wx * 0x85EBCA6Bu;
                h = (h << 13) | (h >> 19);
                h *= 0xC2B2AE35u;
                h ^= (uint)wy * 0xCC9E2D51u;
                h = (h << 17) | (h >> 15);
                h *= 0x1B873593u;
                h ^= (uint)ruleIdx * 0x38495AB5u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)h;
            }
        }
    }
}