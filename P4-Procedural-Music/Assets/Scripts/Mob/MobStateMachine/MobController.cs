using System;
using UnityEngine;
using MobSystem.Data;
using MobSystem.States;
using InventorySystem.Data;

namespace MobSystem
{
    /// <summary>
    /// Core runtime component for a mob. Drives the state machine, handles
    /// player detection on a staggered timer, tracks health, and manages death/loot.
    ///
    /// Equivalent to PlayerStateManager — states call mob.SwitchState() to transition,
    /// and read mob.Data / mob.PlayerDetected / mob.Rb etc. for decisions.
    ///
    /// SETUP:
    ///   Attach to a prefab with SpriteRenderer, Rigidbody2D (Kinematic or Dynamic),
    ///   and a Collider2D. MobSpawnManager sets the Data reference at spawn time.
    ///   Or assign Data directly in the inspector for testing.
    ///
    /// DETECTION:
    ///   Runs Physics2D.OverlapCircle on a staggered timer (not every frame).
    ///   Each mob instance gets a random offset so detection checks are distributed
    ///   across frames — 30 mobs at 0.25s interval = ~2 checks/frame, not 30.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class MobController : MonoBehaviour
    {
        // ───────────────────────── Inspector (for testing) ─────────────────────────

        [Header("Data (set by MobSpawnManager or inspector)")]
        [Tooltip("The mob type definition. MobSpawnManager assigns this at spawn time.")]
        [SerializeField] private MobData data;

        [Header("Detection")]
        [Tooltip("Layer(s) the player is on. Used for OverlapCircle detection.")]
        [SerializeField] private LayerMask playerLayer;

        // ───────────────────────── Public Read ─────────────────────────

        /// <summary>The MobData ScriptableObject driving this mob's stats and behaviour.</summary>
        public MobData Data => data;

        /// <summary>Current health points.</summary>
        public int CurrentHealth { get; private set; }

        /// <summary>True when the detection check found the player within range.</summary>
        public bool PlayerDetected { get; private set; }

        /// <summary>
        /// Cached reference to the player Transform. Null until first detection.
        /// Remains set even after the player leaves range (used by SearchingState
        /// to move to last known position).
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        /// <summary>
        /// Last position where the player was detected. Updated every successful
        /// detection tick. SearchingState uses this when the player escapes.
        /// </summary>
        public Vector3 LastKnownPlayerPos { get; private set; }

        /// <summary>The world position where this mob was spawned. Roaming uses this as an anchor.</summary>
        public Vector3 SpawnPosition { get; private set; }

        /// <summary>For Neutral mobs: flips to true on first TakeDamage, unlocking hostile states.</summary>
        public bool IsProvoked { get; private set; }

        /// <summary>Current facing direction — states set this for animation.</summary>
        public Vector2 FacingDirection { get; set; }

        /// <summary>Animation state name — set by states, read by MobAnimator (like PlayerStateManager.animationQue).</summary>
        public string AnimationQueue { get; set; }

        /// <summary>Physics body — states use this for movement.</summary>
        public Rigidbody2D Rb { get; private set; }

        /// <summary>Currently active state (for debug display).</summary>
        public MobBaseState CurrentState => _currentState;

        // ───────────────────────── Events ─────────────────────────

        /// <summary>Fired when this mob takes damage. Args: (currentHealth, maxHealth, attackerPosition).</summary>
        public event Action<int, int, Vector3> OnDamaged;

        /// <summary>Fired when this mob dies. Arg: this MobController.</summary>
        public event Action<MobController> OnDeath;

        // ───────────────────────── State Instances ─────────────────────────

        // Pre-allocated — no GC from state transitions.
        [NonSerialized] public readonly RoamingState    roamingState    = new RoamingState();
        [NonSerialized] public readonly ChasingState    chasingState    = new ChasingState();
        [NonSerialized] public readonly AttackingState  attackingState  = new AttackingState();
        [NonSerialized] public readonly FleeingState    fleeingState    = new FleeingState();
        [NonSerialized] public readonly SearchingState  searchingState  = new SearchingState();

        // ───────────────────────── Private ─────────────────────────

        private MobBaseState _currentState;
        private float _detectionTimer;
        private float _detectionOffset; // Random stagger so mobs don't all check the same frame

        // ───────────────────────── Lifecycle ─────────────────────────

        private void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
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

            // Stagger detection so mobs don't all fire on the same frame
            _detectionOffset = UnityEngine.Random.Range(0f, data.detectionInterval);
            _detectionTimer = _detectionOffset;

            // Start in roaming
            _currentState = roamingState;
            _currentState.EnterState(this);
        }

        private void Update()
        {
            if (data == null) return;

            // Staggered player detection
            _detectionTimer -= Time.deltaTime;
            if (_detectionTimer <= 0f)
            {
                RunDetection();
                _detectionTimer = data.detectionInterval;
            }

            // Tick current state
            _currentState?.UpdateState(this);
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

        // ───────────────────────── Detection ─────────────────────────

        private void RunDetection()
        {
            float range = data.detectionRange;

            // Hostile mobs use aggroRange for detection when not yet chasing
            if (data.behaviour == MobBehaviour.Hostile)
                range = Mathf.Max(range, data.aggroRange);

            var hit = Physics2D.OverlapCircle(transform.position, range, playerLayer);

            if (hit != null)
            {
                PlayerDetected = true;
                PlayerTransform = hit.transform;
                LastKnownPlayerPos = hit.transform.position;
            }
            else
            {
                PlayerDetected = false;
                // Keep PlayerTransform and LastKnownPlayerPos — SearchingState needs them
            }
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

            // Neutral mobs become hostile on first hit
            if (data.behaviour == MobBehaviour.Neutral && !IsProvoked)
            {
                IsProvoked = true;
                // Force detection toward the attacker
                PlayerDetected = true;
                LastKnownPlayerPos = attackerPosition;
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
        /// Whether this mob should flee based on its current health.
        /// States check this to decide whether to transition to FleeingState.
        /// </summary>
        public bool ShouldFlee()
        {
            if (data.fleeHealthThreshold <= 0f) return false;

            float healthPct = (float)CurrentHealth / data.maxHealth;
            return healthPct <= data.fleeHealthThreshold;
        }

        /// <summary>
        /// Whether this mob currently uses hostile states.
        /// Always true for Hostile, true for Neutral after provocation, false for Passive.
        /// </summary>
        public bool CanUseHostileStates()
        {
            if (data.behaviour == MobBehaviour.Hostile) return true;
            if (data.behaviour == MobBehaviour.Neutral && IsProvoked) return true;
            return false;
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

                    // Scatter away from the attacker with some randomness
                    Vector2 dir = scatterDirection + UnityEngine.Random.insideUnitCircle * 0.8f;
                    worldItem?.Initialise(instance, 1, dir);
                }
            }
        }

        // ───────────────────────── Audio ─────────────────────────

        /// <summary>
        /// Play a one-shot sound that survives this GameObject being destroyed.
        /// Same pattern as HarvestableResource.PlaySoundWithPitch.
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

        // ───────────────────────── Debug ─────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (data == null) return;

            // Detection range
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, data.detectionRange);

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

            // Roam radius from spawn
            if (Application.isPlaying)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
                Gizmos.DrawWireSphere(SpawnPosition, data.roamRadius);
            }

            // State label
            string stateLabel = _currentState != null ? _currentState.GetType().Name : "None";
            string label = $"{(data != null ? data.displayName : "?")} | {stateLabel} | HP: {CurrentHealth}/{(data != null ? data.maxHealth : 0)}";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, label);
        }
#endif
    }
}
