using System.Collections.Generic;
using UnityEngine;

public class DungeonObjectSpawner : MonoBehaviour
{
    [SerializeField] private DungeonGenerator generator;

    [Range(0f, 0.5f)]
    [Tooltip("How far objects scatter from cell center (fraction of cell size). 0 = grid-locked, 0.5 = full cell width.")]
    [SerializeField] private float scatterRange = 0.45f;

    private System.Random rng;

    private void Start()
    {
        if (generator == null)
            generator = GetComponent<DungeonGenerator>();

        if (generator == null)
        {
            Debug.LogError("[DungeonObjectSpawner] No DungeonGenerator found.");
            return;
        }

        StartCoroutine(SpawnAfterGeneration());
    }

    private System.Collections.IEnumerator SpawnAfterGeneration()
    {
        yield return null;

        var config = DungeonManager.Instance != null && DungeonManager.Instance.ActiveConfig != null
            ? DungeonManager.Instance.ActiveConfig
            : generator.ActiveConfig;

        if (config == null) yield break;

        rng = new System.Random(generator.ActiveSeed + 7919);

        if (config.roomSpawns != null)
            SpawnEntries(config.roomSpawns, DungeonGenerator.CellType.Room, config.maxTotalRoomSpawns);

        if (config.corridorSpawns != null)
            SpawnEntries(config.corridorSpawns, DungeonGenerator.CellType.Corridor, config.maxTotalCorridorSpawns);

        // One sync + cleanup pass instead of per-object
        Physics2D.SyncTransforms();
        RemoveOverlapping();
    }

    private struct SpawnCandidate
    {
        public Vector2Int cell;
        public int entryIdx;
    }

