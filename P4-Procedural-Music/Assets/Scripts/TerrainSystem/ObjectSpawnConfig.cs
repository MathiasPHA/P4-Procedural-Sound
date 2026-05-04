using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Defines what objects can spawn and how dense/clustered they are.
    /// Create via: Assets > Create > Procedural Terrain > Object Spawn Config
    /// </summary>
    [CreateAssetMenu(fileName = "ObjectSpawnConfig", menuName = "Procedural Terrain/Object Spawn Config")]
    public class ObjectSpawnConfig : ScriptableObject
    {
        [System.Serializable]
        public class SpawnRule
        {
            [Tooltip("Name for debugging.")]
            public string name = "Tree";

            [Header("Toggle")]
            [Tooltip("Enable/disable this rule. Disabled rules won't spawn objects.")]
            public bool enabled = true;

            [Tooltip("Prefab(s) to spawn. Picks randomly if multiple.")]
            public List<GameObject> prefabs = new List<GameObject>();

            [Header("Density")]
            [Tooltip("Base chance to spawn per grass tile (0-1).")]
            [Range(0f, 1f)]
            public float baseChance = 0.05f;

            [Tooltip("Scale of the density noise. Smaller = larger clusters.")]
            [Range(0.005f, 0.2f)]
            public float densityNoiseScale = 0.04f;

            [Tooltip("Noise below this = no spawning. Creates empty patches.")]
            [Range(0f, 1f)]
            public float densityCutoff = 0.3f;

            [Tooltip("Multiplier on spawn chance in dense areas.")]
            [Range(1f, 5f)]
            public float densityMultiplier = 2f;

            [Header("Placement")]
            [Tooltip("Minimum distance (in tiles) this object claims from any other spawned object, " +
                     "across all rules. When two objects are checked, the larger of the two minSpacing " +
                     "values is used as the required clearance.")]
            [Range(0f, 50f)]
            public float minSpacing = 1.5f;

            [Tooltip("Required distance (in WORLD UNITS) from any disallowed terrain. 0 = no check " +
                     "(only the spawn cell itself is verified). Use this when the object's sprite is " +
                     "wider than one cell — e.g. a tree sprite ~30 units wide on a 20-unit grid wants " +
                     "a clearance of ~15 so its canopy doesn't visually overhang water at coastlines. " +
                     "Setting this ≥ cellSize/2 effectively bans the object from coastline cells.")]
            public float clearanceRadius = 0f;

            [Tooltip("Required distance (in WORLD UNITS) from any other spawned object, across all rules. " +
                     "When two objects are checked, the larger of the two values is used. " +
                     "Use this for sprite-overlap prevention when sprites are wider than a tile — " +
                     "e.g. a tree sprite ~30 units wide wants ~25 to avoid canopy overlap. " +
                     "Stacks with minSpacing — both checks run; whichever rejects first wins.")]
            public float objectClearance = 0f;

            [Tooltip("Max instances per chunk (0 = unlimited).")]
            public int maxCountPerChunk = 0;

            [Tooltip("Random offset from tile center (in tile units). Prevents grid look.")]
            [Range(0f, 0.45f)]
            public float positionJitter = 0.3f;

            [Tooltip("Which terrain types this can spawn on.")]
            public List<TerrainType> allowedTerrain = new List<TerrainType> { TerrainType.Grass };

            [Tooltip("If enabled, only spawns on water tiles that have at least one grass neighbour " +
                     "(i.e. shore/edge water tiles). Has no effect unless Water is in allowedTerrain.")]
            public bool shoreOnly = false;

            [Header("Sorting")]
            [Tooltip("Sorting order offset. Higher = renders in front.")]
            public int sortingOrderOffset = 0;

            [Tooltip("Unique seed offset so different rules don't mirror each other.")]
            public int seedOffset = 0;

            [Header("Depletion")]
            [Tooltip("Prefab to spawn in place when this object is depleted (e.g. tree stump). " +
                     "Leave empty if the object just disappears (like twigs/mushrooms).")]
            public GameObject depletedPrefab;
        }

        [Tooltip("Spawn rules evaluated in order. Earlier rules block later rules via spacing.")]
        public List<SpawnRule> rules = new List<SpawnRule>();
    }
}