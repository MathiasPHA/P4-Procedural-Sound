using UnityEngine;
using ProceduralMusic.Bridge;

/// <summary>
/// Central game state manager that controls music state switching.
/// 
/// Two modes of operation:
///   1. AUTO — ComfortMusicBridge drives state from tension thresholds (default)
///   2. MANUAL — Game code forces a specific state (combat triggers, story events, areas)
///
/// Manual overrides automatically expire after a timeout, or you can release them explicitly.
/// This prevents the game from getting stuck in a state if you forget to release it.
///
/// Usage:
///   GameStateManager.Instance.EnterCombat();
///   GameStateManager.Instance.EnterState(GameMusicState.Horror);
///   GameStateManager.Instance.ReturnToAuto();
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Auto-finds if left empty")]
    [SerializeField] private ComfortMusicBridge comfortMusicBridge;

    [Header("Override Settings")]
    [Tooltip("Max seconds a manual override lasts before auto-releasing (0 = never auto-release)")]
    [SerializeField] private float defaultOverrideTimeout = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    // State tracking
    private GameMusicState currentState;
    private bool isManualOverride;
    private float overrideTimer;
    private float overrideTimeout;
    private string overrideReason = "";

    private ProceduralMusicController Music => ProceduralMusicController.Instance;

    // ───────────────────────── Public Read ─────────────────────────

    /// <summary>Current active game music state.</summary>
    public GameMusicState CurrentState => currentState;

    /// <summary>True if a manual override is active (auto-switching is paused).</summary>
    public bool IsManualOverride => isManualOverride;

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (comfortMusicBridge == null)
            comfortMusicBridge = FindObjectOfType<ComfortMusicBridge>();

        if (Music != null)
            currentState = Music.CurrentState;
    }

    private void Update()
    {
        // Track current state from music controller
        if (Music != null && !isManualOverride)
            currentState = Music.CurrentState;

        // Handle override timeout
        if (isManualOverride && overrideTimeout > 0f)
        {
            overrideTimer += Time.deltaTime;
            if (overrideTimer >= overrideTimeout)
                ReturnToAuto();
        }
    }

    // ───────────────────────── State Switching ─────────────────────────

    /// <summary>
    /// Switch to a specific music state. Pauses automatic tension-based switching.
    /// </summary>
    /// <param name="state">The target music state.</param>
    /// <param name="reason">Debug label for why this override happened.</param>
    /// <param name="timeout">Seconds until auto-release (0 = use default, -1 = never).</param>
    public void EnterState(GameMusicState state, string reason = "", float timeout = 0f)
    {
        currentState = state;
        isManualOverride = true;
        overrideTimer = 0f;
        overrideTimeout = timeout > 0f ? timeout : defaultOverrideTimeout;
        overrideReason = reason;

        // Tell the music controller directly
        Music?.SetGameState(state);

        // Pause the comfort bridge's auto-switching
        comfortMusicBridge?.OverrideGameState(state);
    }

    /// <summary>
    /// Return to automatic tension-based state switching from the ComfortMusicBridge.
    /// </summary>
    public void ReturnToAuto()
    {
        isManualOverride = false;
        overrideTimer = 0f;
        overrideReason = "";

        comfortMusicBridge?.ReleaseStateOverride();
    }

    // ───────────────────────── Convenience Methods ─────────────────────────

    /// <summary>Enter combat music. Call when enemies engage.</summary>
    public void EnterCombat(float timeout = 0f)
    {
        EnterState(GameMusicState.Combat, "Combat started", timeout);
    }

    /// <summary>Enter horror music. Call when the beast/evil presence is near.</summary>
    public void EnterHorror(float timeout = 0f)
    {
        EnterState(GameMusicState.Horror, "Horror triggered", timeout);
    }

    /// <summary>Enter spooky music. Call when entering unsettling areas.</summary>
    public void EnterSpooky(float timeout = 0f)
    {
        EnterState(GameMusicState.Spooky, "Spooky area", timeout);
    }

    /// <summary>Enter cozy music. Call when sitting by fire, in shelter, etc.</summary>
    public void EnterCozy(float timeout = 0f)
    {
        EnterState(GameMusicState.Cozy, "Cozy moment", timeout);
    }

    /// <summary>Enter night exploration music.</summary>
    public void EnterNight(float timeout = 0f)
    {
        EnterState(GameMusicState.Night, "Night time", timeout);
    }

    /// <summary>Enter pressure/chase music.</summary>
    public void EnterPressure(float timeout = 0f)
    {
        EnterState(GameMusicState.Pressure, "Pressure building", timeout);
    }

    /// <summary>Switch between Exploring and Exploring2 (more upbeat).</summary>
    public void EnterExploring(bool upbeat = false, float timeout = 0f)
    {
        var state = upbeat ? GameMusicState.Exploring2 : GameMusicState.Exploring;
        EnterState(state, upbeat ? "Upbeat exploring" : "Exploring", timeout);
    }

    /// <summary>
    /// Temporarily override for a set duration, then auto-return.
    /// Useful for one-shot events like discovering a location or a jumpscare.
    /// </summary>
    public void FlashState(GameMusicState state, float duration, string reason = "")
    {
        EnterState(state, reason.Length > 0 ? reason : "Flash: " + state, duration);
    }

    // ───────────────────────── Debug GUI ─────────────────────────

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        float x = 10f;
        float y = 300f; // below the comfort system debug GUI

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), "=== GAME STATE MANAGER ===");
        y += 24f;

        string mode = isManualOverride ? "MANUAL" : "AUTO";
        Color modeColor = isManualOverride ? Color.yellow : Color.green;
        GUI.color = modeColor;
        GUI.Label(new Rect(x, y, 300, 20), $"Mode: {mode}");
        y += 20f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), $"State: {currentState}");
        y += 20f;

        if (isManualOverride)
        {
            if (overrideReason.Length > 0)
            {
                GUI.Label(new Rect(x, y, 300, 20), $"Reason: {overrideReason}");
                y += 20f;
            }

            if (overrideTimeout > 0f)
            {
                float remaining = Mathf.Max(0f, overrideTimeout - overrideTimer);
                GUI.Label(new Rect(x, y, 300, 20), $"Auto-release in: {remaining:F1}s");
                y += 20f;
            }
        }

        // Quick-switch buttons for testing
        y += 10f;
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), "Quick switch:");
        y += 22f;

        float btnW = 80f;
        float btnH = 25f;
        float btnSpacing = 4f;
        float bx = x;

        foreach (GameMusicState state in System.Enum.GetValues(typeof(GameMusicState)))
        {
            bool isActive = currentState == state;
            GUI.color = isActive ? Color.green : Color.white;

            if (GUI.Button(new Rect(bx, y, btnW, btnH), state.ToString()))
                EnterState(state, "Debug button");

            bx += btnW + btnSpacing;
            if (bx + btnW > 400f)
            {
                bx = x;
                y += btnH + btnSpacing;
            }
        }

        y += btnH + 10f;
        GUI.color = Color.cyan;
        if (GUI.Button(new Rect(x, y, 120, btnH), "Return to Auto"))
            ReturnToAuto();

        GUI.color = Color.white;
    }
}
