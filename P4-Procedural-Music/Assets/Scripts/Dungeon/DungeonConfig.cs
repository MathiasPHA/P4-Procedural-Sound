using UnityEngine;
using ProceduralMusic.Bridge;

[CreateAssetMenu(fileName = "NewDungeonConfig", menuName = "Dungeon/Dungeon Config")]
public class DungeonConfig : ScriptableObject
{
    [Header("Dungeon Identity")]
    public string dungeonName = "Cave";
    public DungeonType dungeonType = DungeonType.Cave;

    [Header("Generation Settings")]
    [Range(1, 20)] public int minRooms = 5;
    [Range(1, 20)] public int maxRooms = 10;
    [Range(3, 30)] public int minRoomWidth = 7;
    [Range(3, 30)] public int maxRoomWidth = 14;
    [Range(3, 30)] public int minRoomHeight = 7;
    [Range(3, 30)] public int maxRoomHeight = 12;
    [Range(1, 5)] public int corridorWidth = 2;
    [Range(3, 15)] public int entranceHallwayLength = 8;
    [Range(1, 10)] public int wallThickness = 3;

    [Header("Grid")]
    public int gridWidth = 80;
    public int gridHeight = 80;

    [Header("Difficulty")]
    [Range(1, 10)] public int difficultyTier = 1;
    [Range(0f, 1f)] public float enemyDensity = 0.3f;
    [Range(0f, 1f)] public float lootDensity = 0.2f;

    [Header("Object Spawning — Rooms")]
    [Tooltip("Max total objects across all room spawn entries (0 = unlimited)")]
    public int maxTotalRoomSpawns = 0;

    [Tooltip("Objects that can spawn inside rooms")]
    public DungeonSpawnEntry[] roomSpawns;

    [Header("Object Spawning — Corridors")]
    [Tooltip("Max total objects across all corridor spawn entries (0 = unlimited)")]
    public int maxTotalCorridorSpawns = 0;

    [Tooltip("Objects that can spawn inside corridors")]
    public DungeonSpawnEntry[] corridorSpawns;

    [Header("Atmosphere — Initial Conditions")]
    [Tooltip("Immediate happiness hit when entering this dungeon")]
    [Range(-1f, 0f)] public float happinessModifier = -0.3f;

    [Tooltip("Comfort value the dungeon itself radiates (low = oppressive)")]
    [Range(0f, 0.5f)] public float dungeonComfortValue = 0.15f;

    [Header("Tension → Music State Thresholds")]
    [Tooltip("Below this tension = first state (calmest dungeon state)")]
    [Range(0f, 1f)] public float threshold1Max = 0.3f;

    [Tooltip("Above this = third state")]
    [Range(0f, 1f)] public float threshold2Min = 0.55f;

    [Tooltip("Above this = fourth state (most intense)")]
    [Range(0f, 1f)] public float threshold3Min = 0.8f;

    [Header("Tension → Music State Mapping")]
    [Tooltip("Lowest tension state (e.g. exploring a quiet cave)")]
    public GameMusicState stateLow = GameMusicState.Spooky;

    [Tooltip("Default mid-tension state")]
    public GameMusicState stateMid = GameMusicState.Pressure;

    [Tooltip("High tension state")]
    public GameMusicState stateHigh = GameMusicState.Horror;

    [Tooltip("Maximum tension state (e.g. combat, boss encounter)")]
    public GameMusicState stateMax = GameMusicState.Combat;

    /// <summary>
    /// Given a tension value, return the appropriate music state for this dungeon.
    /// </summary>
    public GameMusicState EvaluateState(float tension)
    {
        if (tension >= threshold3Min) return stateMax;
        if (tension >= threshold2Min) return stateHigh;
        if (tension <= threshold1Max) return stateLow;
        return stateMid;
    }
}

public enum DungeonType
{
    Cave,
    Crypt,
    Burrow,
    Ruins
}

[System.Serializable]
public class DungeonSpawnEntry
{
    [Tooltip("Enable/disable this spawn entry for testing")]
    public bool enabled = true;

    [Tooltip("Prefab to spawn")]
    public GameObject prefab;

    [Tooltip("Chance per eligible cell (0 = never, 1 = every cell)")]
    [Range(0f, 1f)] public float density = 0.05f;

    [Tooltip("Max instances across the entire dungeon (0 = unlimited)")]
    public int maxCount = 0;

    [Tooltip("Minimum distance in tiles between instances of this object")]
    [Range(0, 10)] public int minSpacing = 2;

    [Tooltip("Keep away from walls — minimum tiles from nearest wall")]
    [Range(0, 5)] public int wallPadding = 1;

    [Header("Noise Scatter")]
    [Tooltip("Enable Perlin noise clustering instead of uniform random.")]
    public bool useNoise = true;

    [Range(0.01f, 1f)]
    [Tooltip("Lower = larger clusters, higher = smaller/tighter clusters.")]
    public float noiseScale = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Cells below this noise value won't spawn. Higher = sparser clusters.")]
    public float noiseThreshold = 0.45f;
}