using System.Collections.Generic;
using UnityEngine;

namespace MobSystem.Data
{
    /// <summary>
    /// Configures which mobs can spawn, how many, and how often.
    /// Assigned to MobSpawnManager. Evaluated every spawn tick when
    /// the player enters new chunks or time thresholds cross.
    ///
    /// CREATE: Right-click → Create → Mobs → Mob Spawn Config
    ///
    /// Unlike ObjectSpawnConfig (deterministic, seed-based), mob spawning is
    /// probabilistic and time-dependent — the same chunk won't always have
    /// the same mobs. This is intentional: mobs are dynamic encounters,
    /// not static world features.
    /// </summary>
    [CreateAssetMenu(fileName = "MobSpawnConfig", menuName = "Mobs/Mob Spawn Config")]
    public class MobSpawnConfig : ScriptableObject
    {
        [Header("Global Caps")]
        [Tooltip("Maximum number of mobs alive at any time across all loaded chunks. " +
                 "Prevents performance degradation. Includes all types.")]
        [Min(1)]
        public int globalMobCap = 30;

        [Tooltip("Minimum distance (tiles) between any two mob spawn points. " +
                 "Prevents clumping of different mob types on top of each other.")]
        [Min(0f)]
        public float minSpawnSpacing = 5f;

        [Tooltip("Minimum distance (tiles) from the player for new spawns. " +
                 "Prevents mobs from popping into existence visibly.")]
        [Min(3f)]
        public float minPlayerDistance = 8f;

        [Tooltip("Maximum distance (tiles) from the player for new spawns. " +
                 "Keeps mobs within encounter range — no point spawning them 200 tiles away.")]
        [Min(5f)]
        public float maxPlayerDistance = 30f;

        [Header("Spawn Timing")]
        [Tooltip("Seconds between spawn evaluation ticks. Lower = more responsive " +
                 "but more CPU. The system checks all rules each tick.")]
        [Range(1f, 30f)]
        public float spawnInterval = 5f;

        [Tooltip("Maximum number of mobs to spawn per tick. " +
                 "Prevents large batches from causing frame drops.")]
        [Min(1)]
        public int maxSpawnsPerTick = 3;

        [Header("Spawn Rules")]
        [Tooltip("Rules evaluated in order each spawn tick. " +
                 "Earlier rules with unmet caps get priority.")]
        public List<MobSpawnRule> rules = new List<MobSpawnRule>();

        [System.Serializable]
        public class MobSpawnRule
        {
            [Tooltip("Name for debugging (e.g. 'Forest Rabbits', 'Night Wolves').")]
            public string name = "New Spawn Rule";

            [Tooltip("The mob type this rule spawns.")]
            public MobData mobData;

            [Tooltip("Max alive mobs of this type at any time. " +
                     "Rule is skipped when count is at cap.")]
            [Min(1)]
            public int typeCap = 5;

            [Tooltip("Base probability (0–1) of this rule producing a spawn each tick. " +
                     "Rolled per-tick, not per-chunk.")]
            [Range(0f, 1f)]
            public float spawnChance = 0.3f;

            [Tooltip("Multiplier applied to spawnChance during this mob's active time window. " +
                     "Use > 1 to make wolves much more common at night, or < 1 for rare daytime sightings.")]
            [Range(0.1f, 5f)]
            public float activeTimeMultiplier = 1f;

            [Tooltip("If true, this mob ONLY spawns during its time window. " +
                     "If false, it can spawn anytime but is more likely during the window.")]
            public bool strictTimeWindow = false;
        }
    }
}
