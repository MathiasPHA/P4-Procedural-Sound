using UnityEngine;

/// <summary>
/// The player-facing happiness bar (0–1). This is what the player sees and
/// what kills them when it hits zero.
///
/// Happiness drifts passively based on the current MoodTier:
///   Elated   → gains fast
///   Content  → gains slowly
///   Neutral  → stable (no drift)
///   Uneasy   → drains slowly
///   Miserable→ drains fast
///
/// Only two things modify happiness directly:
///   1. Mood-driven drift (this script, every frame)
///   2. Mob attacks via AdjustHappiness() (instant penalty)
///   (Future: potions can also call AdjustHappiness with a positive value)
///
/// SETUP:
///   1. Attach to the Player GameObject (same object as MoodSystem).
///   2. MoodSystem must exist — this reads MoodSystem.CurrentTier.
///   3. HappinessMeter UI reads from this instead of ComfortSystem.
///
/// DATA FLOW:
///   [MoodSystem] ──→ CurrentTier ──→ drift rate ──→ happiness value ──→ UI bar
///   [Mob Attack] ──→ AdjustHappiness(-x) ─────────→ happiness value ──→ UI bar
///   happiness == 0 ──→ OnHappinessDepleted event ──→ (death, game over, etc.)
/// </summary>
public class HappinessSystem : MonoBehaviour
{
    public static HappinessSystem Instance { get; private set; }

    // ───────────────────────── Tuning ─────────────────────────

    [Header("Starting Value")]
    [Range(0f, 1f)]
    [SerializeField] private float startingHappiness = 0.7f;

    [Header("Drift Rates (units per second, per mood tier)")]
    [Tooltip("Happiness gain rate when mood is Elated")]
    [SerializeField] private float elatedRate = 0.025f;

    [Tooltip("Happiness gain rate when mood is Content")]
    [SerializeField] private float contentRate = 0.010f;

    [Tooltip("Happiness drift when mood is Neutral (should be 0 or very small)")]
    [SerializeField] private float neutralRate = 0.000f;

    [Tooltip("Happiness drain rate when mood is Uneasy (positive number, applied as negative)")]
    [SerializeField] private float uneasyRate = 0.010f;

    [Tooltip("Happiness drain rate when mood is Miserable (positive number, applied as negative)")]
    [SerializeField] private float miserableRate = 0.025f;

    [Header("Smoothing")]
    [Tooltip("Smooth happiness changes so the bar doesn't jitter")]
    [SerializeField] private float smoothTime = 0.4f;

    [Header("Death")]
    [Tooltip("Enable death when happiness reaches zero. " +
             "Turn off during development to test without dying.")]
    [SerializeField] private bool deathEnabled = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    // ───────────────────────── Events ─────────────────────────

    /// <summary>
    /// Fired once when happiness hits zero and deathEnabled is true.
    /// Subscribe from your game-over / respawn system.
    /// </summary>
    public event System.Action OnHappinessDepleted;

    /// <summary>
    /// Fired whenever a damaging AdjustHappiness call actually lands (i.e.
    /// wasn't filtered out by i-frames). The argument is the absolute amount
    /// of happiness lost (positive number). Subscribe from PlayerHealth or
    /// any other hit-reaction system.
    /// </summary>
    public event System.Action<float> OnDamaged;

    // ───────────────────────── State ─────────────────────────

    private float happiness;
    private float happinessTarget;
    private float happinessVelocity;   // for SmoothDamp
    private float currentDriftRate;    // for debug display
    private bool isDead;

    private MoodSystem moodSystem;

    // ───────────────────────── Cross-Scene Persistence ─────────────────────────

    // Static caches survive scene unload (statics live until app quit OR until
    // the editor reloads the assembly). On Awake the new instance reads these
    // back; on every Update / SetHappiness we write the latest values.
    //
    // -1f is the "nothing cached yet" sentinel (no real happiness can be
    // negative). Call ResetPersistedState() to wipe the cache when starting
    // a fresh game.
    private static float _persistedHappiness = -1f;
    private static bool _persistedIsDead = false;

    /// <summary>
    /// Wipe the cross-scene happiness cache. Call this before loading the
    /// gameplay scene from a "New Game" flow so the player starts with
    /// startingHappiness instead of whatever value carried over from a prior
    /// session in the same Unity process.
    /// </summary>
    public static void ResetPersistedState()
    {
        _persistedHappiness = -1f;
        _persistedIsDead = false;
    }

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Current happiness (0–1). This is the visible bar value.</summary>
    public float Happiness => happiness;

    /// <summary>True if happiness has been fully depleted and death is enabled.</summary>
    public bool IsDead => isDead;

    /// <summary>The drift rate currently being applied (units/sec). Positive = gaining.</summary>
    public float CurrentDriftRate => currentDriftRate;

