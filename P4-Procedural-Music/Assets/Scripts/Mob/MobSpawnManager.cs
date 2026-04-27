using System.Collections.Generic;
using UnityEngine;
using MobSystem.Data;
using MobSystem.States;
using ProceduralTerrain;

namespace MobSystem
{
    /// <summary>
    /// Manages mob lifecycle: spawning, population tracking, and distance-based culling.
    /// Runs independently of the chunk system — mobs are parented to this manager's
    /// transform, not to chunk objects, so they can freely cross chunk boundaries.
    ///
    /// SPAWNING:
    ///   Every spawnInterval seconds, evaluates MobSpawnConfig rules:
    ///     1. Checks per-type and global population caps
    ///     2. Checks time-of-day window via TimeReference
    ///     3. Rolls spawn chance (with activeTimeMultiplier during peak hours)
    ///     4. Finds a valid position: correct terrain, not too close/far from player,
    ///        spaced from existing mobs
    ///     5. Spawns the mob with group size from MobData
    ///
    /// CULLING:
    ///   Mobs in RoamingState that drift beyond cullDistance are despawned.
    ///   Active mobs (chasing, attacking, searching, fleeing) are never culled
    ///   regardless of distance — the player started that encounter and it should
    ///   resolve naturally.
    ///
    /// SETUP:
    ///   1. Create an empty GameObject, attach MobSpawnManager
    ///   2. Assign a MobSpawnConfig asset
    ///   3. Assign the playerLayer mask (same layer as the player collider)
    ///   4. Optionally assign TimeReference for time-gated spawning
    ///   5. ChunkManager.Instance must exist (used for terrain queries and player ref)
    /// </summary>
    public class MobSpawnManager : MonoBehaviour
    {
        public static MobSpawnManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Spawn rules, caps, and timing settings.")]
        [SerializeField] private MobSpawnConfig config;

        [Tooltip("Mob database for ID lookups (optional, for future save/load).")]
        [SerializeField] private MobDatabase mobDatabase;

        [Header("References")]
        [Tooltip("Layer mask for the player — passed to MobController for detection.")]
        [SerializeField] private LayerMask playerLayer;

        [Tooltip("Layer mask for obstacles (trees, rocks, water) — used by mob steering avoidance.")]
        [SerializeField] private LayerMask obstacleMask;

        [Tooltip("Layer mask for other mobs — used by steering separation and pack alerts.")]
        [SerializeField] private LayerMask mobMask;

        [Tooltip("Time system reference for day/night spawn rules. " +
                 "Auto-finds if left empty.")]
        [SerializeField] private TimeReference timeReference;

        [Header("Culling")]
        [Tooltip("Mobs in Roaming state beyond this distance from the player are despawned. " +
                 "Should be larger than maxPlayerDistance to avoid spawn-then-cull loops.")]
        [Min(400f)]
        [SerializeField] private float cullDistance = 900f;

        [Tooltip("Seconds between cull sweeps.")]
        [Range(2f, 30f)]
        [SerializeField] private float cullInterval = 5f;

        [Header("Spawn Position")]
        [Tooltip("Max attempts per tick to find a valid spawn position before giving up. " +
                 "Higher = more reliable spawning but more work per tick.")]
        [Range(3, 20)]
        [SerializeField] private int maxSpawnAttempts = 8;

        [Header("Debug")]
        [SerializeField] private bool showDebugGUI = false;

        // ───────────────────────── Runtime ─────────────────────────

        private readonly List<MobController> _activeMobs = new List<MobController>();
        private readonly Dictionary<string, int> _typeCounts = new Dictionary<string, int>();
        private float _spawnTimer;
        private float _cullTimer;
        private Transform _playerTransform;

        // ───────────────────────── Public Read ─────────────────────────

        /// <summary>All currently alive mobs.</summary>
        public IReadOnlyList<MobController> ActiveMobs => _activeMobs;

        /// <summary>Current total mob count.</summary>
        public int TotalMobCount => _activeMobs.Count;

        // ───────────────────────── Lifecycle ─────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (config == null)
            {
                Debug.LogError("[MobSpawnManager] No MobSpawnConfig assigned!");
                enabled = false;
                return;
            }

            // Find player
            if (ChunkManager.Instance != null)
                _playerTransform = ChunkManager.Instance.player;

