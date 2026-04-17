using UnityEngine;

/// <summary>
/// Compact always-visible HUD that shows the full mood/happiness cascade
/// as a stacked bar breakdown. Lets you see at a glance exactly what's
/// pushing mood up or down and by how much.
///
/// Shows:
///   - Each IMoodModifier's contribution as a colored bar segment
///   - Net mood rate with direction arrow
///   - Current mood value + tier (with color)
///   - Current happiness drift rate
///   - Time to next tier change (tells you "Neutral in 8s")
///
/// SETUP:
///   Attach to the Player (same GameObject as MoodSystem) or any
///   persistent object. Toggle with F2 or the showHUD field.
/// </summary>
public class MoodRateHUD : MonoBehaviour
{
    [Header("Toggle")]
    [SerializeField] private UnityEngine.InputSystem.Key toggleKey = UnityEngine.InputSystem.Key.F2;
    [SerializeField] private bool showHUD = true;

    [Header("Position")]
    [SerializeField] private float hudX = 10f;
    [SerializeField] private float hudY = 420f;
    [SerializeField] private float hudWidth = 300f;

    // Cached refs
    private MoodSystem      mood;
    private HappinessSystem happiness;
    private ComfortSystem   comfort;
    private HungerSystem    hunger;

    // GUI
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle boldStyle;
    private bool     stylesReady;
    private Texture2D whiteTex;

    private void Start()
    {
        CacheRefs();
    }

