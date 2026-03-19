using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Drives musical tension through two interacting floats:
///   - Comfort (0–1): invisible accumulation of nearby environmental influences
///   - Happiness (0–1): visible "health bar" that drifts up/down based on comfort
///
/// Tension output (0–1) is derived from both values and fed to the music system.
/// </summary>
public class ComfortSystem : MonoBehaviour
{
    // ───────────────────────── Tuning ─────────────────────────

    [Header("Happiness Drift")]
    [Tooltip("How fast happiness rises when comfort > 0.5 (units per second at full comfort)")]
    [SerializeField] private float happinessRiseRate = 0.08f;

    [Tooltip("How fast happiness falls when comfort < 0.5 (units per second at zero comfort)")]
    [SerializeField] private float happinessFallRate = 0.12f;

    [Tooltip("Smooth the happiness changes so they don't feel jerky")]
    [SerializeField] private float happinessSmoothTime = 0.5f;

    [Header("Comfort")]
    [Tooltip("Baseline comfort at full daytime (no influences nearby)")]
    [SerializeField] private float dayBaselineComfort = 0.55f;

    [Tooltip("Baseline comfort at full nighttime (no influences nearby)")]
    [SerializeField] private float nightBaselineComfort = 0.3f;

    [Tooltip("How quickly comfort responds to influence changes (lower = smoother)")]
    [SerializeField] private float comfortSmoothTime = 0.3f;

    [Header("Tension Mapping")]
    [Tooltip("How much happiness contributes to tension (vs comfort)")]
    [Range(0f, 1f)]
    [SerializeField] private float happinessTensionWeight = 0.7f;

    [Tooltip("How much comfort directly contributes to tension")]
    [Range(0f, 1f)]
    [SerializeField] private float comfortTensionWeight = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    // ───────────────────────── State ─────────────────────────

    private float happiness = 0.5f;          // 0 = miserable, 1 = elated
    private float comfort = 0.5f;            // 0 = terrified, 1 = cozy
    private float rawComfort = 0.5f;         // unsmoothed accumulation
    private float happinessVelocity;         // for SmoothDamp
    private float comfortVelocity;           // for SmoothDamp
    private float tension = 0.5f;            // final output for music
    private float dayNightValue = 1f;        // 0 = full night, 1 = full day

    /// <summary>Baseline comfort derived from day/night cycle.</summary>
    private float BaselineComfort => Mathf.Lerp(nightBaselineComfort, dayBaselineComfort, dayNightValue);

    // Registered influence sources
    private readonly List<IComfortInfluence> influences = new List<IComfortInfluence>();

    // Optional: reference to your music bridge (assign in inspector or find at start)
    // [SerializeField] private ProceduralMusicBridge musicBridge;

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Current happiness value (0–1). Visible to the player.</summary>
    public float Happiness => happiness;

    /// <summary>Current comfort value (0–1). Hidden from the player.</summary>
    public float Comfort => comfort;

    /// <summary>Current musical tension (0–1). Fed to the music system.</summary>
    public float Tension => tension;

    /// <summary>Current day/night value (0 = night, 1 = day).</summary>
    public float DayNightValue => dayNightValue;

    /// <summary>
    /// Set the day/night value (0 = full night, 1 = full day).
    /// Call this from your day/night cycle system each frame or when it changes.
    /// Night lowers the baseline comfort, making happiness drift downward
    /// unless the player is near comforting influences like campfires.
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

    /// <summary>Directly set happiness (e.g. story events, item pickups).</summary>
    public void SetHappiness(float value)
    {
        happiness = Mathf.Clamp01(value);
    }

    /// <summary>Add or subtract from happiness directly.</summary>
    public void AdjustHappiness(float delta)
    {
        happiness = Mathf.Clamp01(happiness + delta);
    }

    // ───────────────────────── Core Loop ─────────────────────────

    private void Update()
    {
        UpdateComfort();
        UpdateHappiness();
        UpdateTension();

        // Feed tension to your music system here:
        // musicBridge?.SetGameParameter("tension", tension);
    }

