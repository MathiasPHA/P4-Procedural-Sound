using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runtime dev tool for tuning and testing the comfort / hunger / mood / happiness cascade.
///
/// Press the toggle key (default F1) during play mode to show/hide. A small "Dev" button
/// also appears in the top-right corner so you can open it without remembering the hotkey.
///
/// Features:
///   - Live readouts of all system values, tiers, and drift rates
///   - Sliders for hunger, happiness, mood, time of day, and comfort override
///   - Quick-set buttons (0%, 50%, 100%) and time presets (Dawn, Noon, Dusk, Midnight)
///   - "Lock" toggles to pin a value every frame (e.g. lock hunger at 1.0 to isolate comfort effects)
///   - "Comfort Override" forces a specific comfort value, bypassing baseline + influences
///     (useful for testing what net mood rate a specific comfort level produces)
///
/// SETUP:
///   1. Create an empty GameObject in your scene (e.g. "DevConsole") and attach this script.
///   2. Press F1 in play mode, or click the "Dev [F1]" button in the top-right corner.
///   3. Remove or disable the GameObject before shipping a build.
///
/// Requires (all optional — console degrades gracefully if any are missing):
///   HungerSystem, HappinessSystem, MoodSystem, ComfortSystem, DayNightMaster
/// </summary>
public class DevTestConsole : MonoBehaviour
{
    // ───────────────────────── Config ─────────────────────────

    [Header("Toggle")]
    [Tooltip("Key to show/hide the dev console")]
    [SerializeField] private Key toggleKey = Key.F1;
    [SerializeField] private bool openOnStart = false;
    [SerializeField] private bool showCornerButton = true;

    [Header("Layout (pixels)")]
    [SerializeField] private float panelX = 580f;
    [SerializeField] private float panelY = 10f;
    [SerializeField] private float panelWidth = 340f;
    [SerializeField] private float maxPanelHeight = 700f;

    // ───────────────────────── State ─────────────────────────

    private bool   isOpen;
    private Vector2 scroll;

    // Working values the user edits
    private float hungerValue           = 0.8f;
    private float happinessValue        = 0.7f;
    private float moodValue             = 0.6f;
    private float timeOfDay             = 12f;
    private float comfortOverrideValue  = 0.5f;

    // Per-field locks (re-apply value every frame)
    private bool lockHunger;
    private bool lockHappiness;
    private bool lockMood;
    private bool lockTime;
    private bool overrideComfort;

    // Cached system refs
    private HungerSystem    hunger;
    private HappinessSystem happiness;
    private MoodSystem      mood;
    private ComfortSystem   comfort;

    // Lazy GUI styles
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private bool     stylesInitialized;

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Start()
    {
        isOpen = openOnStart;
        CacheRefs();
    }

    private void CacheRefs()
    {
        hunger    = HungerSystem.Instance    != null ? HungerSystem.Instance    : FindObjectOfType<HungerSystem>();
        happiness = HappinessSystem.Instance != null ? HappinessSystem.Instance : FindObjectOfType<HappinessSystem>();
        mood      = MoodSystem.Instance      != null ? MoodSystem.Instance      : FindObjectOfType<MoodSystem>();
        comfort   = FindObjectOfType<ComfortSystem>();
    }

    private void Update()
    {
        // Hotkey
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            isOpen = !isOpen;

        if (!isOpen) return;

        // Re-cache if a ref was destroyed (e.g. scene reload)
        if (hunger == null || happiness == null || mood == null || comfort == null)
            CacheRefs();

        // Apply locks every frame — this is what makes "Lock" work
        if (lockHunger    && hunger    != null) hunger.SetHunger(hungerValue);
        if (lockHappiness && happiness != null) happiness.SetHappiness(happinessValue);
        if (lockMood      && mood      != null) mood.SetMood(moodValue);
        if (lockTime      && DayNightMaster.Instance != null) DayNightMaster.Instance.currentTime = timeOfDay;

        // Comfort override toggle
        if (comfort != null)
        {
            if (overrideComfort) comfort.SetDebugComfortOverride(comfortOverrideValue);
            else                 comfort.ClearDebugComfortOverride();
        }
    }

    // ───────────────────────── GUI ─────────────────────────