    /// <summary>
    /// Directly adjust happiness. Use for:
    ///   - Mob attacks: AdjustHappiness(-0.1f)
    ///   - Potions (future): AdjustHappiness(+0.2f)
    /// Clamped to 0–1. Bypasses smoothing for instant feedback on hits.
    ///
    /// Damage (negative delta) is filtered through PlayerHealth.IsInvincible
    /// — hits during i-frames are silently ignored. When a damaging call does
    /// land, OnDamaged fires with the amount lost.
    /// </summary>
    public void AdjustHappiness(float delta)
    {
        // Damage gate: ignore negative deltas during i-frames
        if (delta < 0f
            && MobSystem.PlayerHealth.Instance != null
            && MobSystem.PlayerHealth.Instance.IsInvincible)
        {
            return;
        }

        happinessTarget = Mathf.Clamp01(happinessTarget + delta);

        // For negative hits, snap the displayed value partway so the player
        // feels the impact immediately rather than watching a slow drain.
        if (delta < 0f)
        {
            happiness = Mathf.Clamp01(happiness + delta * 0.7f);
            OnDamaged?.Invoke(-delta);
        }

        // Mirror to the cross-scene cache so a hit landed after Update has
        // already run this frame still survives a same-frame scene unload.
        _persistedHappiness = happiness;
    }

    /// <summary>
    /// Force happiness to an exact value. Use for game start, respawn, debug.
    /// Bypasses smoothing entirely.
    /// </summary>
    public void SetHappiness(float value)
    {
        happiness = Mathf.Clamp01(value);
        happinessTarget = happiness;
        happinessVelocity = 0f;
        isDead = false;

        _persistedHappiness = happiness;
        _persistedIsDead = false;
    }

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Restore from cross-scene cache if anything's there; otherwise start fresh.
        if (_persistedHappiness >= 0f)
        {
            happiness = _persistedHappiness;
            happinessTarget = _persistedHappiness;
            isDead = _persistedIsDead;
        }
        else
        {
            happiness = startingHappiness;
            happinessTarget = startingHappiness;
        }
    }

    private void Start()
    {
        moodSystem = MoodSystem.Instance;

        if (moodSystem == null)
            moodSystem = FindObjectOfType<MoodSystem>();

        if (moodSystem == null)
            Debug.LogWarning("[HappinessSystem] No MoodSystem found! " +
                             "Happiness won't drift.");
    }

    private void Update()
    {
        if (isDead)
        {
            // Even when dead we still keep the cache fresh — otherwise a
            // scene transition could lose the dead state.
            _persistedHappiness = happiness;
            _persistedIsDead = true;
            return;
        }

        ApplyMoodDrift();
        ApplySmoothing();
        CheckDeath();

        _persistedHappiness = happiness;
        _persistedIsDead = isDead;
    }

    // ───────────────────────── Core ─────────────────────────

    private void ApplyMoodDrift()
    {
        if (moodSystem == null) return;

        currentDriftRate = GetDriftRate(moodSystem.CurrentTier);
        happinessTarget = Mathf.Clamp01(happinessTarget + currentDriftRate * Time.deltaTime);
    }

    private void ApplySmoothing()
    {
        happiness = Mathf.SmoothDamp(happiness, happinessTarget, ref happinessVelocity, smoothTime);
        happiness = Mathf.Clamp01(happiness);
    }

    private void CheckDeath()
    {
        if (!deathEnabled) return;
        if (happiness > 0.001f) return;

        isDead = true;
        happiness = 0f;
        happinessTarget = 0f;

        Debug.Log("[HappinessSystem] Happiness depleted — player died.");
        OnHappinessDepleted?.Invoke();
    }

    /// <summary>
    /// Maps a MoodTier to a happiness drift rate (units/sec).
    /// Positive = gaining happiness, negative = losing.
    /// </summary>
    private float GetDriftRate(MoodTier tier)
    {
        switch (tier)
        {
            case MoodTier.Elated: return elatedRate;
            case MoodTier.Content: return contentRate;
            case MoodTier.Neutral: return neutralRate;
            case MoodTier.Uneasy: return -uneasyRate;
            case MoodTier.Miserable: return -miserableRate;
            default: return 0f;
        }
    }

    // ───────────────────────── Debug GUI ─────────────────────────

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        // Position below MoodSystem debug GUI
        float x = 10f;
        float y = 250f;
        float barWidth = 200f;
        float barHeight = 20f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), "=== HAPPINESS SYSTEM ===");
        y += 24f;

        // Happiness bar
        Color barColor = Color.Lerp(Color.red, Color.green, happiness);
        GUI.Label(new Rect(x, y, 100, barHeight), $"Happiness: {happiness:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), happiness, barColor);
        y += barHeight + 6f;

        // Current drift
        string tierName = moodSystem != null ? moodSystem.CurrentTier.ToString() : "???";
        Color driftColor = currentDriftRate >= 0f ? Color.green : Color.red;
        GUI.color = driftColor;
        GUI.Label(new Rect(x, y, 300, 20),
            $"Drift: {currentDriftRate:+0.000;-0.000}/s (from {tierName})");
        y += 22f;

        // Death status
        if (isDead)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(x, y, 300, 20), "*** DEAD ***");
            y += 22f;
        }
        else if (!deathEnabled)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(x, y, 300, 20), "[Death disabled]");
            y += 22f;
        }

        GUI.color = Color.white;
    }

    private void DrawBar(Rect rect, float value, Color color)
    {
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = color;
        Rect fill = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}