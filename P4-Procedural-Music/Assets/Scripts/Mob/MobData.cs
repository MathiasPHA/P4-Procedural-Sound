using UnityEngine;
using InventorySystem.Data;
using ProceduralTerrain;

namespace MobSystem.Data
{
    /// <summary>
    /// Defines a mob type's stats, behaviour tuning, visuals, and drops.
    /// One asset per mob variety (Rabbit, Wolf, Shadow, Boar, etc.).
    ///
    /// CREATE: Right-click → Create → Mobs → Mob Data
    ///
    /// The MobBehaviour field determines which states are available at runtime:
    ///   Passive  → Roaming, Fleeing
    ///   Hostile  → Roaming, Chasing, Attacking, Searching, Fleeing
    ///   Neutral  → Starts passive, gains hostile states when provoked
    ///
    /// Fields under "Hostile Behaviour" and "Searching" are ignored for
    /// Passive mobs — they're still visible in the inspector for clarity,
    /// but MobController skips them when the behaviour doesn't use those states.
    /// </summary>
    [CreateAssetMenu(fileName = "New Mob", menuName = "Mobs/Mob Data")]
    public class MobData : ScriptableObject
    {
        // ───────────────────────── Identity ─────────────────────────

        [Header("Identity")]
        [Tooltip("Unique identifier for saving/loading (e.g. 'rabbit', 'wolf').")]
        public string id;

        [Tooltip("Display name shown in UI or tooltips.")]
        public string displayName;

        [Tooltip("Core behaviour archetype — determines available states.")]
        public MobBehaviour behaviour = MobBehaviour.Passive;

        // ───────────────────────── Stats ─────────────────────────

        [Header("Stats")]
        [Tooltip("Maximum health points.")]
        [Min(1)]
        public int maxHealth = 5;

        [Tooltip("Base movement speed (units/sec) while roaming.")]
        [Min(0.1f)]
        public float moveSpeed = 2f;

        // ───────────────────────── Detection ─────────────────────────

        [Header("Detection")]
        [Tooltip("Radius (tiles) at which this mob notices the player. " +
                 "For passive mobs this triggers fleeing; for hostile mobs this triggers chasing.")]
        [Min(0.5f)]
        public float detectionRange = 5f;

        [Tooltip("Seconds between detection checks. Higher = cheaper but less responsive. " +
                 "Staggered automatically so not all mobs check the same frame.")]
        [Range(0.1f, 1f)]
        public float detectionInterval = 0.25f;

        // ───────────────────────── Fleeing (Passive + Neutral + low-HP Hostile) ─────────────────────────

        [Header("Fleeing")]
        [Tooltip("Speed multiplier while fleeing (applied on top of moveSpeed).")]
        [Range(1f, 3f)]
        public float fleeSpeedMultiplier = 1.5f;

        [Tooltip("Distance (tiles) the mob tries to put between itself and the threat before calming down.")]
        [Min(1f)]
        public float fleeDistance = 8f;

        [Tooltip("For hostile mobs: flee when health drops below this fraction of maxHealth. " +
                 "Set to 0 to never flee (fight to the death).")]
        [Range(0f, 1f)]
        public float fleeHealthThreshold = 0f;

        // ───────────────────────── Hostile Behaviour ─────────────────────────

        [Header("Hostile Behaviour")]
        [Tooltip("Radius (tiles) at which a hostile mob initiates aggro. " +
                 "Usually equal to or slightly less than detectionRange. " +
                 "Ignored for Passive mobs.")]
        [Min(0.5f)]
        public float aggroRange = 6f;

        [Tooltip("Distance (tiles) within which the mob can deal damage to the player. " +
                 "Ignored for Passive mobs.")]
        [Min(0.1f)]
        public float attackRange = 1.2f;

        [Tooltip("Damage dealt per attack. Ignored for Passive mobs.")]
        [Min(0)]
        public int attackDamage = 3;

        [Tooltip("Seconds between attacks while in Attacking state. Ignored for Passive mobs.")]
        [Min(0.1f)]
        public float attackCooldown = 1.5f;

        // ───────────────────────── Searching ─────────────────────────

