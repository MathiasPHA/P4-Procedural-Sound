using System.Collections.Generic;
using UnityEngine;

public class DungeonObjectSpawner : MonoBehaviour
{
    [SerializeField] private DungeonGenerator generator;

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
            SpawnEntries(config.roomSpawns, DungeonGenerator.CellType.Room);

        if (config.corridorSpawns != null)
            SpawnEntries(config.corridorSpawns, DungeonGenerator.CellType.Corridor);
    }

    private void SpawnEntries(DungeonSpawnEntry[] entries, DungeonGenerator.CellType targetType)
    {
        int w = generator.GridWidth;
        int h = generator.GridHeight;
        var cells = GetShuffledCells(targetType, w, h);

        foreach (var entry in entries)
        {
            if (entry.prefab == null || entry.density <= 0f) continue;

            int spawned = 0;
            var placed = new List<Vector2Int>();

            foreach (var cell in cells)
            {
                if (entry.maxCount > 0 && spawned >= entry.maxCount) break;
                if (rng.NextDouble() > entry.density) continue;
                if (generator.CellCenterBlocked(cell.x, cell.y)) continue;

                int pad = targetType == DungeonGenerator.CellType.Room
                    ? Mathf.Max(1, entry.wallPadding) : 0;

                if (pad > 0 && !HasClearance(cell.x, cell.y, pad, w, h)) continue;
                if (entry.minSpacing > 0 && TooClose(cell, placed, entry.minSpacing)) continue;

                Vector3 localPos = generator.GridToLocal(cell);

                if (targetType == DungeonGenerator.CellType.Room)
                    localPos += RandomRoomOffset(cell, w, h);

                var obj = Instantiate(entry.prefab, transform);
                obj.transform.position = generator.FloorTilemapTransform.TransformPoint(localPos);
                placed.Add(cell);
                spawned++;
            }
        }
    }

    private Vector3 RandomRoomOffset(Vector2Int cell, int w, int h)
    {
        float range = generator.CellSize * 0.35f;
        float ox = ((float)rng.NextDouble() - 0.5f) * 2f * range;
        float oy = ((float)rng.NextDouble() - 0.5f) * 2f * range;

        // Push away from adjacent walls
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
                if (generator.GetCellType(x, y) == type)
                    cells.Add(new Vector2Int(x, y));

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
}