            if (_playerTransform == null)
            {
                Debug.LogError("[MobSpawnManager] No player Transform found via ChunkManager!");
                enabled = false;
                return;
            }

            // Auto-find TimeReference
            if (timeReference == null)
            {
                var dayNightObj = GameObject.Find("Day Night System");
                if (dayNightObj != null)
                    timeReference = dayNightObj.GetComponent<TimeReference>();
            }

            // Initialise database if assigned
            if (mobDatabase != null)
                mobDatabase.Initialise();

            // Stagger the first spawn tick so it doesn't coincide with chunk loading
            _spawnTimer = 1f;
            _cullTimer = cullInterval;

            Debug.Log($"[MobSpawnManager] Init: {config.rules.Count} rules, " +
                      $"global cap {config.globalMobCap}, " +
                      $"time ref {(timeReference != null ? "OK" : "MISSING")}");
        }

        private void Update()
        {
            // ── Spawn tick ──
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                RunSpawnTick();
                _spawnTimer = config.spawnInterval;
            }

            // ── Cull tick ──
            _cullTimer -= Time.deltaTime;
            if (_cullTimer <= 0f)
            {
                RunCullSweep();
                _cullTimer = cullInterval;
            }
        }

        // ───────────────────────── Spawning ─────────────────────────

        private void RunSpawnTick()
        {
            if (_activeMobs.Count >= config.globalMobCap) return;

            int spawnsThisTick = 0;

            foreach (var rule in config.rules)
            {
                if (spawnsThisTick >= config.maxSpawnsPerTick) break;
                if (rule.mobData == null) continue;

                // ── Per-type cap ──
                int currentCount = GetTypeCount(rule.mobData.id);
                if (currentCount >= rule.typeCap) continue;

                // ── Global cap ──
                if (_activeMobs.Count >= config.globalMobCap) break;

                // ── Time window ──
                float currentHour = timeReference != null ? timeReference.time : 12f;
                bool inTimeWindow = rule.mobData.CanSpawnAtTime(currentHour);

                if (rule.strictTimeWindow && !inTimeWindow) continue;

                // ── Spawn chance ──
                float chance = rule.spawnChance;
                if (inTimeWindow)
                    chance *= rule.activeTimeMultiplier;

                if (Random.value > chance) continue;

                // ── Find a valid position and spawn ──
                if (TryFindSpawnPosition(rule.mobData, out Vector3 spawnPos))
                {
                    int groupSize = Random.Range(rule.mobData.groupSizeMin,
                                                  rule.mobData.groupSizeMax + 1);

                    for (int i = 0; i < groupSize; i++)
                    {
                        if (_activeMobs.Count >= config.globalMobCap) break;

                        // Offset group members slightly so they don't stack
                        Vector3 offset = i == 0
                            ? Vector3.zero
                            : (Vector3)(Random.insideUnitCircle * 30f);

                        SpawnMob(rule.mobData, spawnPos + offset);
                        spawnsThisTick++;
                    }
                }
            }
        }

        private bool TryFindSpawnPosition(MobData mobData, out Vector3 position)
        {
            position = Vector3.zero;
            Vector2 playerPos = _playerTransform.position;

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                // Random direction and distance from player
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float dist = Random.Range(config.minPlayerDistance, config.maxPlayerDistance);

                Vector2 candidate = playerPos + new Vector2(
                    Mathf.Cos(angle) * dist,
                    Mathf.Sin(angle) * dist
                );

                // ── Terrain check ──
                if (ChunkManager.Instance == null) continue;

                TerrainType terrain = ChunkManager.Instance.GetTerrainAt(candidate);
                bool terrainValid = false;
                foreach (var allowed in mobData.allowedTerrain)
                {
                    if (terrain == allowed)
                    {
                        terrainValid = true;
                        break;
                    }
                }
                if (!terrainValid) continue;

                // ── Spacing check against existing mobs ──
                if (!CheckSpacing(candidate)) continue;

                position = new Vector3(candidate.x, candidate.y, 0f);
                return true;
            }

            return false;
        }

        private bool CheckSpacing(Vector2 candidate)
        {
            float minSqr = config.minSpawnSpacing * config.minSpawnSpacing;

            for (int i = _activeMobs.Count - 1; i >= 0; i--)
            {
                var mob = _activeMobs[i];
                if (mob == null) continue;

                float sqrDist = ((Vector2)mob.transform.position - candidate).sqrMagnitude;
                if (sqrDist < minSqr) return false;
            }

            return true;
        }

        private void SpawnMob(MobData mobData, Vector3 worldPosition)
        {
            if (mobData.prefab == null)
            {
                Debug.LogWarning($"[MobSpawnManager] No prefab on MobData '{mobData.id}'!");
                return;
            }

            // Instantiate under this manager's transform (not under a chunk)
            GameObject instance = Instantiate(mobData.prefab, worldPosition, Quaternion.identity, transform);
            instance.name = $"Mob_{mobData.id}_{_activeMobs.Count}";

            // Ensure MobController exists
            var controller = instance.GetComponent<MobController>();
            if (controller == null)
                controller = instance.AddComponent<MobController>();

            // Initialise with data and layer masks
            controller.Initialise(mobData, playerLayer, obstacleMask, mobMask);

            // Attach catching behaviour if the mob supports it
            CatchableMob.TryAddToInstance(instance, mobData);  

            // Track
            _activeMobs.Add(controller);
            IncrementTypeCount(mobData.id);

            // Listen for death to clean up tracking
            controller.OnDeath += HandleMobDeath;
        }

        // ───────────────────────── Culling ─────────────────────────

        private void RunCullSweep()
        {
            if (_playerTransform == null) return;

            float cullSqr = cullDistance * cullDistance;
            Vector2 playerPos = _playerTransform.position;

            for (int i = _activeMobs.Count - 1; i >= 0; i--)
            {
                var mob = _activeMobs[i];

                // Clean up null entries (destroyed externally)
                if (mob == null || mob.gameObject == null)
                {
                    _activeMobs.RemoveAt(i);
                    continue;
                }

                // Only cull mobs that are idle (roaming)
                if (mob.CurrentState is not RoamingState) continue;

                float sqrDist = ((Vector2)mob.transform.position - playerPos).sqrMagnitude;
                if (sqrDist > cullSqr)
                {
                    RemoveMob(mob, destroy: true);
                    i--; // List shifted
                }
            }
        }

        // ───────────────────────── Tracking ─────────────────────────

        private void HandleMobDeath(MobController mob)
        {
            mob.OnDeath -= HandleMobDeath;
            DecrementTypeCount(mob.Data.id);

            _activeMobs.Remove(mob);
            // GameObject is destroyed by MobController.Die() — no need to destroy here
        }

        private void RemoveMob(MobController mob, bool destroy)
        {
            mob.OnDeath -= HandleMobDeath;
            DecrementTypeCount(mob.Data.id);
            _activeMobs.Remove(mob);

            if (destroy && mob != null && mob.gameObject != null)
                Destroy(mob.gameObject);
        }

        private int GetTypeCount(string mobId)
        {
            return _typeCounts.TryGetValue(mobId, out int count) ? count : 0;
        }

        private void IncrementTypeCount(string mobId)
        {
            if (_typeCounts.ContainsKey(mobId))
                _typeCounts[mobId]++;
            else
                _typeCounts[mobId] = 1;
        }

        private void DecrementTypeCount(string mobId)
        {
            if (_typeCounts.ContainsKey(mobId))
            {
                _typeCounts[mobId]--;
                if (_typeCounts[mobId] <= 0)
                    _typeCounts.Remove(mobId);
            }
        }

        // ───────────────────────── Public API ─────────────────────────

        /// <summary>
        /// Force-spawn a specific mob at a position. Useful for scripted events,
        /// boss encounters, or debug testing. Bypasses caps and rules.
        /// </summary>
        public MobController ForceSpawn(MobData mobData, Vector3 position)
        {
            if (mobData == null || mobData.prefab == null) return null;

            GameObject instance = Instantiate(mobData.prefab, position, Quaternion.identity, transform);
            instance.name = $"Mob_{mobData.id}_forced";

            var controller = instance.GetComponent<MobController>();
            if (controller == null)
                controller = instance.AddComponent<MobController>();

            controller.Initialise(mobData, playerLayer, obstacleMask, mobMask);
            _activeMobs.Add(controller);
            IncrementTypeCount(mobData.id);
            controller.OnDeath += HandleMobDeath;

        // Attach catching behaviour if the mob supports it
            CatchableMob.TryAddToInstance(instance, mobData);

            return controller;
        }

        /// <summary>
        /// Called by CatchableMob when a mob is caught so tracking stays accurate.
        /// Removes the mob from active lists without triggering death logic.
        /// </summary>
        public void UntrackMob(MobController mob)
        {
            if (mob == null) return;
            mob.OnDeath -= HandleMobDeath;
            DecrementTypeCount(mob.Data.id);
            _activeMobs.Remove(mob);
        }

        /// <summary>
        /// Despawn all mobs immediately. Useful for scene transitions or debug.
        /// </summary>
        public void DespawnAll()
        {
            for (int i = _activeMobs.Count - 1; i >= 0; i--)
            {
                var mob = _activeMobs[i];
                if (mob != null && mob.gameObject != null)
                {
                    mob.OnDeath -= HandleMobDeath;
                    Destroy(mob.gameObject);
                }
            }

            _activeMobs.Clear();
            _typeCounts.Clear();
        }

        // ───────────────────────── Debug GUI ─────────────────────────

        private void OnGUI()
        {
            if (!showDebugGUI) return;

            float x = 10f;
            float y = 500f;

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 300, 20), "=== MOB SPAWN MANAGER ===");
            y += 22f;

            GUI.Label(new Rect(x, y, 300, 20),
                $"Active: {_activeMobs.Count} / {config.globalMobCap}");
            y += 18f;

            float currentHour = timeReference != null ? timeReference.time : -1f;
            GUI.Label(new Rect(x, y, 300, 20),
                $"Time: {(currentHour >= 0 ? $"{currentHour:F1}h" : "no ref")}");
            y += 18f;

            GUI.Label(new Rect(x, y, 300, 20),
                $"Spawn in: {_spawnTimer:F1}s | Cull in: {_cullTimer:F1}s");
            y += 22f;

            // Per-type breakdown
            foreach (var kvp in _typeCounts)
            {
                // Find the rule cap for this type
                int cap = 0;
                foreach (var rule in config.rules)
                {
                    if (rule.mobData != null && rule.mobData.id == kvp.Key)
                    {
                        cap = rule.typeCap;
                        break;
                    }
                }

                GUI.Label(new Rect(x, y, 300, 20),
                    $"  {kvp.Key}: {kvp.Value} / {cap}");
                y += 18f;
            }

            // List active mobs with state
            y += 6f;
            GUI.Label(new Rect(x, y, 300, 20), "Mobs:");
            y += 18f;

            int shown = 0;
            foreach (var mob in _activeMobs)
            {
                if (mob == null || shown >= 10) continue;
                string state = mob.CurrentState != null
                    ? mob.CurrentState.GetType().Name.Replace("State", "")
                    : "?";
                float dist = _playerTransform != null
                    ? Vector2.Distance(mob.transform.position, _playerTransform.position)
                    : 0f;

                GUI.color = state switch
                {
                    "Attacking" => Color.red,
                    "Chasing" => new Color(1f, 0.5f, 0f),
                    "Searching" => Color.yellow,
                    "Fleeing" => Color.cyan,
                    _ => Color.white
                };

                GUI.Label(new Rect(x, y, 400, 20),
                    $"  {mob.Data.displayName} | {state} | HP:{mob.CurrentHealth}/{mob.Data.maxHealth} | {dist:F0}m");
                y += 16f;
                shown++;
            }

            if (_activeMobs.Count > 10)
            {
                GUI.color = Color.gray;
                GUI.Label(new Rect(x, y, 300, 20),
                    $"  ...and {_activeMobs.Count - 10} more");
            }

            GUI.color = Color.white;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_playerTransform == null) return;

            Vector3 playerPos = _playerTransform.position;

            // Min spawn distance (red ring)
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            DrawCircleGizmo(playerPos, config != null ? config.minPlayerDistance : 8f, 32);

            // Max spawn distance (green ring)
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            DrawCircleGizmo(playerPos, config != null ? config.maxPlayerDistance : 30f, 48);

            // Cull distance (yellow ring)
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            DrawCircleGizmo(playerPos, cullDistance, 48);
        }

        private void DrawCircleGizmo(Vector3 center, float radius, int segments)
        {
            float step = 360f / segments;
            Vector3 prev = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                );
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}