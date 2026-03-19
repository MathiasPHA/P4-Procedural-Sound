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

    [Tooltip("Tension above this triggers Horror")]
    [SerializeField] private float horrorThreshold = 0.9f;

    [Tooltip("Tension above this triggers Combat (if below horror)")]
    [SerializeField] private float combatThreshold = 0.75f;

    [Tooltip("Tension above this triggers Pressure (if below combat)")]
    [SerializeField] private float pressureThreshold = 0.5f;

    [Tooltip("Tension above this triggers Spooky (if below pressure)")]
    [SerializeField] private float spookyThreshold = 0.35f;

    [Tooltip("Tension below this triggers Cozy")]
    [SerializeField] private float cozyThreshold = 0.12f;

    [Tooltip("How long tension must stay past a threshold before switching state (prevents flickering)")]
    [SerializeField] private float stateChangeDelay = 2f;

    // State switching internals
    private GameMusicState pendingState;
    private GameMusicState currentMusicState;
    private float stateTimer;
    private bool manualStateOverride;

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
        // Map tension ranges to your game states
        // Highest threshold wins, checked top-down
        GameMusicState suggestedState;

        if (tension >= horrorThreshold)
            suggestedState = GameMusicState.Horror;
        else if (tension >= combatThreshold)
            suggestedState = GameMusicState.Combat;
        else if (tension >= pressureThreshold)
            suggestedState = GameMusicState.Pressure;
        else if (tension >= spookyThreshold)
            suggestedState = GameMusicState.Exploring;
        else if (tension <= cozyThreshold)
            suggestedState = GameMusicState.Cozy;
        else
            suggestedState = GameMusicState.Exploring;

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
}