    private void UpdateComfort()
    {
        // Accumulate all active influences
        float totalWeight = 0f;
        float weightedSum = 0f;
        Transform playerTransform = transform; // assumes this is on the player

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
            falloff = falloff * falloff; // quadratic falloff feels more natural

            float effectiveWeight = inf.Weight * falloff;
            weightedSum += inf.ComfortValue * effectiveWeight;
            totalWeight += effectiveWeight;
        }

        // Blend accumulated influences with baseline
        if (totalWeight > 0f)
        {
            // Mix: if total influence weight is small, baseline dominates
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

    private void UpdateHappiness()
    {
        // How far comfort is from the neutral midpoint (0.5)
        float comfortOffset = comfort - 0.5f; // range: -0.5 to +0.5

        float targetDelta;
        if (comfortOffset > 0f)
        {
            // Comfort above 0.5: happiness rises
            // Scale: at comfort=1.0, offset=0.5, rise at full rate
            targetDelta = (comfortOffset / 0.5f) * happinessRiseRate * Time.deltaTime;
        }
        else
        {
            // Comfort below 0.5: happiness falls
            // Scale: at comfort=0.0, offset=-0.5, fall at full rate
            targetDelta = (comfortOffset / 0.5f) * happinessFallRate * Time.deltaTime;
        }

        float targetHappiness = Mathf.Clamp01(happiness + targetDelta);

        // Smooth the happiness change
        happiness = Mathf.SmoothDamp(happiness, targetHappiness, ref happinessVelocity, happinessSmoothTime);
        happiness = Mathf.Clamp01(happiness);
    }

    private void UpdateTension()
    {
        // Tension is the inverse of the weighted blend of happiness and comfort
        // Low happiness + low comfort = high tension
        float happinessContribution = (1f - happiness) * happinessTensionWeight;
        float comfortContribution = (1f - comfort) * comfortTensionWeight;

        // Normalize so tension spans 0–1 regardless of weight configuration
        float totalWeight = happinessTensionWeight + comfortTensionWeight;
        float rawTension = (happinessContribution + comfortContribution) / Mathf.Max(totalWeight, 0.001f);

        // Optional: apply a curve to make tension feel more dramatic at extremes
        tension = TensionCurve(rawTension);
    }

    /// <summary>
    /// Shape the tension response. Default uses a slight S-curve
    /// to make the middle range more stable and the extremes more pronounced.
    /// </summary>
    private float TensionCurve(float t)
    {
        // Hermite-style S-curve: 3t² - 2t³
        return t * t * (3f - 2f * t);
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
        GUI.Label(new Rect(x, y, 300, 20), "=== COMFORT SYSTEM ===");
        y += 24f;

        // Day/Night bar (yellow to dark blue)
        Color dayNightColor = Color.Lerp(new Color(0.1f, 0.1f, 0.4f), new Color(1f, 0.9f, 0.4f), dayNightValue);
        GUI.Label(new Rect(x, y, 100, barHeight), $"Day/Night: {dayNightValue:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), dayNightValue, dayNightColor);
        y += barHeight + spacing;

        // Baseline comfort (derived from day/night)
        GUI.Label(new Rect(x, y, 300, 18), $"  Baseline comfort: {BaselineComfort:F2}");
        y += 20f;

        // Comfort bar (cyan)
        GUI.Label(new Rect(x, y, 100, barHeight), $"Comfort: {comfort:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), comfort, Color.cyan);
        y += barHeight + spacing;

        // Happiness bar (yellow/green)
        Color happyColor = Color.Lerp(Color.red, Color.green, happiness);
        GUI.Label(new Rect(x, y, 100, barHeight), $"Happiness: {happiness:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), happiness, happyColor);
        y += barHeight + spacing;

        // Tension bar (red)
        GUI.Label(new Rect(x, y, 100, barHeight), $"Tension: {tension:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), tension, Color.Lerp(Color.green, Color.red, tension));
        y += barHeight + spacing;

        // Active influences
        GUI.Label(new Rect(x, y, 300, 20), $"Active influences: {influences.Count}");
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
        // Background
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        // Fill
        GUI.color = color;
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        // Midpoint marker for comfort bar
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        float midX = rect.x + rect.width * 0.5f;
        GUI.DrawTexture(new Rect(midX - 1, rect.y, 2, rect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}
