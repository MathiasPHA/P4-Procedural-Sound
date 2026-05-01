using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central mood hub for the player. Mood (0–1) is driven by the sum of all
/// registered IMoodModifier rates each frame. Many systems push and pull mood:
/// hunger, environmental comfort, day/night, shelter, etc.
///
/// Mood is NOT the player-facing bar — that's HappinessSystem.
/// Mood determines the *rate* at which happiness drifts.
///
/// The 5 mood tiers map directly to the smiley faces next to the happiness bar.
///
/// SETUP:
///   1. Attach to the Player GameObject.
///   2. Other systems (HungerSystem, ComfortSystem, etc.) register as modifiers.
///   3. HappinessSystem reads MoodTier to decide happiness drift rate.
///
/// DATA FLOW:
///   [HungerSystem]  ──→ modifier ──┐
///   [ComfortSystem] ──→ modifier ──┤
///   [Day/Night]     ──→ modifier ──┼──→ MoodSystem ──→ MoodTier ──→ HappinessSystem
///   [Shelter]       ──→ modifier ──┤
///   [Future...]     ──→ modifier ──┘
/// </summary>
public class MoodSystem : MonoBehaviour
{
    public static MoodSystem Instance { get; private set; }

    // ───────────────────────── Tuning ─────────────────────────

    [Header("Mood Settings")]
    [Tooltip("Starting mood value (0 = miserable, 1 = elated)")]
    [Range(0f, 1f)]
    [SerializeField] private float startingMood = 0.6f;

    [Tooltip("Smoothing applied to mood changes so they don't feel jerky. " +
             "Lower = snappier mood response (0.2-0.4 feels responsive, 1.0 feels laggy).")]
    [SerializeField] private float moodSmoothTime = 0.4f;

    [Tooltip("Clamp the total modifier sum so no single frame has an extreme swing. " +
             "Units per second — 0.2 means mood can shift at most 20% per second.")]
    [SerializeField] private float maxMoodRatePerSecond = 0.2f;

    [Header("Tier Thresholds")]
    [Tooltip("Mood >= this = Elated")]
    [SerializeField] private float elatedThreshold = 0.80f;
    [Tooltip("Mood >= this = Content")]
    [SerializeField] private float contentThreshold = 0.60f;
    [Tooltip("Mood >= this = Neutral")]
    [SerializeField] private float neutralThreshold = 0.40f;
    [Tooltip("Mood >= this = Uneasy")]
    [SerializeField] private float uneasyThreshold = 0.20f;
    // Below uneasy = Horrified (implicit)

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    // ───────────────────────── State ─────────────────────────

    private float mood;
    private float moodTarget;
    private float moodVelocity;  // for SmoothDamp
    private float lastSummedRate;

    private readonly List<IMoodModifier> modifiers = new List<IMoodModifier>();

    // ───────────────────────── Cross-Scene Persistence ─────────────────────────

    // Static cache survives scene unload — see HappinessSystem for the full
    // explanation. -1f = "nothing cached yet".
    private static float _persistedMood = -1f;

    /// <summary>
    /// Wipe the cross-scene mood cache. Call from "New Game" flows so the
    /// player starts with startingMood rather than carrying over a previous
    /// session's value.
    /// </summary>
    public static void ResetPersistedState()
    {
        _persistedMood = -1f;
    }

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Current mood value (0–1). Not shown directly to the player.</summary>
    public float Mood => mood;

    /// <summary>The current mood tier, mapping to one of the 5 smiley states.</summary>
    public MoodTier CurrentTier => GetTier(mood);

    /// <summary>The raw summed rate from all active modifiers (units/sec). Useful for debug.</summary>
    public float CurrentRate => lastSummedRate;

    /// <summary>Number of registered modifiers.</summary>
    public int ModifierCount => modifiers.Count;

    /// <summary>Read-only access to registered modifiers (for debug HUDs).</summary>
    public IReadOnlyList<IMoodModifier> Modifiers => modifiers;

    /// <summary>
    /// Register a mood modifier. Call from OnEnable or Start.
    /// Duplicate registrations are ignored.
    /// </summary>
    public void Register(IMoodModifier modifier)
    {
        if (modifier != null && !modifiers.Contains(modifier))
            modifiers.Add(modifier);
    }

    /// <summary>
    /// Unregister a mood modifier. Call from OnDisable or OnDestroy.
    /// </summary>
    public void Unregister(IMoodModifier modifier)
    {
        modifiers.Remove(modifier);
    }

    /// <summary>
    /// Instant one-shot mood adjustment. Use for events like:
    /// discovering a landmark (+0.05), jump scare (-0.1), etc.
    /// Does NOT bypass smoothing — the change is applied to the target
    /// and mood drifts toward it.
    /// </summary>
    public void AdjustMood(float delta)
    {
        moodTarget = Mathf.Clamp01(moodTarget + delta);

        // Mirror to the cross-scene cache so a one-shot adjustment landed
        // after Update already ran this frame still survives a same-frame
        // scene unload.
        _persistedMood = mood;
    }

