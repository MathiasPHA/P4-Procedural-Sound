using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Environmental comfort sensor. Accumulates nearby IComfortInfluence sources
/// (campfires, shelters, enemies) and blends them with a day/night baseline
/// to produce a single Comfort value (0–1).
///
/// Registers itself as an IMoodModifier on the MoodSystem so that environmental
/// comfort pushes the player's mood up or down:
///   comfort > 0.5 → positive mood rate (cozy, safe)
///   comfort < 0.5 → negative mood rate (exposed, threatened)
///   comfort = 0.5 → no mood contribution
///
/// This script no longer owns happiness, tension, or item consumption.
/// Those responsibilities have moved to HappinessSystem and HungerSystem.
///
/// SETUP:
///   1. Attach to the Player GameObject (same object as MoodSystem).
///   2. IComfortInfluence sources (campfires, etc.) register automatically.
///   3. Day/night cycle is read from DayNightMaster automatically,
///      or set manually via SetDayNightValue().
/// </summary>
public class ComfortSystem : MonoBehaviour, IMoodModifier
{
    // ───────────────────────── Tuning ─────────────────────────

    [Header("Comfort")]
    [Tooltip("Baseline comfort at full daytime (no influences nearby)")]
    [SerializeField] private float dayBaselineComfort = 0.55f;

    [Tooltip("Baseline comfort at full nighttime (no influences nearby)")]
    [SerializeField] private float nightBaselineComfort = 0.3f;

    [Tooltip("How quickly comfort responds to influence changes (lower = smoother)")]
    [SerializeField] private float comfortSmoothTime = 0.3f;

    [Header("Mood Contribution")]
    [Tooltip("Maximum mood rate (units/sec) when comfort is at 0 or 1. " +
             "At comfort 0.5 the contribution is zero.")]
    [SerializeField] private float maxMoodContribution = 0.06f;

    [Header("Day/Night")]
    [Tooltip("Hours considered 'full day' (comfort uses day baseline at full strength)")]
    [SerializeField] private float dayStartHour = 6f;
    [SerializeField] private float dayEndHour = 18f;

    [Tooltip("Hours over which the day/night transition blends (sunrise/sunset duration)")]
    [SerializeField] private float transitionHours = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    // ───────────────────────── State ─────────────────────────

    private float comfort = 0.5f;           // 0 = terrified, 1 = cozy
    private float rawComfort = 0.5f;        // unsmoothed accumulation
    private float comfortVelocity;          // for SmoothDamp
    private float dayNightValue = 1f;       // 0 = full night, 1 = full day

    /// <summary>Baseline comfort derived from day/night cycle.</summary>
    private float BaselineComfort => Mathf.Lerp(nightBaselineComfort, dayBaselineComfort, dayNightValue);

    // Registered influence sources
    private readonly List<IComfortInfluence> influences = new List<IComfortInfluence>();

    // MoodSystem reference
    private MoodSystem moodSystem;

    // ───────────────────────── IMoodModifier ─────────────────────────

    public float MoodRate
    {
        get
        {
            // Map comfort (0–1) to a rate centered on 0.5
            // comfort 1.0 → +maxMoodContribution
            // comfort 0.5 → 0
            // comfort 0.0 → -maxMoodContribution
            float offset = (comfort - 0.5f) * 2f; // range: -1 to +1
            return offset * maxMoodContribution;
        }
    }

    public string ModifierName => "Environment";
    public bool IsActive => enabled;

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Current comfort value (0–1). Hidden from the player.</summary>
    public float Comfort => comfort;

    /// <summary>Current day/night value (0 = night, 1 = day).</summary>
    public float DayNightValue => dayNightValue;

    /// <summary>
    /// Manually set the day/night value (0 = full night, 1 = full day).
    /// Only needed if you don't want automatic reading from DayNightMaster.
    /// </summary>
    public void SetDayNightValue(float value)
    {
        dayNightValue = Mathf.Clamp01(value);
    }

    /// <summary>Register an influence source (call from OnEnable or when spawning).</summary>
    public void RegisterInfluence(IComfortInfluence influence)
    {
        if (!influences.Contains(influence))
            influences.Add(influence);
    }

    /// <summary>Unregister an influence source (call from OnDisable or when despawning).</summary>
    public void UnregisterInfluence(IComfortInfluence influence)
    {
        influences.Remove(influence);
    }

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Start()
    {
        moodSystem = MoodSystem.Instance;
        if (moodSystem == null)
            moodSystem = FindObjectOfType<MoodSystem>();

        if (moodSystem != null)
            moodSystem.Register(this);
        else
            Debug.LogWarning("[ComfortSystem] No MoodSystem found — " +
                             "environmental comfort won't affect mood.");
    }

    private void OnDestroy()
    {
        if (moodSystem != null)
            moodSystem.Unregister(this);
    }

    private void Update()
    {
        UpdateDayNightFromMaster();
        UpdateComfort();
    }

    // ───────────────────────── Core ─────────────────────────