    private void OnGUI()
    {
        InitStyles();

        // Corner button (shown even when panel is closed)
        if (!isOpen)
        {
            if (showCornerButton)
            {
                if (GUI.Button(new Rect(Screen.width - 90, 10, 80, 24), $"Dev [{toggleKey}]"))
                    isOpen = true;
            }
            return;
        }

        float h = Mathf.Min(Screen.height - 20, maxPanelHeight);
        GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, h));
        GUILayout.BeginVertical(boxStyle);

        // Title bar
        GUILayout.BeginHorizontal();
        GUILayout.Label("DEV CONSOLE", headerStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("X", GUILayout.Width(24))) isOpen = false;
        GUILayout.EndHorizontal();

        scroll = GUILayout.BeginScrollView(scroll);

        DrawLiveState();
        DrawHunger();
        DrawHappiness();
        DrawMood();
        DrawTime();
        DrawComfortOverride();

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    // ───────────────────────── Sections ─────────────────────────

    private void DrawLiveState()
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("— Live State —", headerStyle);

        string comfortStr = comfort != null
            ? $"{comfort.Comfort:F2}   (day/night {comfort.DayNightValue:F2})"
            : "<no ComfortSystem>";
        GUILayout.Label($"Comfort:    {comfortStr}");

        if (mood != null)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = RateColor(mood.CurrentRate);
            GUILayout.Label($"Mood:       {mood.Mood:F2}  ({mood.CurrentTier})   {FmtRate(mood.CurrentRate)}/s");
            GUI.contentColor = prev;
        }

        if (hunger != null)
        {
            string tag = hunger.IsHungry ? "  [HUNGRY]"
                       : hunger.IsWellFed ? "  [WELL-FED]"
                       : "";
            GUILayout.Label($"Hunger:     {hunger.Hunger:F2}{tag}");
        }

        if (happiness != null)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = RateColor(happiness.CurrentDriftRate);
            GUILayout.Label($"Happiness:  {happiness.Happiness:F2}   {FmtRate(happiness.CurrentDriftRate)}/s");
            GUI.contentColor = prev;
        }

        if (DayNightMaster.Instance != null)
        {
            float t = DayNightMaster.Instance.currentTime;
            int hh = Mathf.FloorToInt(t);
            int mm = Mathf.FloorToInt((t - hh) * 60f);
            GUILayout.Label($"Time:       {hh:00}:{mm:00}");
        }

        GUILayout.EndVertical();
    }

    private void DrawHunger()
    {
        GUILayout.BeginVertical(boxStyle);
        SectionHeader("Hunger", ref lockHunger);

        float v = GUILayout.HorizontalSlider(hungerValue, 0f, 1f);
        if (v != hungerValue)
        {
            hungerValue = v;
            hunger?.SetHunger(v);
        }
        GUILayout.Label($"  target: {hungerValue:F2}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Empty"))    SetHunger(0f);
        if (GUILayout.Button("Half"))     SetHunger(0.5f);
        if (GUILayout.Button("Full"))     SetHunger(1f);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawHappiness()
    {
        GUILayout.BeginVertical(boxStyle);
        SectionHeader("Happiness", ref lockHappiness);

        float v = GUILayout.HorizontalSlider(happinessValue, 0f, 1f);
        if (v != happinessValue)
        {
            happinessValue = v;
            happiness?.SetHappiness(v);
        }
        GUILayout.Label($"  target: {happinessValue:F2}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0%"))       SetHappiness(0f);
        if (GUILayout.Button("50%"))      SetHappiness(0.5f);
        if (GUILayout.Button("100%"))     SetHappiness(1f);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawMood()
    {
        GUILayout.BeginVertical(boxStyle);
        SectionHeader("Mood", ref lockMood);

        float v = GUILayout.HorizontalSlider(moodValue, 0f, 1f);
        if (v != moodValue)
        {
            moodValue = v;
            mood?.SetMood(v);
        }
        GUILayout.Label($"  target: {moodValue:F2}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Miserable"))    SetMood(0.10f);
        if (GUILayout.Button("Uneasy"))       SetMood(0.30f);
        if (GUILayout.Button("Neutral"))      SetMood(0.50f);
        if (GUILayout.Button("Content"))      SetMood(0.70f);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawTime()
    {
        GUILayout.BeginVertical(boxStyle);
        SectionHeader($"Time of Day ({timeOfDay:F1}h)", ref lockTime);

        float v = GUILayout.HorizontalSlider(timeOfDay, 0f, 24f);
        if (v != timeOfDay)
        {
            timeOfDay = v;
            if (DayNightMaster.Instance != null) DayNightMaster.Instance.currentTime = v;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Dawn"))     SetTime(6f);
        if (GUILayout.Button("Noon"))     SetTime(12f);
        if (GUILayout.Button("Dusk"))     SetTime(19f);
        if (GUILayout.Button("Midnight")) SetTime(0f);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawComfortOverride()
    {
        GUILayout.BeginVertical(boxStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Comfort Override", headerStyle);
        GUILayout.FlexibleSpace();
        overrideComfort = GUILayout.Toggle(overrideComfort, " Active");
        GUILayout.EndHorizontal();

        GUI.enabled = overrideComfort;
        comfortOverrideValue = GUILayout.HorizontalSlider(comfortOverrideValue, 0f, 1f);
        GUILayout.Label($"  forced comfort = {comfortOverrideValue:F2}");
        GUI.enabled = true;

        if (!overrideComfort)
            GUILayout.Label("  (toggle Active to bypass baseline + influences)",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, fontSize = 10 });

        GUILayout.EndVertical();
    }

    // ───────────────────────── Quick-set helpers ─────────────────────────

    private void SetHunger(float v)    { hungerValue = v;    hunger?.SetHunger(v); }
    private void SetHappiness(float v) { happinessValue = v; happiness?.SetHappiness(v); }
    private void SetMood(float v)      { moodValue = v;      mood?.SetMood(v); }
    private void SetTime(float v)
    {
        timeOfDay = v;
        if (DayNightMaster.Instance != null) DayNightMaster.Instance.currentTime = v;
    }

    // ───────────────────────── Styling ─────────────────────────

    private void SectionHeader(string title, ref bool locked)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(title, headerStyle);
        GUILayout.FlexibleSpace();
        locked = GUILayout.Toggle(locked, " Lock");
        GUILayout.EndHorizontal();
    }

    private Color RateColor(float r)
    {
        if (r >  0.001f) return new Color(0.5f, 1f, 0.5f);
        if (r < -0.001f) return new Color(1f, 0.5f, 0.5f);
        return Color.white;
    }

    private string FmtRate(float r) => r.ToString("+0.000;-0.000");

    private void InitStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.9f, 0.4f) }
        };

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 6, 6)
        };

        stylesInitialized = true;
    }
}
