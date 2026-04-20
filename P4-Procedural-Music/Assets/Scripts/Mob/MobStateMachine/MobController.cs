using System;
using UnityEngine;
using MobSystem.Data;
using MobSystem.States;
using InventorySystem.Data;

namespace MobSystem
{
    /// <summary>
    /// Core runtime component for a mob. Drives the state machine and manages
    /// health, death, loot, and audio. Delegates detection to MobAwareness,
    /// movement to MobSteering, and group behaviour to MobPackCoordinator.
    ///
    /// Equivalent to PlayerStateManager — states call mob.SwitchState() to
    /// transition, and use mob.Steering / mob.Awareness / mob.PackCoordinator
    /// for movement, detection, and group coordination.
    ///
    /// COMPONENT ARCHITECTURE:
    ///   MobController        — state machine, health, death, loot, audio
    ///   MobSteering          — blended movement (desire + separation + avoidance)
    ///   MobAwareness         — gradient detection (awareness 0→1 with thresholds)
    ///   MobPackCoordinator   — alerts nearby same-type mobs on detection
    ///   MobAnimator          — sprite flipping / Animator.Play (unchanged)
    ///   MobHealthBar         — health bar above mob (unchanged)
    ///   MobHitFeedback       — flash + shake on damage (unchanged)
    ///
    /// SETUP:
    ///   Attach to a prefab with SpriteRenderer, Rigidbody2D (Kinematic or Dynamic),
    ///   and a Collider2D. MobSpawnManager sets the Data reference at spawn time.
    ///   Steering, Awareness, and PackCoordinator are added automatically.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class MobController : MonoBehaviour
    {
        // ───────────────────────── Inspector ─────────────────────────

        [Header("Data (set by MobSpawnManager or inspector)")]
        [Tooltip("The mob type definition. MobSpawnManager assigns this at spawn time.")]
        [SerializeField] private MobData data;

        [Header("Detection")]
        [Tooltip("Layer(s) the player is on. Used for detection OverlapCircle.")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Steering Layers")]
        [Tooltip("Layer(s) for obstacles (trees, rocks, water). Used by avoidance raycasts.")]
        [SerializeField] private LayerMask obstacleMask;

        [Tooltip("Layer(s) other mobs are on. Used by separation force.")]
        [SerializeField] private LayerMask mobMask;

        // ───────────────────────── Public Read ─────────────────────────

        /// <summary>The MobData ScriptableObject driving this mob's stats and behaviour.</summary>
        public MobData Data => data;

        /// <summary>Layer mask for the player — used by AttackingState for hitbox checks.</summary>
        public LayerMask PlayerLayerMask => playerLayer;

        /// <summary>Current health points.</summary>
        public int CurrentHealth { get; private set; }

        /// <summary>The world position where this mob was spawned. Roaming uses this as an anchor.</summary>
        public Vector3 SpawnPosition { get; private set; }

        /// <summary>For Neutral mobs: flips to true on first TakeDamage, unlocking hostile states.</summary>
        public bool IsProvoked { get; private set; }

        /// <summary>
        /// Current facing direction — set by states or derived from steering.
        /// MobAnimator reads this for sprite flipping and direction suffixes.
        /// </summary>
        public Vector2 FacingDirection
        {
            get => _facingDirection;
            set => _facingDirection = value;
        }

        /// <summary>Animation state name — set by states, read by MobAnimator.</summary>
        public string AnimationQueue { get; set; }

        /// <summary>Physics body — exposed for states that need direct access.</summary>
        public Rigidbody2D Rb { get; private set; }

        /// <summary>Currently active state (for debug display).</summary>
        public MobBaseState CurrentState => _currentState;

        /// <summary>
        /// Last position where the player was known to be.
        /// Updated by ChasingState and MobAwareness.
        /// </summary>
        public Vector3 LastKnownPlayerPos { get; private set; }

        // ───────────────────────── Component References ─────────────────────────

        /// <summary>Steering system — states call Seek/Flee/Wander/Stop on this.</summary>
        public MobSteering Steering { get; private set; }

        /// <summary>Awareness system — states read IsSuspicious/IsFullyAlert from this.</summary>
        public MobAwareness Awareness { get; private set; }

        /// <summary>Pack coordinator — states call RaiseAlarm on this.</summary>
        public MobPackCoordinator PackCoordinator { get; private set; }

        // ───────────────────────── Events ─────────────────────────

        /// <summary>Fired when this mob takes damage. Args: (currentHealth, maxHealth, attackerPosition).</summary>
        public event Action<int, int, Vector3> OnDamaged;

        /// <summary>Fired when this mob dies. Arg: this MobController.</summary>
        public event Action<MobController> OnDeath;

        // ───────────────────────── State Instances ─────────────────────────

        // Pre-allocated — no GC from state transitions.
        [NonSerialized] public readonly RoamingState roamingState = new RoamingState();
        [NonSerialized] public readonly AlertState alertState = new AlertState();
        [NonSerialized] public readonly ChasingState chasingState = new ChasingState();
        [NonSerialized] public readonly AttackingState attackingState = new AttackingState();
        [NonSerialized] public readonly FleeingState fleeingState = new FleeingState();
        [NonSerialized] public readonly SearchingState searchingState = new SearchingState();

        // ───────────────────────── Private ─────────────────────────

        private MobBaseState _currentState;
        private Vector2 _facingDirection = Vector2.down;

        // ───────────────────────── Lifecycle ─────────────────────────

        private void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();

            // Ensure sub-components exist
            Steering = GetComponent<MobSteering>();
            if (Steering == null) Steering = gameObject.AddComponent<MobSteering>();

            Awareness = GetComponent<MobAwareness>();
            if (Awareness == null) Awareness = gameObject.AddComponent<MobAwareness>();

            PackCoordinator = GetComponent<MobPackCoordinator>();
            if (PackCoordinator == null) PackCoordinator = gameObject.AddComponent<MobPackCoordinator>();
        }

        private void Start()
        {
            if (data == null)
            {
                Debug.LogError($"[MobController] No MobData assigned on {gameObject.name}!");
                enabled = false;
                return;
            }

            // Initialise runtime state
            CurrentHealth = data.maxHealth;
            SpawnPosition = transform.position;
            IsProvoked = false;
            FacingDirection = Vector2.down;
            AnimationQueue = "Idle";

            // Configure sub-components from MobData
            ConfigureSteering();
            ConfigureAwareness();
            ConfigurePackCoordinator();

            // Start in roaming
            _currentState = roamingState;
            _currentState.EnterState(this);
        }

        private void Update()
        {
            if (data == null) return;

            // Update facing from steering when moving
            if (Steering.IsMoving)
                _facingDirection = Steering.FacingDirection;

            // Tick current state
            _currentState?.UpdateState(this);
        }

        // ───────────────────────── Configuration ─────────────────────────

        private void ConfigureSteering()
        {
            Steering.Configure(
                maxSpeed: data.moveSpeed,
                separationRadius: data.separationRadius,
                separationWeight: data.separationWeight,
                avoidanceDistance: data.avoidanceDistance,
                avoidanceWeight: data.avoidanceWeight,
                wanderStrength: data.wanderStrength,
                obstacleMask: obstacleMask,
                mobMask: mobMask
            );
        }

        private void ConfigureAwareness()
        {
            Awareness.Configure(
                detectionRange: data.detectionRange,
                hearingRange: data.hearingRange,
                visualGainRate: data.visualGainRate,
                auditoryGainRate: data.auditoryGainRate,
                decayRate: data.awarenessDecayRate,
                suspiciousThreshold: data.suspiciousThreshold,
                alertThreshold: 1f,
                detectionInterval: data.detectionInterval,
                playerLayer: playerLayer
            );
        }

        private void ConfigurePackCoordinator()
        {
            PackCoordinator.Configure(
                mobTypeId: data.id,
                alertRadius: data.packAlertRadius,
                alertAwarenessBoost: data.packAlertBoost,
                mobMask: mobMask
            );
        }

        // ───────────────────────── State Machine ─────────────────────────

        /// <summary>
        /// Transition to a new state. Calls ExitState on the old, EnterState on the new.
        /// </summary>
        public void SwitchState(MobBaseState newState)
        {
            _currentState?.ExitState(this);
            _currentState = newState;
            _currentState.EnterState(this);
        }

        // ───────────────────────── Combat ─────────────────────────

        /// <summary>
        /// Apply damage to this mob. Called by ToolUseSystem or other damage sources.
        /// Returns true if the mob died from this hit.
        /// </summary>
        public bool TakeDamage(int damage, Vector3 attackerPosition)
        {
            if (CurrentHealth <= 0) return false;

            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(0, CurrentHealth);

            // Getting hit = instant full awareness of the attacker
            Awareness.ForceFullAlert(attackerPosition);

            // Neutral mobs become hostile on first hit
            if (data.behaviour == MobBehaviour.Neutral && !IsProvoked)
            {
                IsProvoked = true;

                // Alert nearby neutral mobs of the same type
                PackCoordinator.ProvokeNearby(attackerPosition);
            }

            // Play hit sound
            if (data.hitSound != null)
                PlaySound(data.hitSound, data.hitVolume);

            OnDamaged?.Invoke(CurrentHealth, data.maxHealth, attackerPosition);

            if (CurrentHealth <= 0)
            {
                Die(attackerPosition);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Provoked by a pack member (Neutral mob group aggro).
        /// Sets IsProvoked and forces awareness to full.
        /// </summary>
        public void ProvokeFromPack(Vector3 attackerPosition)
        {
            if (data.behaviour != MobBehaviour.Neutral) return;

            IsProvoked = true;
            Awareness.ForceFullAlert(attackerPosition);
        }

        /// <summary>
        /// Whether this mob should flee based on its current health.
        /// </summary>
        public bool ShouldFlee()
        {
            if (data.fleeHealthThreshold <= 0f) return false;

            float healthPct = (float)CurrentHealth / data.maxHealth;
            return healthPct <= data.fleeHealthThreshold;
        }

        /// <summary>
        /// Whether this mob currently uses hostile states.
        /// </summary>
        public bool CanUseHostileStates()
        {
            if (data.behaviour == MobBehaviour.Hostile) return true;
            if (data.behaviour == MobBehaviour.Neutral && IsProvoked) return true;
            return false;
        }

        /// <summary>
        /// Update the last known player position. Called by ChasingState
        /// while actively pursuing.
        /// </summary>
        public void UpdateLastKnownPlayerPos(Vector3 position)
        {
            LastKnownPlayerPos = position;
        }

        // ───────────────────────── Death & Loot ─────────────────────────

        private void Die(Vector3 attackerPosition)
        {
            // Play death sound
            if (data.deathSound != null)
                PlaySound(data.deathSound, data.deathVolume);

            // Drop loot
            if (data.lootTable != null && data.worldItemPrefab != null)
            {
                Vector2 scatterDir = ((Vector2)(transform.position - attackerPosition)).normalized;
                SpawnLoot(scatterDir);
            }

            OnDeath?.Invoke(this);

            Destroy(gameObject);
        }

        private void SpawnLoot(Vector2 scatterDirection)
        {
            var drops = data.lootTable.Roll();

            foreach (var drop in drops)
            {
                for (int i = 0; i < drop.quantity; i++)
                {
                    var instance = new ItemInstance(drop.item);
                    var go = Instantiate(data.worldItemPrefab, transform.position, Quaternion.identity);
                    var worldItem = go.GetComponent<InventorySystem.UI.WorldItem>();

                    Vector2 dir = scatterDirection + UnityEngine.Random.insideUnitCircle * 0.8f;
                    worldItem?.Initialise(instance, 1, dir);
                }
            }
        }

        // ───────────────────────── Audio ─────────────────────────

        /// <summary>
        /// Play a one-shot sound that survives this GameObject being destroyed.
        /// </summary>
        public void PlaySound(AudioClip clip, float volume, float pitchVariation = 0.1f)
        {
            if (clip == null) return;

            var go = new GameObject("MobSound");
            go.transform.position = transform.position;

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            source.spatialBlend = 0f;
            source.Play();

            Destroy(go, clip.length / Mathf.Max(source.pitch, 0.1f));
        }

        // ───────────────────────── Initialisation (called by MobSpawnManager) ─────────────────────────

        /// <summary>
        /// Assign mob data at runtime. Called by MobSpawnManager after instantiation.
        /// Must be called before Start() runs (same frame as Instantiate is fine).
        /// </summary>
        public void Initialise(MobData mobData, LayerMask playerLayerMask)
        {
            data = mobData;
            playerLayer = playerLayerMask;
        }

        /// <summary>
        /// Extended initialise with obstacle and mob layers for steering.
        /// Called by MobSpawnManager if available, falls back to serialised values otherwise.
        /// </summary>
        public void Initialise(MobData mobData, LayerMask playerLayerMask,
                               LayerMask obstacleMaskOverride, LayerMask mobMaskOverride)
        {
            data = mobData;
            playerLayer = playerLayerMask;
            obstacleMask = obstacleMaskOverride;
            mobMask = mobMaskOverride;
        }

        // ───────────────────────── Compatibility ─────────────────────────

        // These properties maintain backward compatibility with code that
        // reads the old detection fields. They now delegate to MobAwareness.

        /// <summary>True when the player is within visual detection range.</summary>
        public bool PlayerDetected => Awareness != null && Awareness.PlayerInRange;

        /// <summary>Cached player Transform from awareness system.</summary>
        public Transform PlayerTransform => Awareness?.PlayerTransform;

        // ───────────────────────── Debug ─────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (data == null) return;

            // Detection range
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, data.detectionRange);

            // Hearing range
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, data.hearingRange);

            // Aggro range (hostile only)
            if (data.HasHostileStates)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, data.aggroRange);
            }

            // Attack range
            if (data.HasHostileStates)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
                Gizmos.DrawWireSphere(transform.position, data.attackRange);
            }

            // Attack hitbox (placed in front of mob along facing direction)
            if (data.HasHostileStates && Application.isPlaying)
            {
                Vector3 hitboxCenter = transform.position +
                                       (Vector3)FacingDirection * data.attackHitboxOffset;
                Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
                Gizmos.DrawWireSphere(hitboxCenter, data.attackHitboxRadius);

                // Line from mob to hitbox center so it's clear where it's aimed
                Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
                Gizmos.DrawLine(transform.position, hitboxCenter);
            }

            // Roam radius from spawn
            if (Application.isPlaying)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
                Gizmos.DrawWireSphere(SpawnPosition, data.roamRadius);
            }

            // State label with awareness
            string stateLabel = _currentState != null ? _currentState.GetType().Name : "None";
            float awareness = Awareness != null ? Awareness.Awareness : 0f;
            string label = $"{(data != null ? data.displayName : "?")} | {stateLabel} | " +
                           $"HP: {CurrentHealth}/{(data != null ? data.maxHealth : 0)} | " +
                           $"Awareness: {awareness:F2}";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, label);
        }
#endif
    }
}