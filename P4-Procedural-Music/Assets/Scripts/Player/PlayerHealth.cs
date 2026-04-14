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

        [Header("Audio")]
        [Tooltip("Sound played when the player takes a hit from a mob.")]
        [SerializeField] private AudioClip hitSound;
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGUI = false;

        // Runtime
        private HappinessSystem happinessSystem;
        private bool _inCombat;

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
        }

        private void Update()
        {
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
        public void TakeDamage(int damage, float happinessPenaltyOverride, Vector3 attackerPosition)
        {
            if (happinessSystem != null)
            {
                float loss;

                if (happinessPenaltyOverride > 0f)
                {
                    loss = happinessPenaltyOverride;
                }
                else
                {
                    loss = Mathf.Max(damage * happinessLossPerDamage, minimumHitPenalty);
                }

                happinessSystem.AdjustHappiness(-loss);

                Debug.Log($"[PlayerHealth] Hit for {damage} dmg → " +
                          $"-{loss:F3} happiness (now {happinessSystem.Happiness:F2})");
            }

            // Play hit sound
            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, transform.position, hitVolume);
            }

            // Visual feedback — red flash on player sprite
            if (PlayerHitFeedback.Instance != null)
                PlayerHitFeedback.Instance.TriggerHit();
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