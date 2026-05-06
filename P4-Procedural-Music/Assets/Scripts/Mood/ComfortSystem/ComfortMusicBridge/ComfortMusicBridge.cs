using ProceduralMusic.Bridge;
using ProceduralMusic.Core;
using UnityEngine;

/// <summary>
/// Bridges the MoodSystem and HappinessSystem to whichever music backend is active.
///
/// Two backends are available:
///   - Procedural : ProceduralMusicController  (runtime synthesis)
///   - Sample     : SampleMusicController      (pre-recorded clips, one per state)
///
/// Switch between them at runtime via SetBackend() or the MusicBackend inspector field.
/// All tension computation and state-switching logic is shared between both backends,
/// making this a clean A/B comparison: identical switching logic, different audio engine.
///
/// Tension is derived from mood and happiness:
///   Low mood + low happiness  → high tension → intense music
///   High mood + high happiness → low tension → calm music
///
/// Maps tension ranges to game music states using three threshold sets:
///   - Day     : Cozy → Exploring → Pressure
///   - Night   : Cozy → Night → Spooky → Horror
///   - Dungeon : Defined per-dungeon via DungeonConfig SO
///
/// Day/night state switching reads from ComfortSystem.DayNightValue
/// (which auto-reads from DayNightMaster).
/// </summary>
public class ComfortMusicBridge : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Backend selection
    // ─────────────────────────────────────────────────────────────

    public enum MusicBackend { Procedural, Sample }

    [Header("Music Backend")]
    [Tooltip("Procedural = runtime synthesis (ProceduralMusicController)\n" +
             "Sample     = pre-recorded clips  (SampleMusicController)\n\n" +
             "Can also be switched at runtime via SetBackend().")]
    [SerializeField] private MusicBackend activeBackend = MusicBackend.Procedural;

    [Tooltip("Fade duration (seconds) when switching backends mid-play. " +
             "The outgoing backend fades to silence; the incoming backend fades in.")]
    [Range(0f, 5f)]
    [SerializeField] private float backendSwitchFadeDuration = 1.5f;

    // ─────────────────────────────────────────────────────────────
    //  References
    // ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Auto-finds if left empty. Only used for DayNightValue.")]
    [SerializeField] private ComfortSystem comfortSystem;

    // ─────────────────────────────────────────────────────────────
    //  Tension mapping
    // ─────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────
    //  Auto state switching
    // ─────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────
    //  State machine internals
    // ─────────────────────────────────────────────────────────────

    private GameMusicState _pendingState;
    private GameMusicState _currentMusicState;
    private float _stateTimer;
    private bool _manualStateOverride;
    private bool _useExploring2;
    private bool _wasNight;

    // Dungeon mode
    private bool _inDungeonMode;
    private DungeonConfig _activeDungeonConfig;

    // Systems
    private MoodSystem _moodSystem;
    private HappinessSystem _happinessSystem;

    private float _currentTension;
    private float _debugTimer;

    // ─────────────────────────────────────────────────────────────
    //  Backend accessors
    // ─────────────────────────────────────────────────────────────

    private ProceduralMusicController ProceduralMusic => ProceduralMusicController.Instance;
    private SampleMusicController SampleMusic => SampleMusicController.Instance;

    /// <summary>Currently active backend.</summary>
    public MusicBackend ActiveBackend => activeBackend;

    /// <summary>Current computed tension (0–1) after curve mapping.</summary>
    public float Tension => _currentTension;

    // ─────────────────────────────────────────────────────────────
    //  Helper: call a method on whichever backend is active
    // ─────────────────────────────────────────────────────────────

    private void MusicSetTension(float t)
    {
        if (activeBackend == MusicBackend.Procedural) ProceduralMusic?.SetTension(t);
        else SampleMusic?.SetTension(t);
    }

    private void MusicSetGameState(GameMusicState s)
    {
        if (activeBackend == MusicBackend.Procedural) ProceduralMusic?.SetGameState(s);
        else SampleMusic?.SetGameState(s);
    }

    private void MusicSetGameStateAndModulation(GameMusicState s, PitchClass root, MusicalMode mode)
    {
        if (activeBackend == MusicBackend.Procedural)
        {
            ProceduralMusic?.SetGameState(s);
            ProceduralMusic?.ForceModulation(root, mode);
        }
        else
        {
            SampleMusic?.SetGameState(s);
            SampleMusic?.ForceModulation(root, mode); // stored for readback, no audio effect
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (comfortSystem == null)
            comfortSystem = FindObjectOfType<ComfortSystem>();

        _moodSystem = MoodSystem.Instance ?? FindObjectOfType<MoodSystem>();
        _happinessSystem = HappinessSystem.Instance ?? FindObjectOfType<HappinessSystem>();

        if (_moodSystem == null && _happinessSystem == null)
            Debug.LogWarning("[ComfortMusicBridge] No MoodSystem or HappinessSystem found. " +
                             "Tension won't update.");

        bool proceduralReady = ProceduralMusic != null;
        bool sampleReady = SampleMusic != null;

        if (!proceduralReady)
            Debug.LogWarning("[ComfortMusicBridge] ProceduralMusicController.Instance is null.");
        if (!sampleReady)
            Debug.LogWarning("[ComfortMusicBridge] SampleMusicController.Instance is null.");

        // Read the initial state from whichever controller is active
        if (activeBackend == MusicBackend.Procedural && proceduralReady)
            _currentMusicState = ProceduralMusic.CurrentState;
        else if (activeBackend == MusicBackend.Sample && sampleReady)
            _currentMusicState = SampleMusic.CurrentState;
    }

    private void Update()
    {
        _debugTimer += Time.deltaTime;
        if (_debugTimer >= 1f)
        {
            _debugTimer = 0f;
            Debug.Log($"[Bridge] backend={activeBackend} tension={_currentTension:F2} " +
                      $"pending={_pendingState} current={_currentMusicState} " +
                      $"timer={_stateTimer:F1}/{stateChangeDelay} override={_manualStateOverride}");
        }

        bool hasSystem = _moodSystem != null || _happinessSystem != null;
        bool hasMusic = activeBackend == MusicBackend.Procedural
                         ? ProceduralMusic != null
                         : SampleMusic != null;

        if (!hasSystem || !hasMusic) return;

        float rawTension = ComputeTension();
        _currentTension = tensionCurve.Evaluate(rawTension);

        MusicSetTension(_currentTension);

        if (autoSwitchStates && !_manualStateOverride)
            UpdateAutoState(_currentTension);
    }

    // ─────────────────────────────────────────────────────────────
    //  Tension computation (shared between both backends)
    // ─────────────────────────────────────────────────────────────

    private float ComputeTension()
    {
        float mood = _moodSystem != null ? _moodSystem.Mood : 0.5f;
        float happiness = _happinessSystem != null ? _happinessSystem.Happiness : 0.5f;

        float moodContribution = (1f - mood) * moodWeight;
        float happinessContribution = (1f - happiness) * happinessWeight;

        float totalWeight = moodWeight + happinessWeight;
        float raw = (moodContribution + happinessContribution) / Mathf.Max(totalWeight, 0.001f);

        // S-curve for more dramatic extremes: 3t² - 2t³
        return raw * raw * (3f - 2f * raw);
    }

    // ─────────────────────────────────────────────────────────────
    //  Auto state switching (shared between both backends)
    // ─────────────────────────────────────────────────────────────

    private void UpdateAutoState(float tension)
    {
        GameMusicState suggestedState;

        if (_inDungeonMode && _activeDungeonConfig != null)
        {
            suggestedState = _activeDungeonConfig.EvaluateState(tension);
        }
        else
        {
            bool isNight = comfortSystem != null && comfortSystem.DayNightValue < nightThreshold;

            if (isNight && !_wasNight)
                _useExploring2 = false;
            _wasNight = isNight;

            if (isNight)
            {
                if (tension >= nightHorrorMin) suggestedState = GameMusicState.Horror;
                else if (tension >= nightSpookyMin) suggestedState = GameMusicState.Spooky;
                else if (tension <= nightCozyMax) suggestedState = GameMusicState.Cozy;
                else suggestedState = GameMusicState.Night;
            }
            else
            {
                if (tension >= dayPressureMin) suggestedState = GameMusicState.Pressure;
                else if (tension <= dayCozyMax) suggestedState = GameMusicState.Cozy;
                else suggestedState = _useExploring2 ? GameMusicState.Exploring2 : GameMusicState.Exploring;
            }
        }

        // Hysteresis
        if (suggestedState != _pendingState)
        {
            _pendingState = suggestedState;
            _stateTimer = 0f;
        }
        else
        {
            _stateTimer += Time.deltaTime;
        }

        if (_pendingState != _currentMusicState && _stateTimer >= stateChangeDelay)
        {
            _currentMusicState = _pendingState;
            MusicSetGameState(_currentMusicState);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Public API – Backend switching
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Switch music backend at runtime.
    /// The outgoing backend fades to silence; the incoming backend fades in from the
    /// current music state so the transition is seamless.
    /// </summary>
    public void SetBackend(MusicBackend backend)
    {
        if (backend == activeBackend) return;

        StartCoroutine(SwitchBackendRoutine(backend));
    }

    private System.Collections.IEnumerator SwitchBackendRoutine(MusicBackend incoming)
    {
        float fadeDuration = backendSwitchFadeDuration;
        float halfDuration = fadeDuration * 0.5f;

        // --- Fade OUT the current backend ---
        if (activeBackend == MusicBackend.Procedural && ProceduralMusic != null)
        {
            float startVol = ProceduralMusic.MasterVolume;
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                ProceduralMusic.MasterVolume = Mathf.Lerp(startVol, 0f, elapsed / halfDuration);
                yield return null;
            }
            ProceduralMusic.Panic();
        }
        else if (activeBackend == MusicBackend.Sample && SampleMusic != null)
        {
            SampleMusic.FadeMasterVolumeTo(0f, halfDuration);
            yield return new WaitForSeconds(halfDuration);
            SampleMusic.Panic();
        }

        // --- Swap backend ---
        activeBackend = incoming;

        // --- Bring incoming backend to the current state ---
        MusicSetGameState(_currentMusicState);
        MusicSetTension(_currentTension);

        // --- Fade IN the incoming backend ---
        if (incoming == MusicBackend.Procedural && ProceduralMusic != null)
        {
            float targetVol = ProceduralMusic.MasterVolume; // read default
            ProceduralMusic.MasterVolume = 0f;
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                ProceduralMusic.MasterVolume = Mathf.Lerp(0f, targetVol, elapsed / halfDuration);
                yield return null;
            }
        }
        else if (incoming == MusicBackend.Sample && SampleMusic != null)
        {
            float target = SampleMusic.MasterVolume;
            SampleMusic.MasterVolume = 0f;
            SampleMusic.FadeMasterVolumeTo(target, halfDuration);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Public API – Dungeon mode
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Enter dungeon mode: tension thresholds are read from the DungeonConfig SO.
    /// Called by DungeonAtmosphere on dungeon entry.
    /// </summary>
    public void EnterDungeonMode(DungeonConfig config)
    {
        _inDungeonMode = true;
        _activeDungeonConfig = config;
        _stateTimer = 0f;

        float tension = tensionCurve.Evaluate(ComputeTension());
        GameMusicState initial = config.EvaluateState(tension);
        _currentMusicState = initial;
        _pendingState = initial;
        MusicSetGameState(initial);
    }

    /// <summary>
    /// Exit dungeon mode: return to day/night threshold switching.
    /// Called by DungeonAtmosphere.OnDestroy().
    /// </summary>
    public void ExitDungeonMode()
    {
        _inDungeonMode = false;
        _activeDungeonConfig = null;
        _stateTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────────
    //  Public API – Manual state overrides
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Manually override the game music state (e.g. cutscenes, dialogue).
    /// Disables auto-switching until ReleaseStateOverride() is called.
    /// Works on whichever backend is currently active.
    /// </summary>
    public void OverrideGameState(GameMusicState state)
    {
        _manualStateOverride = true;
        _currentMusicState = state;
        MusicSetGameState(state);
    }

    /// <summary>
    /// Override the game music state and force a specific key (procedural backend only;
    /// stored for readback on the sample backend).
    /// Disables auto-switching until ReleaseStateOverride() is called.
    /// </summary>
    public void OverrideGameState(GameMusicState state, PitchClass root, MusicalMode mode)
    {
        _manualStateOverride = true;
        _currentMusicState = state;
        MusicSetGameStateAndModulation(state, root, mode);
    }

    /// <summary>
    /// Release the manual state override, returning to automatic tension-based switching.
    /// </summary>
    public void ReleaseStateOverride()
    {
        _manualStateOverride = false;
        _stateTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────────
    //  Public API – Misc
    // ─────────────────────────────────────────────────────────────

    /// <summary>Adjust the tension curve at runtime if needed.</summary>
    public void SetTensionCurve(AnimationCurve curve) => tensionCurve = curve;

    /// <summary>
    /// Switch the daytime default between Exploring and Exploring2.
    /// Automatically resets to Exploring when night falls.
    /// </summary>
    public void SetExploring2(bool active) => _useExploring2 = active;
}