    /// <summary>
    /// Automatically reads DayNightMaster.Instance.currentTime and converts
    /// it to a 0–1 day/night value with smooth sunrise/sunset transitions.
    /// </summary>
    private void UpdateDayNightFromMaster()
    {
        if (DayNightMaster.Instance == null) return;

        float hour = DayNightMaster.Instance.currentTime;

        // Sunrise ramp: 0 at (dayStart - transition) → 1 at dayStart
        float sunriseStart = dayStartHour - transitionHours;
        float sunriseEnd   = dayStartHour;

        // Sunset ramp: 1 at dayEnd → 0 at (dayEnd + transition)
        float sunsetStart = dayEndHour;
        float sunsetEnd   = dayEndHour + transitionHours;

        float value;
        if (hour >= sunriseEnd && hour <= sunsetStart)
        {
            value = 1f; // full day
        }
        else if (hour >= sunriseStart && hour < sunriseEnd)
        {
            value = Mathf.InverseLerp(sunriseStart, sunriseEnd, hour); // sunrise
        }
        else if (hour > sunsetStart && hour <= sunsetEnd)
        {
            value = 1f - Mathf.InverseLerp(sunsetStart, sunsetEnd, hour); // sunset
        }
        else
        {
            value = 0f; // full night
        }

        dayNightValue = value;
    }

    private void UpdateComfort()
    {
        // Accumulate all active influences
        float totalWeight = 0f;
        float weightedSum = 0f;
        Transform playerTransform = transform;

        for (int i = influences.Count - 1; i >= 0; i--)
        {
            IComfortInfluence inf = influences[i];

            // Clean up destroyed objects
            if (inf == null || inf.IsDestroyed)
            {
                influences.RemoveAt(i);
                continue;
            }

            float distance = Vector3.Distance(playerTransform.position, inf.Position);
            if (distance > inf.Radius) continue;

            // Falloff: full strength at center, zero at edge
            float falloff = 1f - Mathf.Clamp01(distance / inf.Radius);
            falloff = falloff * falloff; // quadratic falloff

            float effectiveWeight = inf.Weight * falloff;
            weightedSum += inf.ComfortValue * effectiveWeight;
            totalWeight += effectiveWeight;
        }

        // Blend accumulated influences with baseline
        if (totalWeight > 0f)
        {
            float influenceStrength = Mathf.Clamp01(totalWeight);
            float influenceComfort = weightedSum / totalWeight;
            rawComfort = Mathf.Lerp(BaselineComfort, influenceComfort, influenceStrength);
        }
        else
        {
            rawComfort = BaselineComfort;
        }

        rawComfort = Mathf.Clamp01(rawComfort);

        // Smooth the transition
        comfort = Mathf.SmoothDamp(comfort, rawComfort, ref comfortVelocity, comfortSmoothTime);
    }

    // ───────────────────────── Debug GUI ─────────────────────────

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        float x = 320f;  // offset right so it doesn't overlap MoodSystem debug
        float y = 10f;
        float barWidth = 200f;
        float barHeight = 20f;
        float spacing = 6f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), "=== COMFORT (Environment) ===");
        y += 24f;

        // Day/Night bar
        Color dayNightColor = Color.Lerp(new Color(0.1f, 0.1f, 0.4f),
                                          new Color(1f, 0.9f, 0.4f), dayNightValue);
        GUI.Label(new Rect(x, y, 100, barHeight), $"Day/Night: {dayNightValue:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), dayNightValue, dayNightColor);
        y += barHeight + spacing;

        // Baseline
        GUI.Label(new Rect(x, y, 300, 18), $"  Baseline: {BaselineComfort:F2}");
        y += 20f;

        // Comfort bar
        GUI.Label(new Rect(x, y, 100, barHeight), $"Comfort: {comfort:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), comfort, Color.cyan);
        y += barHeight + spacing;

        // Mood contribution
        Color rateColor = MoodRate >= 0f ? Color.green : Color.red;
        GUI.color = rateColor;
        GUI.Label(new Rect(x, y, 300, 20),
            $"→ Mood rate: {MoodRate:+0.000;-0.000}/s");
        y += 22f;

        // Active influences
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), $"Influences ({influences.Count}):");
        y += 20f;

        foreach (var inf in influences)
        {
            if (inf == null || inf.IsDestroyed) continue;
            float dist = Vector3.Distance(transform.position, inf.Position);
            string status = dist <= inf.Radius ? "IN RANGE" : "out of range";
            GUI.Label(new Rect(x + 10, y, 400, 18),
                $"  {inf.Name}: val={inf.ComfortValue:F2} w={inf.Weight:F2} r={inf.Radius:F0} [{status}]");
            y += 18f;
        }
    }

    private void DrawBar(Rect rect, float value, Color color)
    {
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = color;
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        // Midpoint marker
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        float midX = rect.x + rect.width * 0.5f;
        GUI.DrawTexture(new Rect(midX - 1, rect.y, 2, rect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}