    private void SpawnEntries(DungeonSpawnEntry[] entries, DungeonGenerator.CellType targetType, int totalMax)
    {
        int w = generator.GridWidth;
        int h = generator.GridHeight;
        var cells = GetShuffledCells(targetType, w, h);

        // Pre-generate noise offsets per entry
        var noiseOffsets = new Vector2[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            noiseOffsets[i] = new Vector2(
                (float)(rng.NextDouble() * 10000.0),
                (float)(rng.NextDouble() * 10000.0));

        // Build candidates: every valid cell+entry pair
        var candidates = new List<SpawnCandidate>();
        for (int entryIdx = 0; entryIdx < entries.Length; entryIdx++)
        {
            var entry = entries[entryIdx];
            if (entry.prefab == null || entry.density <= 0f || !entry.enabled) continue;

            foreach (var cell in cells)
            {
                if (entry.useNoise)
                {
                    float nx = (cell.x + noiseOffsets[entryIdx].x) * entry.noiseScale;
                    float ny = (cell.y + noiseOffsets[entryIdx].y) * entry.noiseScale;
                    if (Mathf.PerlinNoise(nx, ny) < entry.noiseThreshold) continue;
                    if (rng.NextDouble() > entry.density) continue;
                }
                else
                {
                    if (rng.NextDouble() > entry.density) continue;
                }

                if (generator.CellCenterBlocked(cell.x, cell.y)) continue;

                int pad = targetType == DungeonGenerator.CellType.Room
                    ? Mathf.Max(1, entry.wallPadding) : 0;
                if (pad > 0 && !HasClearance(cell.x, cell.y, pad, w, h)) continue;

                candidates.Add(new SpawnCandidate { cell = cell, entryIdx = entryIdx });
            }
        }

        // Shuffle so no entry has priority
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        // Spawn from shuffled list
        int totalSpawned = 0;
        var spawnedPerEntry = new int[entries.Length];
        var placedPerEntry = new List<Vector2Int>[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            placedPerEntry[i] = new List<Vector2Int>();

        foreach (var candidate in candidates)
        {
            if (totalMax > 0 && totalSpawned >= totalMax) break;

            var entry = entries[candidate.entryIdx];
            if (entry.maxCount > 0 && spawnedPerEntry[candidate.entryIdx] >= entry.maxCount) continue;

            var placed = placedPerEntry[candidate.entryIdx];
            if (entry.minSpacing > 0 && TooClose(candidate.cell, placed, entry.minSpacing)) continue;

            Vector3 localPos = generator.GridToLocal(candidate.cell);
            localPos += RandomOffset(candidate.cell, w, h);

            var obj = Instantiate(entry.prefab, transform);
            obj.transform.position = generator.FloorTilemapTransform.TransformPoint(localPos);

            placed.Add(candidate.cell);
            spawnedPerEntry[candidate.entryIdx]++;
            totalSpawned++;
        }
    }

    private Vector3 RandomOffset(Vector2Int cell, int w, int h)
    {
        float range = generator.CellSize * scatterRange;
        float ox = ((float)rng.NextDouble() - 0.5f) * 2f * range;
        float oy = ((float)rng.NextDouble() - 0.5f) * 2f * range;

        // Push away from adjacent walls so objects don't clip into them
        if (cell.x <= 0 || generator.GetCellType(cell.x - 1, cell.y) == DungeonGenerator.CellType.Wall) ox = Mathf.Abs(ox);
        if (cell.x >= w - 1 || generator.GetCellType(cell.x + 1, cell.y) == DungeonGenerator.CellType.Wall) ox = -Mathf.Abs(ox);
        if (cell.y <= 0 || generator.GetCellType(cell.x, cell.y - 1) == DungeonGenerator.CellType.Wall) oy = Mathf.Abs(oy);
        if (cell.y >= h - 1 || generator.GetCellType(cell.x, cell.y + 1) == DungeonGenerator.CellType.Wall) oy = -Mathf.Abs(oy);

        return new Vector3(ox, oy, 0f);
    }

    private List<Vector2Int> GetShuffledCells(DungeonGenerator.CellType type, int w, int h)
    {
        var cells = new List<Vector2Int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var ct = generator.GetCellType(x, y);
                // Entrance hallway counts as corridor for spawning
                if (ct == type || (type == DungeonGenerator.CellType.Corridor && ct == DungeonGenerator.CellType.Entrance))
                    cells.Add(new Vector2Int(x, y));
            }

        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
        return cells;
    }

    private bool HasClearance(int cx, int cy, int pad, int w, int h)
    {
        for (int dx = -pad; dx <= pad; dx++)
            for (int dy = -pad; dy <= pad; dy++)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) return false;
                if (generator.GetCellType(nx, ny) == DungeonGenerator.CellType.Wall) return false;
            }
        return true;
    }

    private bool TooClose(Vector2Int pos, List<Vector2Int> placed, int minDist)
    {
        int sqr = minDist * minDist;
        foreach (var p in placed)
        {
            int dx = pos.x - p.x, dy = pos.y - p.y;
            if (dx * dx + dy * dy < sqr) return true;
        }
        return false;
    }

    private void RemoveOverlapping()
    {
        var toDestroy = new HashSet<GameObject>();

        // Shuffle child order so no entry type gets priority
        var children = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
            children.Add(transform.GetChild(i).gameObject);

        for (int i = children.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (children[i], children[j]) = (children[j], children[i]);
        }

        foreach (var child in children)
        {
            if (toDestroy.Contains(child)) continue;

            var col = child.GetComponentInChildren<Collider2D>();
            if (col == null) continue;

            var bounds = col.bounds;
            var hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);

            foreach (var hit in hits)
            {
                if (hit.gameObject == child) continue;
                if (hit.transform.IsChildOf(child.transform)) continue;
                if (hit.gameObject.name == "DungeonCollision") continue;

                if (hit.transform.parent == transform && !toDestroy.Contains(hit.gameObject))
                {
                    toDestroy.Add(hit.gameObject);
                }
            }
        }

        foreach (var obj in toDestroy)
            Destroy(obj);

        if (toDestroy.Count > 0)
            Debug.Log($"[DungeonObjectSpawner] Removed {toDestroy.Count} overlapping objects.");
    }
}