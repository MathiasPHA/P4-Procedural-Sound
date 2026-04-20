using ProceduralMusic.Bridge;
using ProceduralMusic.Core;
using UnityEngine;

/// <summary>
/// Bridges the MoodSystem and HappinessSystem to the ProceduralMusicController.
///
/// Tension is derived from mood and happiness:
///   Low mood + low happiness = high tension = intense music
///   High mood + high happiness = low tension = calm music
///
/// Maps tension ranges to game music states using three threshold sets:
///   - Day:     Cozy → Exploring → Pressure
///   - Night:   Cozy → Night → Spooky → Horror
///   - Dungeon: Defined per-dungeon via DungeonConfig SO
///
/// Day/night state switching reads from ComfortSystem.DayNightValue
/// (which auto-reads from DayNightMaster).
/// </summary>
public class ComfortMusicBridge : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Auto-finds if left empty. Only used for DayNightValue.")]
    [SerializeField] private ComfortSystem comfortSystem;

    [Header("Tension Mapping")]
    [Tooltip("Curve to reshape tension before sending to music. " +
             "X = raw tension (0–1), Y = music tension (0–1). " +
             "Leave as default linear if you want 1:1 mapping.")]
    [SerializeField] private AnimationCurve tensionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Tension Weights")]
    [Tooltip("How much happiness contributes to tension (vs mood)")]
    [Range(0f, 1f)]
    [SerializeField] private float happinessWeight = 0.6f;

    [Tooltip("How much mood contributes to tension")]
    [Range(0f, 1f)]
    [SerializeField] private float moodWeight = 0.4f;

    [Header("Automatic State Switching")]
    [Tooltip("Enable to automatically switch GameMusicState based on tension thresholds")]
    [SerializeField] private bool autoSwitchStates = true;

    [Header("Day Thresholds (Cozy → Exploring → Pressure)")]
    [SerializeField] private float dayCozyMax = 0.15f;
    [SerializeField] private float dayPressureMin = 0.6f;

    [Header("Night Thresholds (Cozy → Night → Spooky → Horror)")]
    [SerializeField] private float nightCozyMax = 0.2f;
    [SerializeField] private float nightSpookyMin = 0.6f;
    [SerializeField] private float nightHorrorMin = 0.8f;

    [Header("Day/Night")]
    [Tooltip("When the day/night value drops below this, switch to night threshold set")]
    [SerializeField] private float nightThreshold = 0.35f;

    [Tooltip("How long tension must stay past a threshold before switching state (prevents flickering)")]
    [SerializeField] private float stateChangeDelay = 2f;

    // State switching internals
    private GameMusicState pendingState;
    private GameMusicState currentMusicState;
    private float stateTimer;
    private bool manualStateOverride;
    private bool useExploring2;
    private bool wasNight;

    // Dungeon mode
    private bool inDungeonMode;
    private DungeonConfig activeDungeonConfig;

    // System references
    private MoodSystem moodSystem;
    private HappinessSystem happinessSystem;

    private float currentTension;

    private ProceduralMusicController Music => ProceduralMusicController.Instance;

    /// <summary>Current computed tension (0–1) after curve mapping. Useful for debug.</summary>
    public float Tension => currentTension;

    private void Start()
    {
        if (comfortSystem == null)
            comfortSystem = FindObjectOfType<ComfortSystem>();

        moodSystem = MoodSystem.Instance;
        if (moodSystem == null)
            moodSystem = FindObjectOfType<MoodSystem>();

        happinessSystem = HappinessSystem.Instance;
        if (happinessSystem == null)
            happinessSystem = FindObjectOfType<HappinessSystem>();

        if (moodSystem == null && happinessSystem == null)
            Debug.LogWarning("[ComfortMusicBridge] No MoodSystem or HappinessSystem found. " +
                             "Tension won't update.");

        if (Music == null)
            Debug.LogWarning("[ComfortMusicBridge] ProceduralMusicController.Instance is null. " +
                             "Make sure the music system initializes before this script.");
        else
            currentMusicState = Music.CurrentState;
    }

    private void Update()
    {
        if (Music == null) return;
        if (moodSystem == null && happinessSystem == null) return;

        // Compute tension from mood and happiness
        float rawTension = ComputeTension();
        currentTension = tensionCurve.Evaluate(rawTension);

        // Send to music system
        Music.SetTension(currentTension);

        // Optionally handle state switching
        if (autoSwitchStates && !manualStateOverride)
            UpdateAutoState(currentTension);
    }

    /// <summary>
    /// Tension = weighted inverse blend of mood and happiness.
    /// Low values of both = high tension. Same formula as the old
    /// ComfortSystem.UpdateTension() but reading from the new systems.
    /// </summary>
    private float ComputeTension()
    {
        float mood = moodSystem != null ? moodSystem.Mood : 0.5f;
        float happiness = happinessSystem != null ? happinessSystem.Happiness : 0.5f;

        float moodContribution = (1f - mood) * moodWeight;
        float happinessContribution = (1f - happiness) * happinessWeight;

        float totalWeight = moodWeight + happinessWeight;
        float raw = (moodContribution + happinessContribution) / Mathf.Max(totalWeight, 0.001f);

        // S-curve for more dramatic extremes: 3t² - 2t³
        return raw * raw * (3f - 2f * raw);
    }

    private void UpdateAutoState(float tension)
    {
        GameMusicState suggestedState;

        if (inDungeonMode && activeDungeonConfig != null)
        {
            // Dungeon: thresholds defined per-dungeon on the DungeonConfig SO
            suggestedState = activeDungeonConfig.EvaluateState(tension);
        }
        else
        {
            bool isNight = comfortSystem != null && comfortSystem.DayNightValue < nightThreshold;

            // Reset to Exploring when night falls
            if (isNight && !wasNight)
                useExploring2 = false;
            wasNight = isNight;

            if (isNight)
            {
                // Night: Cozy (0–0.2) → Night (0.2–0.6) → Spooky (0.6–0.8) → Horror (0.8–1)
                if (tension >= nightHorrorMin)
                    suggestedState = GameMusicState.Horror;
                else if (tension >= nightSpookyMin)
                    suggestedState = GameMusicState.Spooky;
                else if (tension <= nightCozyMax)
                    suggestedState = GameMusicState.Cozy;
                else
                    suggestedState = GameMusicState.Night;
            }
            else
            {
                // Day: Cozy (0–0.15) → Exploring/Exploring2 (0.15–0.6) → Pressure (0.6–1)
                if (tension >= dayPressureMin)
                    suggestedState = GameMusicState.Pressure;
                else if (tension <= dayCozyMax)
                    suggestedState = GameMusicState.Cozy;
                else
                    suggestedState = useExploring2 ? GameMusicState.Exploring2 : GameMusicState.Exploring;
            }
        }

        // Hysteresis
        if (suggestedState != pendingState)
        {
            pendingState = suggestedState;
            stateTimer = 0f;
        }
        else
        {
            stateTimer += Time.deltaTime;
        }

        if (pendingState != currentMusicState && stateTimer >= stateChangeDelay)
        {
            currentMusicState = pendingState;
            Music.SetGameState(currentMusicState);
        }
    }

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>
    /// Enter dungeon mode: tension thresholds are read from the DungeonConfig SO.
    /// Each dungeon type defines its own state progression.
    /// Called by DungeonAtmosphere on dungeon entry.
    /// </summary>
    public void EnterDungeonMode(DungeonConfig config)
    {
        inDungeonMode = true;
        activeDungeonConfig = config;
        stateTimer = 0f;

        // Immediately evaluate so music doesn't lag behind
        if (Music != null)
        {
            float tension = tensionCurve.Evaluate(ComputeTension());
            GameMusicState initialState = config.EvaluateState(tension);
            currentMusicState = initialState;
            pendingState = initialState;
            Music.SetGameState(initialState);
        }
    }

    /// <summary>
    /// Exit dungeon mode: return to day/night threshold switching.
    /// Called by DungeonAtmosphere.OnDestroy().
    /// </summary>
    public void ExitDungeonMode()
    {
        inDungeonMode = false;
        activeDungeonConfig = null;
        stateTimer = 0f;
    }

    /// <summary>
    /// Manually override the game music state (e.g. for cutscenes, dialogue).
    /// Disables auto-switching until you call ReleaseStateOverride().
    /// </summary>
    public void OverrideGameState(GameMusicState state)
    {
        manualStateOverride = true;
        currentMusicState = state;
        Music?.SetGameState(state);
    }

    /// <summary>
    /// Override the game music state and force a specific key.
    /// SetGameState runs first (applies state config), then ForceModulation
    /// overwrites the key — preventing the state's default mode from winning.
    /// Disables auto-switching until you call ReleaseStateOverride().
    /// </summary>
    public void OverrideGameState(GameMusicState state, PitchClass root, MusicalMode mode)
    {
        manualStateOverride = true;
        currentMusicState = state;
        Music?.SetGameState(state);
        Music?.ForceModulation(root, mode);
    }

    /// <summary>
    /// Release the manual state override, returning to automatic tension-based switching.
    /// </summary>
    public void ReleaseStateOverride()
    {
        manualStateOverride = false;
        stateTimer = 0f;
    }

    /// <summary>
    /// Adjust the tension curve at runtime if needed.
    /// </summary>
    public void SetTensionCurve(AnimationCurve curve)
    {
        tensionCurve = curve;
    }

    /// <summary>
    /// Switch the daytime default between Exploring and Exploring2.
    /// Automatically resets to Exploring when night falls.
    /// </summary>
    public void SetExploring2(bool active)
    {
        useExploring2 = active;
    }
}