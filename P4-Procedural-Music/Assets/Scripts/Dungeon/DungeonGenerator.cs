using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using ProceduralTerrain;

/// <summary>
/// Procedural dungeon generator using BSP room-and-corridor layout.
/// Paints bitmask-resolved water tiles for floor cells (walkable)
/// and builds PolygonCollider2D collision from DungeonCollisionData
/// so the player can walk on the water but not into walls.
///
/// Uses the same WaterBitmaskResolver as the overworld since
/// the dungeon tileset shares the same tile ID scheme.
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;

    [Header("Tileset")]
    [Tooltip("All tiles from the Dungeon/Forest tileset, indexed by tsx tile ID. " +
             "Drag the sub-assets from the imported .tsx in order.")]
    [SerializeField] private TileBase[] tileset;

    [Header("Tile Size")]
    [Tooltip("Cell size in world units (match your tilemap grid settings)")]
    [SerializeField] private float cellSize = 1f;

    [Header("Spawning")]
    [SerializeField] private GameObject dungeonExitPrefab;

    [Header("Editor Testing")]
    [Tooltip("Assign a DungeonConfig here to test the dungeon scene directly without going through the overworld.")]
    [SerializeField] private DungeonConfig testConfig;
    [SerializeField] private int testSeed = 42;

    private int[,] grid; // 0 = wall, 1 = floor
    private CellType[,] cellTypes;
    private int[] resolvedTileIds; // tsx ID per cell (floor cells only)
    private List<RectInt> rooms = new List<RectInt>();
    private List<Vector2Int> corridorMidpoints = new List<Vector2Int>();
    private DungeonConfig currentConfig;
    private System.Random rng;
    private int gridWidth;
    private int gridHeight;

    public enum CellType { Wall, Room, Corridor, Entrance }

    /// <summary>All generated rooms — available for other systems (e.g. enemy spawning).</summary>
    public IReadOnlyList<RectInt> Rooms => rooms;

    /// <summary>The config used for the current generation (works in editor testing too).</summary>
    public DungeonConfig ActiveConfig => currentConfig;

    /// <summary>The seed used for the current generation.</summary>
    public int ActiveSeed { get; private set; }

    /// <summary>
    /// Returns true if the center of this floor tile is inside one of its
    /// collision polygons. Objects should not spawn on these cells since
    /// they'd overlap with the rocky edges.
    /// </summary>
    public bool CellCenterBlocked(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return true;
        if (grid[x, y] != 1) return true;

        int tsxId = resolvedTileIds[y * gridWidth + x];
        if (tsxId < 0) return true;

        var paths = ProceduralTerrain.DungeonCollisionData.GetCollisionPaths(tsxId);
        if (paths == null) return false; // No collision = safe

        // Check if tile center (0.5, 0.5 in normalized coords) is inside any polygon
        Vector2 center = new Vector2(0.5f, 0.5f);
        foreach (var poly in paths)
        {
            if (PointInPolygon(center, poly))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Ray-casting point-in-polygon test.
    /// </summary>
    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        int count = polygon.Length;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y)
                          / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>Cell type at a grid position. Used by DungeonObjectSpawner.</summary>
    public CellType GetCellType(int x, int y) => cellTypes[x, y];
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float CellSize => cellSize;

    private void Start()
    {
        if (DungeonManager.Instance != null && DungeonManager.Instance.ActiveConfig != null)
        {
            Generate(DungeonManager.Instance.ActiveConfig, DungeonManager.Instance.DungeonSeed);
        }
        else if (testConfig != null)
        {
            Debug.Log($"[DungeonGenerator] No DungeonManager found — using test config '{testConfig.dungeonName}' with seed {testSeed}.");
            Generate(testConfig, testSeed);
        }
        else
        {
            Debug.LogError("[DungeonGenerator] No DungeonManager and no testConfig assigned. Cannot generate.");
        }

        // Debug: log tile positioning info
        Debug.Log($"[DungeonGenerator] tileAnchor={floorTilemap.tileAnchor}, " +
                  $"cellSize={floorTilemap.layoutGrid.cellSize}, " +
                  $"tilemapLocalPos={floorTilemap.transform.localPosition}, " +
                  $"CellToLocal(0,0)={floorTilemap.CellToLocal(Vector3Int.zero)}, " +
                  $"CellToWorld(0,0)={floorTilemap.CellToWorld(Vector3Int.zero)}");
    }

    public void Generate(DungeonConfig config, int seed)
    {
        rng = new System.Random(seed);
        ActiveSeed = seed;
        currentConfig = config;
        gridWidth = config.gridWidth;
        gridHeight = config.gridHeight;
        grid = new int[gridWidth, gridHeight];
        cellTypes = new CellType[gridWidth, gridHeight];
        resolvedTileIds = new int[gridWidth * gridHeight];
        corridorMidpoints.Clear();
        rooms.Clear();

        // Init all resolved IDs to -1 (no tile)
        for (int i = 0; i < resolvedTileIds.Length; i++)
            resolvedTileIds[i] = -1;

        // 1. BSP split into leaf regions
        List<RectInt> leaves = new List<RectInt>();
        BSPSplit(new RectInt(1, 1, gridWidth - 2, gridHeight - 2), config, leaves);

        // 2. Carve rooms inside leaves
        int targetRooms = rng.Next(config.minRooms, config.maxRooms + 1);
        int roomCount = Mathf.Min(targetRooms, leaves.Count);

        for (int i = 0; i < roomCount; i++)
        {
            RectInt room = CarveRoom(leaves[i], config);
            rooms.Add(room);
            FillRect(room, 1, CellType.Room);
        }

        // 3. Connect rooms with L-shaped corridors
        for (int i = 0; i < rooms.Count - 1; i++)
            ConnectRooms(rooms[i], rooms[i + 1], config.corridorWidth);

        // 4. Find the best room for the entrance (most wall space around it)
        int bestRoomIdx = FindBestEntranceRoom(config);
        var entrance = CarveEntranceHallway(rooms[bestRoomIdx], config);

        // 5. Resolve bitmasks for floor tiles
        ResolveTiles();

        // 6. Paint tilemaps
        PaintTilemaps();

        // 7. Generate collision
        Vector3Int tileOffset = new Vector3Int(-gridWidth / 2, -gridHeight / 2, 0);
        DungeonCollisionGenerator.GenerateCollision(
            grid, resolvedTileIds,
            gridWidth, gridHeight,
            cellSize, tileOffset,
            floorTilemap.transform
        );

        // Exit at the entrance tip (way out), player a few tiles inward
        SpawnExitAt(entrance.playerPos);
        SpawnPlayerAt(entrance.exitPos);
    }

    // ===================== Bitmask Resolution =====================

    private void ResolveTiles()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] != 1) continue;

                // Check which neighbors are also floor (= water tile)
                bool nw = IsFloor(x - 1, y + 1);
                bool n = IsFloor(x, y + 1);
                bool ne = IsFloor(x + 1, y + 1);
                bool w = IsFloor(x - 1, y);
                bool e = IsFloor(x + 1, y);
                bool sw = IsFloor(x - 1, y - 1);
                bool s = IsFloor(x, y - 1);
                bool se = IsFloor(x + 1, y - 1);

                int bitmask = WaterBitmaskResolver.BuildBitmask(nw, n, ne, w, e, sw, s, se);
                int tsxId = WaterBitmaskResolver.Resolve(bitmask);

                resolvedTileIds[y * gridWidth + x] = tsxId;
            }
        }
    }

    private bool IsFloor(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return false;
        return grid[x, y] == 1;
    }

    // ===================== BSP =====================

    private void BSPSplit(RectInt area, DungeonConfig config, List<RectInt> leaves)
    {
        int minLeaf = Mathf.Max(config.maxRoomWidth, config.maxRoomHeight) + 2;

        bool canH = area.height >= minLeaf * 2;
        bool canV = area.width >= minLeaf * 2;

        if (!canH && !canV)
        {
            leaves.Add(area);
            return;
        }

        bool splitH;
        if (canH && canV) splitH = rng.NextDouble() > 0.5;
        else splitH = canH;

        if (splitH)
        {
            int y = rng.Next(area.yMin + minLeaf, area.yMax - minLeaf + 1);
            BSPSplit(new RectInt(area.xMin, area.yMin, area.width, y - area.yMin), config, leaves);
            BSPSplit(new RectInt(area.xMin, y, area.width, area.yMax - y), config, leaves);
        }
        else
        {
            int x = rng.Next(area.xMin + minLeaf, area.xMax - minLeaf + 1);
            BSPSplit(new RectInt(area.xMin, area.yMin, x - area.xMin, area.height), config, leaves);
            BSPSplit(new RectInt(x, area.yMin, area.xMax - x, area.height), config, leaves);
        }
    }

    // ===================== Rooms & Corridors =====================

    private RectInt CarveRoom(RectInt leaf, DungeonConfig config)
    {
        int w = rng.Next(config.minRoomWidth, Mathf.Min(config.maxRoomWidth, leaf.width - 1) + 1);
        int h = rng.Next(config.minRoomHeight, Mathf.Min(config.maxRoomHeight, leaf.height - 1) + 1);
        int x = rng.Next(leaf.xMin, leaf.xMax - w + 1);
        int y = rng.Next(leaf.yMin, leaf.yMax - h + 1);
        return new RectInt(x, y, w, h);
    }

    private void ConnectRooms(RectInt a, RectInt b, int width)
    {
        Vector2Int cA = RoomCenterInt(a);
        Vector2Int cB = RoomCenterInt(b);

        if (rng.NextDouble() > 0.5)
        {
            CarveHCorridor(cA.x, cB.x, cA.y, width);
            CarveVCorridor(cA.y, cB.y, cB.x, width);
            corridorMidpoints.Add(new Vector2Int(cB.x, cA.y));
        }
        else
        {
            CarveVCorridor(cA.y, cB.y, cA.x, width);
            CarveHCorridor(cA.x, cB.x, cB.y, width);
            corridorMidpoints.Add(new Vector2Int(cA.x, cB.y));
        }
    }

    private void CarveHCorridor(int x1, int x2, int y, int w)
    {
        CarveHCorridor(x1, x2, y, w, CellType.Corridor);
    }

    private void CarveHCorridor(int x1, int x2, int y, int w, CellType type)
    {
        for (int x = Mathf.Min(x1, x2); x <= Mathf.Max(x1, x2); x++)
            for (int i = 0; i < w; i++)
                SetGrid(x, y + i - w / 2, 1, type);
    }

    private void CarveVCorridor(int y1, int y2, int x, int w)
    {
        CarveVCorridor(y1, y2, x, w, CellType.Corridor);
    }

    private void CarveVCorridor(int y1, int y2, int x, int w, CellType type)
    {
        for (int y = Mathf.Min(y1, y2); y <= Mathf.Max(y1, y2); y++)
            for (int i = 0; i < w; i++)
                SetGrid(x + i - w / 2, y, 1, type);
    }

    /// <summary>
    /// Finds the room with the most wall space in its best direction.
    /// This ensures the entrance hallway has enough room to carve without
    /// overlapping other rooms.
    /// </summary>
    private int FindBestEntranceRoom(DungeonConfig config)
    {
        int bestIdx = 0;
        int bestSpace = 0;

        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            int down = room.yMin - 1;
            int up = (gridHeight - 1) - room.yMax;
            int left = room.xMin - 1;
            int right = (gridWidth - 1) - room.xMax;

            // Best direction for this room
            int maxDir = Mathf.Max(Mathf.Max(down, up), Mathf.Max(left, right));

            // Also check the space is actually clear of other rooms
            if (maxDir > bestSpace)
            {
                bestSpace = maxDir;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    /// <summary>
    /// Carves a dead-end entrance hallway extending outward from the given room.
    /// Picks the direction with the most available wall space.
    /// Returns (playerSpawn, exitSpawn) — player at the far end, exit a couple tiles inward.
    /// </summary>
    private (Vector2Int playerPos, Vector2Int exitPos) CarveEntranceHallway(RectInt room, DungeonConfig config)
    {
        int length = config.entranceHallwayLength;
        int w = config.corridorWidth;
        Vector2Int center = RoomCenterInt(room);

        // Check available space in each direction from the room edge
        // Only count space that's actually wall (not another room)
        int spaceDown = CountWallSpace(center.x, room.yMin - 1, 0, -1);
        int spaceUp = CountWallSpace(center.x, room.yMax, 0, 1);
        int spaceLeft = CountWallSpace(room.xMin - 1, center.y, -1, 0);
        int spaceRight = CountWallSpace(room.xMax, center.y, 1, 0);

        // Pick the direction with the most space
        int bestSpace = spaceDown;
        int dir = 0; // 0=down, 1=up, 2=left, 3=right
        if (spaceUp > bestSpace) { bestSpace = spaceUp; dir = 1; }
        if (spaceLeft > bestSpace) { bestSpace = spaceLeft; dir = 2; }
        if (spaceRight > bestSpace) { bestSpace = spaceRight; dir = 3; }

        // Clamp length to available space (leave border so the tip stays on walkable floor)
        int actualLength = Mathf.Min(length, Mathf.Max(bestSpace - 1, 1));
        int exitOffset = Mathf.Min(4, actualLength - 1); // player 4 tiles inward from exit

        Vector2Int playerPos;
        Vector2Int exitPos;

        switch (dir)
        {
            case 0: // Down
                CarveVCorridor(room.yMin - 1, room.yMin - actualLength, center.x, w, CellType.Entrance);
                playerPos = new Vector2Int(center.x, room.yMin - actualLength + 1);
                exitPos = new Vector2Int(center.x, room.yMin - actualLength + 1 + exitOffset);
                break;
            case 1: // Up
                CarveVCorridor(room.yMax, room.yMax + actualLength, center.x, w, CellType.Entrance);
                playerPos = new Vector2Int(center.x, room.yMax + actualLength - 1);
                exitPos = new Vector2Int(center.x, room.yMax + actualLength - 1 - exitOffset);
                break;
            case 2: // Left
                CarveHCorridor(room.xMin - 1, room.xMin - actualLength, center.y, w, CellType.Entrance);
                playerPos = new Vector2Int(room.xMin - actualLength + 1, center.y);
                exitPos = new Vector2Int(room.xMin - actualLength + 1 + exitOffset, center.y);
                break;
            case 3: // Right
                CarveHCorridor(room.xMax, room.xMax + actualLength, center.y, w, CellType.Entrance);
                playerPos = new Vector2Int(room.xMax + actualLength - 1, center.y);
                exitPos = new Vector2Int(room.xMax + actualLength - 1 - exitOffset, center.y);
                break;
            default:
                playerPos = center;
                exitPos = center;
                break;
        }

        return (playerPos, exitPos);
    }

    private void FillRect(RectInt rect, int val, CellType type)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++)
                SetGrid(x, y, val, type);
    }

    private void SetGrid(int x, int y, int val, CellType type)
    {
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
        {
            grid[x, y] = val;
            // Don't downgrade Room to Corridor
            if (val == 1 && cellTypes[x, y] == CellType.Room && type == CellType.Corridor)
                return;
            if (val == 1) cellTypes[x, y] = type;
        }
    }

    // ===================== Tilemap Painting =====================

    private void PaintTilemaps()
    {
        Vector3Int offset = new Vector3Int(-gridWidth / 2, -gridHeight / 2, 0);
        int thickness = currentConfig.wallThickness;

        var floorPositions = new List<Vector3Int>();
        var floorTiles = new List<TileBase>();
        var wallPositions = new List<Vector3Int>();
        var wallTiles = new List<TileBase>();

        TileBase grassTile = GetTile(WaterBitmaskResolver.GRASS_TILE_ID);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == 1)
                {
                    int tsxId = resolvedTileIds[y * gridWidth + x];
                    TileBase tile = GetTile(tsxId);
                    if (tile != null)
                    {
                        floorPositions.Add(new Vector3Int(x, y, 0) + offset);
                        floorTiles.Add(tile);
                    }
                }
                else if (grassTile != null && IsWithinRangeOfFloor(x, y, thickness))
                {
                    wallPositions.Add(new Vector3Int(x, y, 0) + offset);
                    wallTiles.Add(grassTile);
                }
            }
        }

        floorTilemap.SetTiles(floorPositions.ToArray(), floorTiles.ToArray());
        wallTilemap.SetTiles(wallPositions.ToArray(), wallTiles.ToArray());
    }

    private TileBase GetTile(int tsxId)
    {
        if (tileset == null || tsxId < 0 || tsxId >= tileset.Length)
            return null;
        return tileset[tsxId];
    }

    private bool IsWithinRangeOfFloor(int x, int y, int range)
    {
        for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight && grid[nx, ny] == 1)
                    return true;
            }
        return false;
    }

    // ===================== Spawning =====================

    private void SpawnPlayerAt(Vector2Int gridPos)
    {
        Vector3Int cellPos = new Vector3Int(gridPos.x - gridWidth / 2, gridPos.y - gridHeight / 2, 0);
        Vector3 worldPos = floorTilemap.CellToWorld(cellPos);
        var player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            player.transform.position = worldPos;
        else
            Debug.LogWarning("[DungeonGenerator] No Player found in scene. Make sure a Player-tagged object exists.");
    }

    private void SpawnExitAt(Vector2Int gridPos)
    {
        if (dungeonExitPrefab == null) return;
        Vector3Int cellPos = new Vector3Int(gridPos.x - gridWidth / 2, gridPos.y - gridHeight / 2, 0);
        Vector3 worldPos = floorTilemap.CellToWorld(cellPos);
        Instantiate(dungeonExitPrefab, worldPos, Quaternion.identity);
    }

    /// <summary>
    /// Count how many consecutive wall cells exist starting from (x,y) in direction (dx,dy).
    /// Stops at grid boundary or existing floor tile.
    /// </summary>
    private int CountWallSpace(int x, int y, int dx, int dy)
    {
        int count = 0;
        int cx = x, cy = y;
        while (cx >= 0 && cx < gridWidth && cy >= 0 && cy < gridHeight)
        {
            if (grid[cx, cy] == 1) break; // Hit existing floor (another room/corridor)
            count++;
            cx += dx;
            cy += dy;
        }
        return count;
    }

    private Vector2Int RoomCenterInt(RectInt r) =>
        new Vector2Int(r.xMin + r.width / 2, r.yMin + r.height / 2);

    /// <summary>The floor tilemap transform — use as parent for spawned objects.</summary>
    public Transform FloorTilemapTransform => floorTilemap.transform;

    public Vector3 GridToLocal(Vector2Int gridPos)
    {
        Vector3Int cellPos = new Vector3Int(
            gridPos.x - gridWidth / 2,
            gridPos.y - gridHeight / 2,
            0);

        return floorTilemap.CellToLocal(cellPos)
             + new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0f);
    }

    private Vector3 RoomCenterWorld(RectInt r)
    {
        return floorTilemap.transform.TransformPoint(GridToLocal(RoomCenterInt(r)));
    }

    private void OnDestroy()
    {
        DungeonCollisionGenerator.ClearCollision();
    }
}