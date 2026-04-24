using UnityEngine;
using ProceduralMusic.Bridge;

namespace MobSystem
{
    /// <summary>
    /// Handles the player's response to mob combat through the HappinessSystem.
    /// Two effects:
    ///
    ///   1. PASSIVE DRAIN — while GameStateManager is in Combat state, happiness
    ///      decreases slowly every frame via HappinessSystem.AdjustHappiness().
    ///
    ///   2. HIT DAMAGE — when a mob lands an attack, happiness drops instantly
    ///      by a scaled amount. Bigger hits = bigger drops.
    ///
    /// Both effects go through HappinessSystem.AdjustHappiness(), which is one
    /// of only two things allowed to modify happiness directly (the other being
    /// mood-driven drift). This feeds through to the music system via the
    /// MoodSystem → ComfortMusicBridge chain.
    ///
    /// SETUP:
    ///   1. Attach to the Player GameObject (same object as HappinessSystem)
    ///   2. HappinessSystem auto-finds if left empty
    ///   3. Tune drain rate and damage scaling in the inspector
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [Header("Passive Combat Drain")]
        [Tooltip("Happiness drained per second while in combat state (0–1 scale). " +
                 "0.03 = 3% per second, so 10 seconds of combat = 30% happiness lost.")]
        [Min(0f)]
        [SerializeField] private float combatDrainPerSecond = 0.03f;

        [Header("Hit Damage")]
        [Tooltip("Happiness lost per point of mob damage (0–1 scale). " +
                 "A wolf dealing 4 damage at 0.025 per point = 0.10 = 10% happiness lost.")]
        [Min(0f)]
        [SerializeField] private float happinessLossPerDamage = 0.025f;

        [Tooltip("Minimum happiness lost per hit regardless of damage (0–1 scale). " +
                 "Ensures even weak mobs feel threatening.")]
        [Min(0f)]
        [SerializeField] private float minimumHitPenalty = 0.03f;

        [Header("Hurt Reaction")]
        [Tooltip("How long (sec) the player is locked in the hurt state after " +
                 "taking a hit. Movement is frozen and the hurt animation plays. " +
                 "Set to 0 to disable the hurt state entirely.")]
        [Min(0f)]
        [SerializeField] private float hurtStunDuration = 0.3f;

        [Tooltip("How long (sec) the player is invincible after taking a hit. " +
                 "Usually longer than hurtStunDuration so there's a dodge window " +
                 "after the stun ends but before the player can be hit again. " +
                 "Set to 0 to disable i-frames.")]
        [Min(0f)]
        [SerializeField] private float iFrameDuration = 0.6f;

        [Header("Audio")]
        [Tooltip("Sound played when the player takes a hit from a mob.")]
        [SerializeField] private AudioClip hitSound;
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGUI = false;

        // Runtime
        private HappinessSystem happinessSystem;
        private PlayerStateManager playerStateManager;
        private float _iFrameTimer;
        private bool _inCombat;

        /// <summary>True while the player is in invincibility frames from a recent hit.</summary>
        public bool IsInvincible => _iFrameTimer > 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            happinessSystem = HappinessSystem.Instance;
            if (happinessSystem == null)
                happinessSystem = FindObjectOfType<HappinessSystem>();

            if (happinessSystem == null)
                Debug.LogWarning("[PlayerHealth] No HappinessSystem found! " +
                                 "Mob damage won't affect happiness.");
            else
                happinessSystem.OnDamaged += HandleDamaged;

