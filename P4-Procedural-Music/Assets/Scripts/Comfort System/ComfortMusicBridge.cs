using UnityEngine;
using ProceduralMusic.Bridge;

/// <summary>
/// Bridges the ComfortSystem's tension output to the ProceduralMusicController singleton.
/// Attach this to any convenient GameObject (the player, the music object, or its own).
///
/// Each frame it reads ComfortSystem.Tension and calls SetTension() on the music controller.
/// Optionally maps tension ranges to game music states for structural changes.
/// </summary>
public class ComfortMusicBridge : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Auto-finds if left empty")]
    [SerializeField] private ComfortSystem comfortSystem;

    [Header("Tension Mapping")]
    [Tooltip("Curve to reshape the comfort tension before sending to music. " +
             "X = ComfortSystem tension (0–1), Y = music tension (0–1). " +
             "Leave as default linear if you want 1:1 mapping.")]
    [SerializeField] private AnimationCurve tensionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

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

    private ProceduralMusicController Music => ProceduralMusicController.Instance;

    private void Start()
    {
        if (comfortSystem == null)
            comfortSystem = FindObjectOfType<ComfortSystem>();

        if (comfortSystem == null)
            Debug.LogWarning("[ComfortMusicBridge] No ComfortSystem found. Tension won't update.");

        if (Music == null)
            Debug.LogWarning("[ComfortMusicBridge] ProceduralMusicController.Instance is null. " +
                             "Make sure the music system initializes before this script.");
        else
            currentMusicState = Music.CurrentState;
    }

    private void Update()
    {
        if (comfortSystem == null || Music == null) return;

        // Read tension from comfort system and remap through the curve
        float rawTension = comfortSystem.Tension;
        float mappedTension = tensionCurve.Evaluate(rawTension);

        // Send to music system
        Music.SetTension(mappedTension);

        // Optionally handle state switching
        if (autoSwitchStates && !manualStateOverride)
            UpdateAutoState(mappedTension);
    }

    private void UpdateAutoState(float tension)
    {
        bool isNight = comfortSystem != null && comfortSystem.DayNightValue < nightThreshold;

        // Reset to Exploring when night falls
        if (isNight && !wasNight)
            useExploring2 = false;
        wasNight = isNight;

        GameMusicState suggestedState;

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

        // Hysteresis: require the suggested state to hold for stateChangeDelay
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
    /// Call from your day/night script when you want the more upbeat variant.
    /// </summary>
    public void SetExploring2(bool active)
    {
        useExploring2 = active;
    }
}