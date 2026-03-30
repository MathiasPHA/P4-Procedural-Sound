using UnityEngine;

namespace MobSystem
{
    /// <summary>
    /// Handles the player's response to mob combat through the happiness system.
    /// Two effects:
    ///
    ///   1. PASSIVE DRAIN — while GameStateManager is in Combat state, happiness
    ///      decreases slowly every frame. This creates ambient stress from being
    ///      in danger, even between individual mob attacks.
    ///
    ///   2. HIT DAMAGE — when a mob lands an attack, happiness drops instantly
    ///      by a scaled amount. This is the direct punishment for getting hit.
    ///
    /// Both effects feed through HappinessBar, which smoothly lerps the displayed
    /// value — so a big hit causes a visible dip, and the passive drain creates
    /// a slow downward pressure that the player can feel building.
    ///
    /// SETUP:
    ///   1. Attach to the Player GameObject
    ///   2. Assign the HappinessBar reference (auto-finds if left empty)
    ///   3. Tune the drain rate and damage multiplier in the inspector
    ///
    /// Mobs call PlayerHealth.TakeDamage() from AttackingState. The GameStateManager
    /// combat state is triggered by ChasingState.EnterState() and released by
    /// FleeingState/SearchingState.ExitState() — no extra wiring needed.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [Header("References")]
        [Tooltip("The HappinessBar component. Auto-finds if left empty.")]
        [SerializeField] private HappinessBar happinessBar;

        [Header("Passive Combat Drain")]
        [Tooltip("Happiness drained per second while in combat state. " +
                 "Keeps pressure on the player even between individual hits.")]
        [Min(0f)]
        [SerializeField] private float combatDrainRate = 3f;

        [Header("Hit Damage")]
        [Tooltip("Multiplier converting mob attack damage to happiness reduction. " +
                 "A mob dealing 4 damage with a multiplier of 2.5 = -10 happiness.")]
        [Min(0f)]
        [SerializeField] private float damageToHappinessMultiplier = 2.5f;

        [Tooltip("Minimum happiness lost per hit, regardless of mob damage. " +
                 "Ensures even weak mobs feel threatening.")]
        [Min(0f)]
        [SerializeField] private float minimumHitPenalty = 3f;

        [Header("Audio")]
        [Tooltip("Sound played when the player takes a hit.")]
        [SerializeField] private AudioClip hitSound;
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGUI = false;

        // Runtime
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
            if (happinessBar == null)
                happinessBar = FindObjectOfType<HappinessBar>();

            if (happinessBar == null)
                Debug.LogWarning("[PlayerHealth] No HappinessBar found. " +
                                 "Happiness effects won't work.");
        }

        private void Update()
        {
            // Check combat state
            _inCombat = GameStateManager.Instance != null
                     && GameStateManager.Instance.CurrentState == GameMusicState.Combat;

            // Passive drain while in combat
            if (_inCombat && happinessBar != null && combatDrainRate > 0f)
            {
                happinessBar.ApplyComfortDelta(-combatDrainRate);
            }
        }

        // ───────────────────────── Public API ─────────────────────────

        /// <summary>
        /// Called by MobController's AttackingState when a mob lands a hit.
        /// Applies an instant happiness reduction scaled by the mob's damage.
        /// </summary>
        /// <param name="damage">The mob's attack damage value (from MobData.attackDamage).</param>
        /// <param name="attackerPosition">World position of the mob, for directional feedback.</param>
        public void TakeDamage(int damage, Vector3 attackerPosition)
        {
            if (happinessBar == null) return;

            // Calculate happiness loss: damage * multiplier, with a minimum floor
            float happinessLoss = Mathf.Max(damage * damageToHappinessMultiplier, minimumHitPenalty);
            happinessBar.ApplyInstantDelta(-happinessLoss);

            // Play hit sound
            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, transform.position, hitVolume);
            }

            Debug.Log($"[PlayerHealth] Hit for {damage} → -{happinessLoss:F0} happiness " +
                      $"(now {happinessBar.CurrentHappiness:F0}/{happinessBar.MaxHappiness})");
        }

        // ───────────────────────── Debug GUI ─────────────────────────

        private void OnGUI()
        {
            if (!showDebugGUI) return;

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
                    $"Draining: -{combatDrainRate:F1}/sec");
                y += 18f;
            }

            if (happinessBar != null)
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(x, y, 300, 20),
                    $"Happiness: {happinessBar.CurrentHappiness:F0} / {happinessBar.MaxHappiness}");
            }

            GUI.color = Color.white;
        }
    }
}
