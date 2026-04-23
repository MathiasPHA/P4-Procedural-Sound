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
    /// WORLD SCALE: Defaults assume 1 tile = 20 Unity units (100 PPU sprites).
    /// All distance fields are in Unity units, not tiles.
    ///
    /// The MobBehaviour field determines which states are available at runtime:
    ///   Passive  → Roaming, Alert, Fleeing
    ///   Hostile  → Roaming, Alert, Chasing, Attacking, Searching, Fleeing
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
        [Min(2f)]
        public float moveSpeed = 40f;

        // ───────────────────────── Detection & Awareness ─────────────────────────

        [Header("Detection & Awareness")]
        [Tooltip("Radius (units) at which this mob can visually detect the player.")]
        [Min(10f)]
        public float detectionRange = 100f;

        [Tooltip("Radius (units) at which this mob can hear combat or loud events. " +
                 "Works through obstacles unlike visual detection.")]
        [Min(10f)]
        public float hearingRange = 160f;

        [Tooltip("Seconds between detection checks. Higher = cheaper but less responsive.")]
        [Range(0.1f, 1f)]
        public float detectionInterval = 0.25f;

        [Tooltip("How fast awareness builds from visual contact (awareness/sec at point-blank). " +
                 "Higher = more alert and responsive.")]
        [Range(0.3f, 5f)]
        public float visualGainRate = 1.5f;

        [Tooltip("How fast awareness builds from auditory stimuli (awareness/sec). " +
                 "Usually slower than visual.")]
        [Range(0.1f, 2f)]
        public float auditoryGainRate = 0.6f;

        [Tooltip("How fast awareness decays when there's no stimulus (awareness/sec). " +
                 "Lower = longer memory. Should be slower than gain rates.")]
        [Range(0.1f, 2f)]
        public float awarenessDecayRate = 0.4f;

        [Tooltip("Awareness level (0–1) that triggers the Alert state — the mob " +
                 "notices something and turns toward it. Lower = more perceptive.")]
        [Range(0.1f, 0.9f)]
        public float suspiciousThreshold = 0.5f;

        // ───────────────────────── Steering ─────────────────────────

        [Header("Steering")]
        [Tooltip("Radius (units) within which other mobs cause separation force. " +
                 "Larger = wider spread in groups.")]
        [Range(10f, 80f)]
        public float separationRadius = 30f;

        [Tooltip("Strength of separation force pushing mobs apart. " +
                 "Higher = more aggressive spacing.")]
        [Range(0f, 3f)]
        public float separationWeight = 1.2f;

        [Tooltip("CircleCast distance (units) for obstacle avoidance. " +
                 "Longer = earlier steering around obstacles.")]
        [Range(10f, 100f)]
        public float avoidanceDistance = 40f;

        [Tooltip("Strength of obstacle avoidance force. " +
                 "Higher = harder swerve away from obstacles.")]
        [Range(0f, 3f)]
        public float avoidanceWeight = 1.5f;

        [Tooltip("How much random drift affects wander movement (0–1). " +
                 "Higher = more erratic idle movement.")]
        [Range(0.1f, 1f)]
        public float wanderStrength = 0.4f;

        // ───────────────────────── Fleeing (Passive + Neutral + low-HP Hostile) ─────────────────────────

        [Header("Fleeing")]
        [Tooltip("Speed multiplier while fleeing (applied on top of moveSpeed).")]
        [Range(1f, 3f)]
        public float fleeSpeedMultiplier = 1.5f;

        [Tooltip("Distance (units) the mob tries to put between itself and the threat before calming down.")]
        [Min(20f)]
        public float fleeDistance = 160f;

        [Tooltip("For hostile mobs: flee when health drops below this fraction of maxHealth. " +
                 "Set to 0 to never flee (fight to the death).")]
        [Range(0f, 1f)]
        public float fleeHealthThreshold = 0f;

        // ───────────────────────── Hostile Behaviour ─────────────────────────

        [Header("Hostile Behaviour")]
        [Tooltip("Radius (units) at which a hostile mob initiates aggro. " +
                 "Usually equal to or slightly less than detectionRange. " +
                 "Ignored for Passive mobs.")]
        [Min(10f)]
        public float aggroRange = 120f;

        [Tooltip("Distance (units) within which the mob can deal damage to the player. " +
                 "Ignored for Passive mobs.")]
        [Min(2f)]
        public float attackRange = 24f;

        [Tooltip("Happiness lost when a strike lands (0–1 scale). " +
                 "This is the primary 'damage' — a wolf might take 0.08 per hit, " +
                 "a shadow 0.15. At 0.7 starting happiness, ~9 wolf hits = death.")]
        [Range(0.01f, 0.3f)]
        public float happinessPenalty = 0.08f;

        // ───────────────────────── Comfort Threat ─────────────────────────

        [Header("Comfort Threat")]
        [Tooltip("Comfort value this mob's presence pushes toward (0–1). " +
                 "Below 0.5 = threatening (wolves, shadows). " +
                 "At 0.5 = neutral (no passive effect). " +
                 "Above 0.5 = comforting (friendly NPCs, if you ever add them).")]
        [Range(0f, 1f)]
        public float threatComfortValue = 0.3f;

        [Tooltip("Radius (units) of the passive comfort aura. " +
                 "Players inside this radius have their comfort affected.")]
        [Min(20f)]
        public float threatRadius = 300f;

        // ───────────────────────── Attack Phases ─────────────────────────

        [Header("Attack Phases")]
        [Tooltip("Windup duration (sec). The mob stops, telegraphs, and commits to attacking. " +
                 "This is the player's window to dodge. Longer = easier to dodge.")]
        [Range(0.1f, 2f)]
        public float attackWindupDuration = 0.5f;

        [Tooltip("Strike duration (sec). The hitbox is active, and the mob lunges forward. " +
                 "Very short — this is the commit window where damage can land.")]
        [Range(0.05f, 0.6f)]
        public float attackStrikeDuration = 0.25f;

        [Tooltip("Recovery duration (sec). The mob is locked in place, unable to move or attack. " +
                 "Vulnerable window where the player can counter-attack.")]
        [Range(0.1f, 1.5f)]
        public float attackRecoveryDuration = 0.4f;

        [Tooltip("Fraction of windup the mob spends tracking the player (0–1). " +
                 "After this, direction locks — giving the player a clear 'commit' signal. " +
                 "0.5 = tracks for first half, then locks. Lower = easier to dodge.")]
        [Range(0f, 1f)]
        public float attackAimLockRatio = 0.5f;

        [Tooltip("Distance (units) in front of the mob where the hitbox center is placed. " +
                 "Should roughly match the mob's 'reach'.")]
        [Min(2f)]
        public float attackHitboxOffset = 14f;

        [Tooltip("Radius (units) of the damage hitbox during the strike phase. " +
                 "Larger = harder to dodge. A player inside this circle when the strike " +
                 "checks will take damage.")]
        [Min(2f)]
        public float attackHitboxRadius = 16f;

        [Tooltip("Speed (units/sec) of the forward lunge during the strike phase. " +
                 "0 = no lunge (stationary strike). Higher = harder to dodge by running " +
                 "backward, since the mob catches up mid-strike.")]
        [Min(0f)]
        public float attackLungeSpeed = 80f;

        // ───────────────────────── Searching ─────────────────────────

        [Header("Searching")]
        [Tooltip("Seconds the mob searches at the player's last known position " +
                 "before giving up and returning to Roaming. Ignored for Passive mobs.")]
        [Min(0.5f)]
        public float searchDuration = 4f;

        [Tooltip("Radius (units) the mob wanders while searching. Ignored for Passive mobs.")]
        [Min(10f)]
        public float searchRadius = 60f;

        // ───────────────────────── Roaming ─────────────────────────

        [Header("Roaming")]
        [Tooltip("Maximum distance (units) from spawn point for a random wander target.")]
        [Min(20f)]
        public float roamRadius = 80f;

        [Tooltip("Seconds the mob idles at a waypoint before picking a new one.")]
        [Min(0f)]
        public float roamIdleTime = 2f;

        // ───────────────────────── Pack Coordination ─────────────────────────

        [Header("Pack Coordination")]
        [Tooltip("Radius (units) within which pack members are alerted when one mob " +
                 "detects the player. Only same-type mobs are alerted.")]
        [Min(20f)]
        public float packAlertRadius = 200f;

        [Tooltip("Awareness boost applied to pack members when alerted (0–1). " +
                 "Higher = faster group reaction. 0.7 = almost instant alert.")]
        [Range(0.1f, 1f)]
        public float packAlertBoost = 0.7f;

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

        [Tooltip("Min herd/pack size when this mob spawns.")]
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

        [Tooltip("Telegraph sound at the start of an attack windup (growl, hiss, weapon raise).")]
        public AudioClip attackWindupSound;
        [Range(0f, 1f)]
        public float attackWindupVolume = 0.5f;

        [Tooltip("Impact sound when the strike lands or swings (bite, slash, thud).")]
        public AudioClip attackStrikeSound;
        [Range(0f, 1f)]
        public float attackStrikeVolume = 0.5f;

        // ───────────────────────── Drops ─────────────────────────

        [Header("Drops")]
        [Tooltip("Loot table evaluated on death. Leave empty for no drops.")]
        public LootTable lootTable;

        [Tooltip("Prefab with WorldItem component for spawning drops.")]
        public GameObject worldItemPrefab;

         // ───────────────────────── Catchable ─────────────────────────
 
        [Header("Catchable")]
        [Tooltip("If true, the player can catch this mob with a Net. " +
                 "CatchableMob is added to instances automatically at spawn.")]
        public bool isCatchable = false;
 
        [Tooltip("Item added to the player's inventory on a successful catch " +
                 "(e.g. a 'Firefly' ItemData ScriptableObject). " +
                 "Leave empty to despawn the mob without giving anything.")]
        public ItemData catchResultItem;

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
                return currentHour >= spawnTimeStart && currentHour <= spawnTimeEnd;
            }
            else
            {
                return currentHour >= spawnTimeStart || currentHour <= spawnTimeEnd;
            }
        }

       
    }
            

}