            // Cache PlayerStateManager so hits can trigger the hurt state
            playerStateManager = GetComponent<PlayerStateManager>();
            if (playerStateManager == null)
                Debug.LogWarning("[PlayerHealth] No PlayerStateManager on this GameObject — " +
                                 "hurt state won't trigger on hit.");
        }

        private void OnDestroy()
        {
            if (happinessSystem != null)
                happinessSystem.OnDamaged -= HandleDamaged;
        }

        /// <summary>
        /// Fires when HappinessSystem takes actual damage (after i-frame
        /// filtering). This is where all the player-side hit reactions live —
        /// hurt state, i-frame start, hit sound, visual feedback.
        /// </summary>
        private void HandleDamaged(float amountLost)
        {
            // Start i-frames so follow-up hits during the next few frames are ignored
            if (iFrameDuration > 0f)
                _iFrameTimer = iFrameDuration;

            // Trigger the hurt state so the player freezes and plays the hurt animation
            if (playerStateManager != null && hurtStunDuration > 0f)
                playerStateManager.StartHurt(hurtStunDuration);

            // Play hit sound
            if (hitSound != null)
                AudioSource.PlayClipAtPoint(hitSound, transform.position, hitVolume);

            // Visual feedback — flash on player sprite
            if (PlayerHitFeedback.Instance != null)
                PlayerHitFeedback.Instance.TriggerHit();

            Debug.Log($"[PlayerHealth] Took {amountLost:F3} happiness damage " +
                      $"(now {(happinessSystem != null ? happinessSystem.Happiness : 0f):F2})");
        }

        private void Update()
        {
            // Tick i-frame timer
            if (_iFrameTimer > 0f)
                _iFrameTimer -= Time.deltaTime;

            // Check combat state from GameStateManager
            _inCombat = GameStateManager.Instance != null
                     && GameStateManager.Instance.CurrentState == GameMusicState.Combat;

            // Passive drain while in combat
            if (_inCombat && happinessSystem != null && combatDrainPerSecond > 0f)
            {
                happinessSystem.AdjustHappiness(-combatDrainPerSecond * Time.deltaTime);
            }
        }

        // ───────────────────────── Public API ─────────────────────────

        /// <summary>
        /// Called by AttackingState when a mob lands a hit.
        /// If the mob's MobData.happinessPenalty is set (> 0), uses that directly.
        /// Otherwise falls back to the generic formula: damage × happinessLossPerDamage.
        /// </summary>
        /// <param name="damage">The mob's attackDamage value.</param>
        /// <param name="happinessPenaltyOverride">MobData.happinessPenalty — 0 means use default formula.</param>
        /// <param name="attackerPosition">World position of the mob.</param>
        /// <summary>
        /// Legacy entry point. Mobs now call HappinessSystem.AdjustHappiness
        /// directly, and hit reactions run via the OnDamaged event — see
        /// HandleDamaged. This method is kept so any other caller (debug
        /// tools, scripted events) that still routes through here continues
        /// to work; it just funnels into the same event path.
        /// </summary>
        public void TakeDamage(int damage, float happinessPenaltyOverride, Vector3 attackerPosition)
        {
            if (happinessSystem == null) return;

            float loss = happinessPenaltyOverride > 0f
                ? happinessPenaltyOverride
                : Mathf.Max(damage * happinessLossPerDamage, minimumHitPenalty);

            // AdjustHappiness handles i-frame gating and fires OnDamaged,
            // which routes through HandleDamaged — so hit reactions
            // (hurt state, i-frames, sound, feedback) all happen there.
            happinessSystem.AdjustHappiness(-loss);
        }

        // ───────────────────────── Debug GUI ─────────────────────────

        private void OnGUI()
        {
            if (!showDebugGUI || happinessSystem == null) return;

            float x = 10f;
            float y = 700f;

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 300, 20), "=== PLAYER HEALTH ===");
            y += 20f;

            GUI.color = _inCombat ? Color.red : Color.green;
            GUI.Label(new Rect(x, y, 300, 20),
                $"Combat: {(_inCombat ? "YES" : "no")}");
            y += 18f;

            if (_inCombat)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(x, y, 300, 20),
                    $"Draining: -{combatDrainPerSecond:F3}/sec");
                y += 18f;
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 300, 20),
                $"Happiness: {happinessSystem.Happiness:F2} / 1.00");

            GUI.color = Color.white;
        }
    }
}