    /// <summary>
    /// Force mood to an exact value. Use sparingly (e.g. game start, respawn).
    /// Bypasses smoothing.
    /// </summary>
    public void SetMood(float value)
    {
        mood = Mathf.Clamp01(value);
        moodTarget = mood;
        moodVelocity = 0f;

        _persistedMood = mood;
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
        if (_persistedMood >= 0f)
        {
            mood = _persistedMood;
            moodTarget = _persistedMood;
        }
        else
        {
            mood = startingMood;
            moodTarget = startingMood;
        }
    }

    private void Update()
    {
        SumModifiers();
        ApplySmoothing();

        _persistedMood = mood;
    }

    // ───────────────────────── Core ─────────────────────────

    private void SumModifiers()
    {
        float sum = 0f;

        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            IMoodModifier mod = modifiers[i];

            // Clean up destroyed MonoBehaviour modifiers
            if (mod is Object obj && obj == null)
            {
                modifiers.RemoveAt(i);
                continue;
            }

            if (!mod.IsActive) continue;

            sum += mod.MoodRate;
        }

        // Clamp the total rate to prevent extreme swings
        sum = Mathf.Clamp(sum, -maxMoodRatePerSecond, maxMoodRatePerSecond);
        lastSummedRate = sum;

        // Advance the target
        moodTarget = Mathf.Clamp01(moodTarget + sum * Time.deltaTime);
    }

    private void ApplySmoothing()
    {
        mood = Mathf.SmoothDamp(mood, moodTarget, ref moodVelocity, moodSmoothTime);
        mood = Mathf.Clamp01(mood);
    }

    // ───────────────────────── Tier Mapping ─────────────────────────

    private MoodTier GetTier(float value)
    {
        if (value >= elatedThreshold) return MoodTier.Elated;
        if (value >= contentThreshold) return MoodTier.Content;
        if (value >= neutralThreshold) return MoodTier.Neutral;
        if (value >= uneasyThreshold) return MoodTier.Uneasy;
        return MoodTier.Miserable;
    }

    // ───────────────────────── Debug GUI ─────────────────────────

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        float x = 10f;
        float y = 10f;
        float barWidth = 200f;
        float barHeight = 20f;
        float spacing = 6f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), "=== MOOD SYSTEM ===");
        y += 24f;

        // Mood bar
        Color moodColor = GetTierColor(CurrentTier);
        GUI.Label(new Rect(x, y, 100, barHeight), $"Mood: {mood:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), mood, moodColor);
        y += barHeight + spacing;

        // Tier label
        GUI.color = moodColor;
        GUI.Label(new Rect(x, y, 300, 20), $"Tier: {CurrentTier}");
        y += 22f;

        // Rate
        GUI.color = lastSummedRate >= 0f ? Color.green : Color.red;
        GUI.Label(new Rect(x, y, 300, 20), $"Rate: {lastSummedRate:+0.000;-0.000}/s");
        y += 22f;

        // Active modifiers
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), $"Modifiers ({modifiers.Count}):");
        y += 20f;

        foreach (var mod in modifiers)
        {
            if (mod is Object obj && obj == null) continue;

            Color c = !mod.IsActive ? Color.gray
                    : mod.MoodRate >= 0f ? Color.green : Color.red;
            GUI.color = c;

            string active = mod.IsActive ? "" : " [OFF]";
            GUI.Label(new Rect(x + 10, y, 400, 18),
                $"{mod.ModifierName}: {mod.MoodRate:+0.000;-0.000}/s{active}");
            y += 18f;
        }

        GUI.color = Color.white;
    }

    private void DrawBar(Rect rect, float value, Color color)
    {
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = color;
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        // Tier threshold markers
        GUI.color = new Color(1f, 1f, 1f, 0.3f);
        DrawThresholdLine(rect, uneasyThreshold);
        DrawThresholdLine(rect, neutralThreshold);
        DrawThresholdLine(rect, contentThreshold);
        DrawThresholdLine(rect, elatedThreshold);

        GUI.color = Color.white;
    }

    private void DrawThresholdLine(Rect barRect, float threshold)
    {
        float lineX = barRect.x + barRect.width * threshold;
        GUI.DrawTexture(new Rect(lineX - 1, barRect.y, 2, barRect.height), Texture2D.whiteTexture);
    }

    private Color GetTierColor(MoodTier tier)
    {
        switch (tier)
        {
            case MoodTier.Elated: return new Color(0.2f, 1f, 0.2f);    // bright green
            case MoodTier.Content: return new Color(0.6f, 0.9f, 0.3f);  // yellow-green
            case MoodTier.Neutral: return new Color(1f, 0.9f, 0.3f);    // yellow
            case MoodTier.Uneasy: return new Color(1f, 0.5f, 0.2f);    // orange
            case MoodTier.Miserable: return new Color(1f, 0.2f, 0.2f);    // red
            default: return Color.white;
        }
    }
}

/// <summary>
/// The five mood tiers, mapping to the smiley faces on the UI.
/// Used by HappinessSystem to determine happiness drift rate.
/// </summary>
public enum MoodTier
{
    Miserable,  // < 0.20 — happiness drains fast
    Uneasy,     // < 0.40 — happiness drains slowly
    Neutral,    // < 0.60 — happiness stable
    Content,    // < 0.80 — happiness gains slowly
    Elated      // >= 0.80 — happiness gains fast
}