        [Header("Searching")]
        [Tooltip("Seconds the mob searches at the player's last known position " +
                 "before giving up and returning to Roaming. Ignored for Passive mobs.")]
        [Min(0.5f)]
        public float searchDuration = 4f;

        [Tooltip("Radius (tiles) the mob wanders while searching. Ignored for Passive mobs.")]
        [Min(0.5f)]
        public float searchRadius = 3f;

        // ───────────────────────── Roaming ─────────────────────────

        [Header("Roaming")]
        [Tooltip("Maximum distance (tiles) from spawn point for a random wander target.")]
        [Min(1f)]
        public float roamRadius = 4f;

        [Tooltip("Seconds the mob idles at a waypoint before picking a new one.")]
        [Min(0f)]
        public float roamIdleTime = 2f;

        // ───────────────────────── Spawning ─────────────────────────

        [Header("Spawning")]
        [Tooltip("Which terrain types this mob can spawn on.")]
        public TerrainType[] allowedTerrain = new[] { TerrainType.Grass };

        [Tooltip("Earliest hour this mob can spawn (0–24). Use with spawnTimeEnd for night-only mobs.")]
        [Range(0f, 24f)]
        public float spawnTimeStart = 0f;

        [Tooltip("Latest hour this mob can spawn (0–24). If spawnTimeEnd < spawnTimeStart, " +
                 "the range wraps around midnight (e.g. 18–6 = nighttime).")]
        [Range(0f, 24f)]
        public float spawnTimeEnd = 24f;

        [Tooltip("Min herd/pack size when this mob spawns. " +
                 "Each group spawns this many individuals clustered together.")]
        [Min(1)]
        public int groupSizeMin = 1;

        [Tooltip("Max herd/pack size (inclusive).")]
        [Min(1)]
        public int groupSizeMax = 1;

        // ───────────────────────── Visuals ─────────────────────────

        [Header("Visuals")]
        [Tooltip("Prefab to instantiate. Must have SpriteRenderer, Rigidbody2D, Collider2D. " +
                 "MobController will be added at runtime if not already present.")]
        public GameObject prefab;

        // ───────────────────────── Audio ─────────────────────────

        [Header("Audio")]
        [Tooltip("Ambient sound played randomly while roaming (e.g. rabbit squeaks).")]
        public AudioClip ambientSound;
        [Range(0f, 1f)]
        public float ambientVolume = 0.3f;

        [Tooltip("Sound played when the mob takes damage.")]
        public AudioClip hitSound;
        [Range(0f, 1f)]
        public float hitVolume = 0.5f;

        [Tooltip("Sound played when the mob dies.")]
        public AudioClip deathSound;
        [Range(0f, 1f)]
        public float deathVolume = 0.5f;

        [Tooltip("Sound played when a hostile mob starts attacking.")]
        public AudioClip attackSound;
        [Range(0f, 1f)]
        public float attackVolume = 0.5f;

        // ───────────────────────── Drops ─────────────────────────

        [Header("Drops")]
        [Tooltip("Loot table evaluated on death. Leave empty for no drops.")]
        public LootTable lootTable;

        [Tooltip("Prefab with WorldItem component for spawning drops. " +
                 "Same prefab used by HarvestableResource.")]
        public GameObject worldItemPrefab;

        // ───────────────────────── Helpers ─────────────────────────

        /// <summary>
        /// Whether this mob uses hostile states (Chasing, Attacking, Searching).
        /// Neutral mobs gain these states after being provoked at runtime.
        /// </summary>
        public bool HasHostileStates => behaviour == MobBehaviour.Hostile
                                     || behaviour == MobBehaviour.Neutral;

        /// <summary>
        /// Check whether the given time of day falls within this mob's spawn window.
        /// Handles midnight wraparound (e.g. 18:00–06:00).
        /// </summary>
        public bool CanSpawnAtTime(float currentHour)
        {
            if (spawnTimeStart <= spawnTimeEnd)
            {
                // Simple range: e.g. 5–20
                return currentHour >= spawnTimeStart && currentHour <= spawnTimeEnd;
            }
            else
            {
                // Wraps midnight: e.g. 18–6 means 18–24 OR 0–6
                return currentHour >= spawnTimeStart || currentHour <= spawnTimeEnd;
            }
        }
    }
}