    private void CacheRefs()
    {
        mood      = MoodSystem.Instance      ?? FindObjectOfType<MoodSystem>();
        happiness = HappinessSystem.Instance  ?? FindObjectOfType<HappinessSystem>();
        comfort   = FindObjectOfType<ComfortSystem>();
        hunger    = HungerSystem.Instance     ?? FindObjectOfType<HungerSystem>();
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current[toggleKey].wasPressedThisFrame)
            showHUD = !showHUD;
    }

    private void OnGUI()
    {
        if (!showHUD || mood == null) return;
        InitStyles();

        float x = hudX;
        float y = hudY;
        float w = hudWidth;

        // Background panel
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        float panelHeight = 210f;
        GUI.DrawTexture(new Rect(x - 6, y - 4, w + 12, panelHeight), whiteTex);
        GUI.color = Color.white;

        // ── Title ──
        GUI.Label(new Rect(x, y, w, 18), "MOOD RATE BREAKDOWN", headerStyle);
        y += 20f;

        // ── Per-modifier bars ──
        float barHeight = 16f;
        float maxRate = 0.15f; // scale: the bar's full width represents this rate
        float barAreaWidth = w - 110f;
        float barLeft = x + 100f;
        float barCenter = barLeft + barAreaWidth * 0.5f;

        // Center line label
        GUI.color = new Color(1f, 1f, 1f, 0.3f);
        GUI.DrawTexture(new Rect(barCenter, y, 1, 0), whiteTex);
        GUI.color = Color.white;

        float totalPositive = 0f;
        float totalNegative = 0f;

        var mods = mood.Modifiers;
        for (int i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];
            if (mod is Object obj && obj == null) continue;

            float rate = mod.IsActive ? mod.MoodRate : 0f;
            string name = mod.ModifierName;

            // Track totals
            if (rate > 0f) totalPositive += rate;
            else           totalNegative += rate;

            // Label
            GUI.color = !mod.IsActive ? Color.gray
                      : rate > 0.001f ? new Color(0.5f, 1f, 0.5f)
                      : rate < -0.001f ? new Color(1f, 0.5f, 0.5f)
                      : Color.white;
            GUI.Label(new Rect(x, y, 95, barHeight), name, labelStyle);

            // Rate value
            string rateStr = rate.ToString("+0.000;-0.000");
            GUI.Label(new Rect(x + 48, y, 52, barHeight), rateStr, smallStyle);

            // Bar from center
            float barPixels = Mathf.Abs(rate) / maxRate * (barAreaWidth * 0.5f);
            barPixels = Mathf.Min(barPixels, barAreaWidth * 0.5f);

            Color barColor;
            float barStartX;

            if (rate >= 0f)
            {
                barColor = new Color(0.3f, 0.85f, 0.4f, 0.8f);
                barStartX = barCenter;
            }
            else
            {
                barColor = new Color(0.95f, 0.35f, 0.35f, 0.8f);
                barStartX = barCenter - barPixels;
            }

            GUI.color = barColor;
            if (barPixels > 1f)
                GUI.DrawTexture(new Rect(barStartX, y + 2, barPixels, barHeight - 4), whiteTex);

            // Center line
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(barCenter, y, 1, barHeight), whiteTex);

            GUI.color = Color.white;
            y += barHeight + 2f;
        }

        // ── Net rate ──
        y += 4f;
        float netRate = mood.CurrentRate;
        string arrow = netRate > 0.002f ? " ▲" : netRate < -0.002f ? " ▼" : " ●";
        Color netColor = netRate > 0.002f  ? new Color(0.4f, 1f, 0.4f)
                       : netRate < -0.002f ? new Color(1f, 0.4f, 0.4f)
                       : new Color(0.8f, 0.8f, 0.8f);

        GUI.color = netColor;
        GUI.Label(new Rect(x, y, w, 20),
            $"Net mood rate: {netRate:+0.000;-0.000}/s{arrow}", boldStyle);
        GUI.color = Color.white;
        y += 22f;

        // ── Current mood + tier ──
        Color tierColor = GetTierColor(mood.CurrentTier);
        GUI.color = tierColor;
        GUI.Label(new Rect(x, y, w, 18),
            $"Mood: {mood.Mood:F2}   Tier: {mood.CurrentTier}", labelStyle);
        GUI.color = Color.white;
        y += 20f;

        // ── Mood bar ──
        DrawMoodBar(new Rect(x, y, w, 10), mood.Mood);
        y += 16f;

        // ── Time to next tier ──
        string nextTierInfo = GetNextTierInfo(mood.Mood, mood.CurrentTier, netRate);
        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x, y, w, 18), nextTierInfo, smallStyle);
        GUI.color = Color.white;
        y += 20f;

        // ── Happiness impact ──
        if (happiness != null)
        {
            float hRate = happiness.CurrentDriftRate;
            Color hColor = hRate > 0.001f ? new Color(0.5f, 1f, 0.5f)
                         : hRate < -0.001f ? new Color(1f, 0.5f, 0.5f)
                         : Color.white;
            GUI.color = hColor;
            string hArrow = hRate > 0.001f ? "▲" : hRate < -0.001f ? "▼" : "●";
            GUI.Label(new Rect(x, y, w, 18),
                $"Happiness: {happiness.Happiness:F2}  drift {hRate:+0.000;-0.000}/s {hArrow}", labelStyle);

            // Time to death estimate
            if (hRate < -0.001f)
            {
                float secsToDeath = happiness.Happiness / Mathf.Abs(hRate);
                GUI.color = new Color(1f, 0.6f, 0.3f);
                GUI.Label(new Rect(x, y + 18, w, 16),
                    $"  Death in {secsToDeath:F0}s at this rate", smallStyle);
            }

            GUI.color = Color.white;
        }
    }

    // ───────────────────────── Helpers ─────────────────────────

    private string GetNextTierInfo(float moodVal, MoodTier current, float rate)
    {
        if (Mathf.Abs(rate) < 0.001f)
            return "Mood is stable";

        // Tier thresholds (must match MoodSystem)
        float[] thresholds = { 0f, 0.20f, 0.40f, 0.60f, 0.80f, 1f };
        string[] names = { "Miserable", "Uneasy", "Neutral", "Content", "Elated" };

        int tierIdx = (int)current;

        if (rate > 0f && tierIdx < 4)
        {
            float target = thresholds[tierIdx + 1];
            float dist = target - moodVal;
            if (dist > 0f)
            {
                float secs = dist / rate;
                return $"{names[tierIdx + 1]} in ~{secs:F0}s";
            }
        }
        else if (rate < 0f && tierIdx > 0)
        {
            float target = thresholds[tierIdx];
            float dist = moodVal - target;
            if (dist > 0f)
            {
                float secs = dist / Mathf.Abs(rate);
                return $"{names[tierIdx - 1]} in ~{secs:F0}s";
            }
        }

        return rate > 0f ? "Climbing toward max" : "Falling toward min";
    }

    private void DrawMoodBar(Rect rect, float value)
    {
        // Background
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        GUI.DrawTexture(rect, whiteTex);

        // Tier zones
        float[] thresholds = { 0f, 0.20f, 0.40f, 0.60f, 0.80f, 1f };
        Color[] zoneColors =
        {
            new Color(1f, 0.2f, 0.2f, 0.15f),     // Miserable
            new Color(1f, 0.5f, 0.2f, 0.15f),      // Uneasy
            new Color(1f, 0.9f, 0.3f, 0.15f),      // Neutral
            new Color(0.6f, 0.9f, 0.3f, 0.15f),    // Content
            new Color(0.2f, 1f, 0.2f, 0.15f)        // Elated
        };

        for (int i = 0; i < 5; i++)
        {
            float startX = rect.x + rect.width * thresholds[i];
            float endX   = rect.x + rect.width * thresholds[i + 1];
            GUI.color = zoneColors[i];
            GUI.DrawTexture(new Rect(startX, rect.y, endX - startX, rect.height), whiteTex);
        }

        // Threshold lines
        GUI.color = new Color(1f, 1f, 1f, 0.3f);
        for (int i = 1; i < 5; i++)
        {
            float lineX = rect.x + rect.width * thresholds[i];
            GUI.DrawTexture(new Rect(lineX, rect.y, 1, rect.height), whiteTex);
        }

        // Current position marker
        GUI.color = Color.white;
        float markerX = rect.x + rect.width * Mathf.Clamp01(value);
        GUI.DrawTexture(new Rect(markerX - 1, rect.y - 2, 3, rect.height + 4), whiteTex);

        GUI.color = Color.white;
    }

    private Color GetTierColor(MoodTier tier)
    {
        switch (tier)
        {
            case MoodTier.Elated:    return new Color(0.2f, 1f, 0.2f);
            case MoodTier.Content:   return new Color(0.6f, 0.9f, 0.3f);
            case MoodTier.Neutral:   return new Color(1f, 0.9f, 0.3f);
            case MoodTier.Uneasy:    return new Color(1f, 0.5f, 0.2f);
            case MoodTier.Miserable: return new Color(1f, 0.2f, 0.2f);
            default:                 return Color.white;
        }
    }

    private void InitStyles()
    {
        if (stylesReady) return;

        whiteTex = Texture2D.whiteTexture;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 11,
            normal = { textColor = new Color(1f, 0.9f, 0.4f) }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };

        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };

        boldStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 13,
            normal = { textColor = Color.white }
        };

        stylesReady = true;
    }
}
