using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using ProceduralMusic.Bridge;
using ProceduralMusic.Core;

/// <summary>
/// Sample-based music controller that mirrors the ProceduralMusicController public API.
/// Use one pre-recorded AudioClip per GameMusicState (8 clips total).
/// Transitions between states via a crossfade using two AudioSources (ping-pong).
///
/// Drop-in swap: ComfortMusicBridge can point to this instead of ProceduralMusicController
/// — all the same method names, same enum, same tension-based auto-switching.
/// </summary>
public class SampleMusicController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Singleton
    // ─────────────────────────────────────────────────────────────

    public static SampleMusicController Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    //  Inspector – Clips
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class StateClip
    {
        public GameMusicState State;

        [Tooltip("Pre-recorded loop for this state. Record from the procedural system.")]
        public AudioClip Clip;

        [Tooltip("Volume scale for this clip (0–1). Useful if recordings have different loudness.")]
        [Range(0f, 1f)]
        public float VolumeScale = 1f;
    }

    [Header("Clips  (one per state)")]
    [Tooltip("Assign one AudioClip for each GameMusicState. Missing states play silence.")]
    [SerializeField]
    private StateClip[] stateClips = new StateClip[]
    {
        new StateClip { State = GameMusicState.Exploring,  VolumeScale = 1f },
        new StateClip { State = GameMusicState.Exploring2, VolumeScale = 1f },
        new StateClip { State = GameMusicState.Pressure,   VolumeScale = 1f },
        new StateClip { State = GameMusicState.Combat,     VolumeScale = 1f },
        new StateClip { State = GameMusicState.Spooky,     VolumeScale = 1f },
        new StateClip { State = GameMusicState.Horror,     VolumeScale = 1f },
        new StateClip { State = GameMusicState.Night,      VolumeScale = 1f },
        new StateClip { State = GameMusicState.Cozy,       VolumeScale = 1f },
    };

    // ─────────────────────────────────────────────────────────────
    //  Inspector – Playback
    // ─────────────────────────────────────────────────────────────

    [Header("Playback")]
    [Tooltip("Overall output volume (mirrors ProceduralMusicController.MasterVolume)")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 0.7f;

    [Tooltip("How long (seconds) the crossfade between states takes")]
    [Range(0.1f, 10f)]
    [SerializeField] private float crossfadeDuration = 2f;

    [Tooltip("If true, new state clips start from the same playback position as the outgoing clip. " +
             "Useful for recordings that all share the same loop length.")]
    [SerializeField] private bool preservePlaybackPosition = false;

    // ─────────────────────────────────────────────────────────────
    //  Inspector – Debug
    // ─────────────────────────────────────────────────────────────

    [Header("Audio Mixer")]
    [Tooltip("Assign the Music group from your Audio Mixer. Both AudioSources will route through it.")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;

    [Header("Debug")]
    public bool ShowDebugInfo = false;

    // ─────────────────────────────────────────────────────────────
    //  Mirror properties (ComfortMusicBridge / MusicTestController read these)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Current game music state.</summary>
    public GameMusicState CurrentState { get; private set; } = GameMusicState.Exploring;

    /// <summary>Current tension value (set by ComfortMusicBridge or manually).</summary>
    public float Tension { get; private set; } = 0.3f;

    /// <summary>Mirrors the ProceduralMusicController property name used in MusicTestController.</summary>
    public PitchClass StartingKey { get; private set; } = PitchClass.C;

    /// <summary>Master volume, readable by other systems.</summary>
    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            ApplyMasterVolume();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Private – Audio ping-pong
    // ─────────────────────────────────────────────────────────────

    private AudioSource[] _sources = new AudioSource[2];
    private int _activeSource = 0;          // index into _sources that is currently audible
    private Coroutine _crossfadeRoutine;
    private float _activeVolumeScale = 1f;  // volume scale of the currently playing clip
    private bool _hasPlayedOnce = false;    // ensures first SetGameState always triggers playback

    // ─────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create two AudioSources for crossfading
        for (int i = 0; i < 2; i++)
        {
            _sources[i] = gameObject.AddComponent<AudioSource>();
            _sources[i].loop = true;
            _sources[i].playOnAwake = false;
            _sources[i].volume = 0f;
            _sources[i].spatialBlend = 0f; // 2D
            _sources[i].outputAudioMixerGroup = musicMixerGroup;
        }
    }

    private void Start()
    {
        // Defer one frame so ComfortMusicBridge has time to call SetGameState
        // with the correct initial state before we start playing
        StartCoroutine(DelayedStart());
    }

    private System.Collections.IEnumerator DelayedStart()
    {
        yield return null; // wait one frame
        PlayState(CurrentState, instant: true);
    }

    private void OnGUI()
    {
        if (!ShowDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 200, 320, 140));
        GUILayout.Box("── SampleMusicController ──\n" +
                      $"State   : {CurrentState}\n" +
                      $"Tension : {Tension:F2}\n" +
                      $"Volume  : {masterVolume:F2}\n" +
                      $"Crossfade: {crossfadeDuration:F1}s");
        GUILayout.EndArea();
    }

    // ─────────────────────────────────────────────────────────────
    //  Public API  (mirrors ProceduralMusicController)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Set the current tension (0–1). Stored for readback; does not change the
    /// playing clip — state switching is handled by ComfortMusicBridge.
    /// </summary>
    public void SetTension(float tension)
    {
        Tension = Mathf.Clamp01(tension);
    }

    /// <summary>
    /// Switch to a new game music state, crossfading from the current one.
    /// </summary>
    public void SetGameState(GameMusicState state)
    {
        bool isInitial = !_hasPlayedOnce;
        if (state == CurrentState && !isInitial) return;
        CurrentState = state;
        _hasPlayedOnce = true;
        PlayState(state, instant: isInitial);
    }

    /// <summary>
    /// No-op stub — sample playback has no key concept. Stored for readback only.
    /// Exists so MusicTestController / ComfortMusicBridge compile unchanged.
    /// </summary>
    public void ForceModulation(PitchClass newRoot, MusicalMode newMode)
    {
        StartingKey = newRoot;
        // Nothing to modulate in sample playback
    }

    /// <summary>No-op stub — key root only.</summary>
    public void SetKey(PitchClass newRoot)
    {
        StartingKey = newRoot;
    }

    /// <summary>Returns current tension (mirrors ProceduralMusicController.GetCurrentMusicalTension).</summary>
    public float GetCurrentMusicalTension() => Tension;

    /// <summary>Immediately silence all audio.</summary>
    public void Panic()
    {
        if (_crossfadeRoutine != null)
            StopCoroutine(_crossfadeRoutine);

        foreach (var src in _sources)
        {
            src.volume = 0f;
            src.Stop();
        }
    }

    /// <summary>
    /// Fade master volume to a target over a given duration.
    /// Useful for scene transitions (mirrors how SceneTransition fades the procedural system).
    /// </summary>
    public void FadeMasterVolumeTo(float targetVolume, float duration)
    {
        StartCoroutine(FadeVolume(targetVolume, duration));
    }

    // ─────────────────────────────────────────────────────────────
    //  Stubs for API parity (no-ops that let shared code compile)
    // ─────────────────────────────────────────────────────────────

    /// <summary>No-op — layer enables have no meaning for sample playback.</summary>
    public void SetLayerEnabled(bool pad, bool melody, bool bass, bool percussion,
                                bool strings, bool kantele)
    { }

    /// <summary>No-op.</summary>
    public void SetDistortion(bool enabled) { }

    // ─────────────────────────────────────────────────────────────
    //  Internal – playback helpers
    // ─────────────────────────────────────────────────────────────

    private void PlayState(GameMusicState state, bool instant)
    {
        AudioClip clip = GetClip(state, out float volumeScale);

        if (instant)
        {
            // Just start playing on source 0, full volume
            _sources[0].clip = clip;
            _sources[0].volume = clip != null ? masterVolume * volumeScale : 0f;
            _sources[1].volume = 0f;
            _activeVolumeScale = volumeScale;
            _activeSource = 0;

            if (clip != null)
                _sources[0].Play();

            return;
        }

        // Crossfade: prepare the inactive source, then blend
        if (_crossfadeRoutine != null)
            StopCoroutine(_crossfadeRoutine);

        _crossfadeRoutine = StartCoroutine(Crossfade(clip, volumeScale));
    }

    private IEnumerator Crossfade(AudioClip incoming, float incomingVolumeScale)
    {
        int outIdx = _activeSource;
        int inIdx = 1 - _activeSource;

        AudioSource outSrc = _sources[outIdx];
        AudioSource inSrc = _sources[inIdx];

        // Set up incoming source
        inSrc.clip = incoming;
        if (preservePlaybackPosition && incoming != null && outSrc.isPlaying)
        {
            // Try to sync to the same position, clamped to clip length
            inSrc.timeSamples = Mathf.Min(outSrc.timeSamples, incoming.samples - 1);
        }

        float targetInVolume = incoming != null ? masterVolume * incomingVolumeScale : 0f;
        float startOutVolume = outSrc.volume;

        if (incoming != null)
        {
            inSrc.volume = 0f;
            inSrc.Play();
        }

        // Blend over crossfadeDuration
        float elapsed = 0f;
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / crossfadeDuration);
            float smoothT = t * t * (3f - 2f * t); // smoothstep

            inSrc.volume = Mathf.Lerp(0f, targetInVolume, smoothT);
            outSrc.volume = Mathf.Lerp(startOutVolume, 0f, smoothT);

            yield return null;
        }

        // Finalise
        outSrc.volume = 0f;
        outSrc.Stop();
        inSrc.volume = targetInVolume;

        _activeSource = inIdx;
        _activeVolumeScale = incomingVolumeScale;
        _crossfadeRoutine = null;
    }

    private AudioClip GetClip(GameMusicState state, out float volumeScale)
    {
        foreach (var entry in stateClips)
        {
            if (entry.State == state)
            {
                volumeScale = entry.VolumeScale;
                return entry.Clip; // null is intentional (silent state)
            }
        }

        if (ShowDebugInfo)
            Debug.LogWarning($"[SampleMusicController] No entry for state {state}");

        volumeScale = 1f;
        return null;
    }

    private void ApplyMasterVolume()
    {
        // Rescale the active source's volume without interrupting the crossfade
        _sources[_activeSource].volume = masterVolume * _activeVolumeScale;
    }

    private IEnumerator FadeVolume(float target, float duration)
    {
        float start = masterVolume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            masterVolume = Mathf.Lerp(start, target, elapsed / duration);
            ApplyMasterVolume();
            yield return null;
        }

        masterVolume = target;
        ApplyMasterVolume();
    }
}