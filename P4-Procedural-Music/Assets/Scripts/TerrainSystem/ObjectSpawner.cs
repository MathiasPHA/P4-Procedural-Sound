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
            TerrainGenerationConfig genConfig,
            int worldSeed,
            Transform chunkParent,
            float cellSize,
            HashSet<int> removedIds = null,
            HashSet<int> depletedIds = null)
        {
            var chunkObjects = new ChunkObjects(chunkCoord, chunkParent, removedIds, depletedIds);

            // Track every spawned object across all rules. Each record is the final (jittered)
            // tile-space position plus the rule's minSpacing as a "claim radius". Later candidates
            // must clear max(their minSpacing, this claim radius) — so a tree's large spacing
            // repels nearby rocks even if rocks have tiny spacing of their own.
            var allSpawned = new List<SpawnRecord>();

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

            // Sort rules rarest-first (lowest baseChance) so scarce objects claim space before
            // common ones. The original config index (ruleIdx) is preserved so the deterministic
            // spawnId hash doesn't change — saves remain valid after this reorder.
            var ruleOrder = new List<int>(config.rules.Count);
            for (int i = 0; i < config.rules.Count; i++) ruleOrder.Add(i);
            ruleOrder.Sort((a, b) => config.rules[a].baseChance.CompareTo(config.rules[b].baseChance));

            foreach (int ruleIdx in ruleOrder)
            {
                var rule = config.rules[ruleIdx];
                if (!rule.enabled) continue;
                if (rule.prefabs == null || rule.prefabs.Count == 0) continue;

                int spawnCount = 0;

                foreach (var cell in cellPositions)
                {
                    // Check max count cap
                    if (rule.maxCountPerChunk > 0 && spawnCount >= rule.maxCountPerChunk) break;

                    int lx = cell.x;
                    int ly = cell.y;

                    // Quick terrain check on the spawn tile itself (cheap early-out)
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

                    // Sample density noise.
                    // IMPORTANT: the seed/rule offsets are added AFTER scaling so they translate
                    // the sample window in noise-space by a fixed amount regardless of seed magnitude.
                    // Previously they were inside the scale multiplication, meaning large seed values
                    // (e.g. 1000) were amplified through densityNoiseScale and could push the entire
                    // world into a permanently high or low density region of the Perlin field.
                    float densityNoise = Mathf.PerlinNoise(
                        wx * rule.densityNoiseScale + worldSeed * 3.7f + rule.seedOffset * 137.3f,
                        wy * rule.densityNoiseScale + worldSeed * 7.1f + rule.seedOffset * 241.7f
                    );

                    // Below cutoff = no spawning in this area
                    if (densityNoise < rule.densityCutoff) continue;

                    // Calculate spawn chance: base * density multiplier in dense areas
                    float densityFactor = Mathf.InverseLerp(rule.densityCutoff, 1f, densityNoise);
                    float spawnChance = rule.baseChance * Mathf.Lerp(1f, rule.densityMultiplier, densityFactor);

                    float roll = (float)rng.NextDouble();
                    if (roll > spawnChance) continue;

                    // Pick a prefab deterministically (even for depleted, to keep RNG in sync)
                    int prefabIdx = rng.Next(rule.prefabs.Count);

                    // Compute jitter (in tile units) — done before placement checks so the
                    // checks use the actual final position, not the un-jittered cell center.
                    float jitterX = ((float)rng.NextDouble() * 2f - 1f) * rule.positionJitter;
                    float jitterY = ((float)rng.NextDouble() * 2f - 1f) * rule.positionJitter;

                    // Final tile-space position (cell center + jitter)
                    Vector2 candidateTilePos = new Vector2(lx + 0.5f + jitterX, ly + 0.5f + jitterY);

                    // Post-jitter terrain check: re-verify the tile under the FINAL position is allowed.
                    // With positionJitter <= 0.5 (the inspector slider cap), the position can't leave
                    // the spawn cell, so this is mostly a defensive safety net — but if positionJitter
                    // has been set higher via YAML, this catches the case where jitter pushes the spawn
                    // across a cell boundary onto disallowed terrain (e.g. water).
                    int finalTileX = Mathf.FloorToInt(candidateTilePos.x);
                    int finalTileY = Mathf.FloorToInt(candidateTilePos.y);
                    if (finalTileX >= 0 && finalTileX < chunkSize &&
                        finalTileY >= 0 && finalTileY < chunkSize)
                    {
                        if (!rule.allowedTerrain.Contains(chunkData.GetTerrain(finalTileX, finalTileY)))
                            continue;
                    }
                    // (If jitter pushed across a chunk boundary, fall through and trust the
                    //  spawn-cell check at the top of the loop. With positionJitter <= 0.5 this
                    //  branch is unreachable.)

                    // Clearance check (world units): every tile whose center lies within
                    // clearanceRadius world units of the candidate must be in allowedTerrain.
                    // Used when the sprite is wider than one cell — keeps trees off coastline
                    // cells where their canopy would visually overhang water.
                    if (rule.clearanceRadius > 0f &&
                        !IsClearanceValid(candidateTilePos, rule, cellSize, chunkCoord,
                                          chunkSize, chunkData, genConfig, worldSeed))
                        continue;

                    // Cross-rule spacing check. Required clearance = max of the two claim radii,
                    // so a tree (minSpacing 2) repels a nearby rock candidate even if the rock's
                    // own minSpacing is small.
                    bool tooClose = false;
                    for (int i = 0; i < allSpawned.Count; i++)
                    {
                        float requiredSpacing = Mathf.Max(rule.minSpacing, allSpawned[i].ClaimRadius);
                        if (requiredSpacing <= 0f) continue;
                        Vector2 delta = allSpawned[i].TilePos - candidateTilePos;
                        if (delta.sqrMagnitude < requiredSpacing * requiredSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    // World-unit clearance check (objectClearance). Same idea as minSpacing but
                    // authored in world units instead of tiles — the right tool when sprites are
                    // bigger than tiles. Distance compared in world space; required clearance is
                    // the larger of the two objects' values.
                    if (rule.objectClearance > 0f || HasAnyObjectClearance(allSpawned))
                    {
                        for (int i = 0; i < allSpawned.Count; i++)
                        {
                            float required = Mathf.Max(rule.objectClearance, allSpawned[i].ObjectClearance);
                            if (required <= 0f) continue;
                            Vector2 deltaTiles = allSpawned[i].TilePos - candidateTilePos;
                            float deltaWorldSq = deltaTiles.sqrMagnitude * cellSize * cellSize;
                            if (deltaWorldSq < required * required)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        if (tooClose) continue;
                    }

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

                    Vector3 worldPos = new Vector3(
                        (worldOffsetX + candidateTilePos.x) * cellSize,
                        (worldOffsetY + candidateTilePos.y) * cellSize,
                        0f
                    );

                    // Spawn
                    GameObject instance = Object.Instantiate(prefab, worldPos, Quaternion.identity, chunkParent);
                    instance.name = $"{rule.name}_{spawnId}";

                    chunkObjects.Objects.Add(new SpawnedObject
                    {
                        SpawnId = spawnId,
                        Instance = instance,
                        RuleName = rule.name,
                    });

                    // Track globally so subsequent candidates (any rule) respect this claim
                    allSpawned.Add(new SpawnRecord
                    {
                        TilePos = candidateTilePos,
                        ClaimRadius = rule.minSpacing,
                        ObjectClearance = rule.objectClearance,
                    });
                    spawnCount++;
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

        private struct SpawnRecord
        {
            public Vector2 TilePos;
            public float ClaimRadius;       // tile units (minSpacing)
            public float ObjectClearance;   // world units
        }

        private static bool HasAnyObjectClearance(List<SpawnRecord> records)
        {
            for (int i = 0; i < records.Count; i++)
                if (records[i].ObjectClearance > 0f) return true;
            return false;
        }

        /// <summary>
        /// Returns true if every tile whose center lies within rule.clearanceRadius (in WORLD units)
        /// of the candidate position is in allowedTerrain. Internally converts the world-unit radius
        /// to tile units once (clearanceRadius / cellSize) and does a disc sample. Cross-chunk
        /// neighbors are sampled from the base generator — modifications in unloaded adjacent
        /// chunks are not respected, but base terrain is what matters for spawn placement.
        /// </summary>
        private static bool IsClearanceValid(
            Vector2 candidateTilePos,
            ObjectSpawnConfig.SpawnRule rule,
            float cellSize,
            Vector2Int chunkCoord,
            int chunkSize,
            ChunkData chunkData,
            TerrainGenerationConfig genConfig,
            int worldSeed)
        {
            // Convert clearance from world units to tile units (cellSize == world units per tile)
            float radiusTiles = rule.clearanceRadius / cellSize;
            float r2 = radiusTiles * radiusTiles;

            int minX = Mathf.FloorToInt(candidateTilePos.x - radiusTiles);
            int maxX = Mathf.CeilToInt(candidateTilePos.x + radiusTiles);
            int minY = Mathf.FloorToInt(candidateTilePos.y - radiusTiles);
            int maxY = Mathf.CeilToInt(candidateTilePos.y + radiusTiles);

            int worldOffsetX = chunkCoord.x * chunkSize;
            int worldOffsetY = chunkCoord.y * chunkSize;

            for (int cy = minY; cy <= maxY; cy++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    Vector2 tileCenter = new Vector2(cx + 0.5f, cy + 0.5f);
                    if ((tileCenter - candidateTilePos).sqrMagnitude > r2) continue;

                    TerrainType t;
                    if (cx >= 0 && cx < chunkSize && cy >= 0 && cy < chunkSize)
                    {
                        t = chunkData.GetTerrain(cx, cy);
                    }
                    else
                    {
                        t = TerrainGenerator.SampleAt(
                            worldOffsetX + cx, worldOffsetY + cy,
                            genConfig, worldSeed);
                    }

                    if (!rule.allowedTerrain.Contains(t)) return false;
                }
            }

            return true;
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