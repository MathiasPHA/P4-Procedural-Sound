using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using ProceduralMusic.Core;
using ProceduralMusic.Synthesis;
using ProceduralMusic.Composition;

namespace ProceduralMusic.Bridge
{
    /// <summary>
    /// Game states that affect musical mood and structure.
    /// Each state maps to a set of musical parameters (key, tempo range, 
    /// instrument enables, base tension offset).
    /// </summary>
    public enum GameMusicState
    {
        Explore,        // Calm, ambient, major key, sparse
        Dialogue,       // Quiet, simple pads only
        Tension,        // Building, minor key, increasing density
        Combat,         // Intense, fast, full instrumentation
        Victory,        // Triumphant, major, fanfare-like
        Mystery,        // Dorian/aeolian, sparse, high FM mod
        Ambient,        // Very sparse, just pads and occasional melody
        Spooky          // Phrygian, sparse, unpredictable, unsettling
    }

    /// <summary>
    /// Configuration for a game music state.
    /// </summary>
    [Serializable]
    public class MusicStateConfig
    {
        public GameMusicState State;
        public MusicalMode PreferredMode = MusicalMode.Major;
        public float TempoMin = 80f;
        public float TempoMax = 120f;
        public float BaseTensionOffset = 0f;
        public float MaxTension = 1f;
        public int RootOffset = 0;              // Semitones from starting key (0 = same key, 5 = up a fourth, 7 = up a fifth)
        public bool Pads = true;
        public bool Melody = true;
        public bool Bass = true;
        public bool Percussion = true;
        public bool Strings = true;
        public bool Kantele = true;
        public float FMModIndexMultiplier = 1f;

        public static MusicStateConfig GetDefault(GameMusicState state)
        {
            switch (state)
            {
                case GameMusicState.Explore:
                    // Home key — the "default" key center
                    return new MusicStateConfig
                    {
                        State = state, PreferredMode = MusicalMode.Major,
                        TempoMin = 85, TempoMax = 105,
                        BaseTensionOffset = -0.1f, MaxTension = 0.6f, RootOffset = 0,
                        Pads = true, Melody = true, Bass = true, Percussion = false,
                        Strings = true, Kantele = true
                    };

                case GameMusicState.Dialogue:
                    // Subdominant (up a 4th) — warm, related, slightly different color
                    return new MusicStateConfig
                    {
                        State = state, PreferredMode = MusicalMode.Major,
                        TempoMin = 70, TempoMax = 90,
                        BaseTensionOffset = -0.2f, MaxTension = 0.3f, RootOffset = 5,
                        Pads = true, Melody = false, Bass = false, Percussion = false,
                        Strings = true, Kantele = true
                    };

                case GameMusicState.Tension:
                    // Relative minor (same key center, different mode) — seamless transition
                    return new MusicStateConfig
                    {
                        State = state, PreferredMode = MusicalMode.NaturalMinor,
                        TempoMin = 90, TempoMax = 115,
                        BaseTensionOffset = 0.15f, MaxTension = 0.75f, RootOffset = 0,
                        Pads = true, Melody = true, Bass = true, Percussion = true,
                        Strings = true, Kantele = true, FMModIndexMultiplier = 1.5f
                    };

                case GameMusicState.Combat:
                    // Down a minor 3rd — dramatic shift, common in film scores
                    return new MusicStateConfig
                    {
                        State = state, PreferredMode = MusicalMode.HarmonicMinor,
                        TempoMin = 85, TempoMax = 110,
                        BaseTensionOffset = 0.2f, MaxTension = 0.65f, RootOffset = -3,
                        Pads = true, Melody = true, Bass = true, Percussion = true,
                        Strings = true, Kantele = false, FMModIndexMultiplier = 1.5f
                    };

                case GameMusicState.Victory:
                    // Dominant (up a 5th) — bright, triumphant, strong relationship to home
                    return new MusicStateConfig
                    {
                        State = state, PreferredMode = MusicalMode.Major,
                        TempoMin = 105, TempoMax = 120,
                        BaseTensionOffset = -0.15f, MaxTension = 0.4f, RootOffset = 7,
                        Pads = true, Melody = true, Bass = true, Percussion = true,
                        Strings = true, Kantele = true
                    };

                case GameMusicState.Mystery:
                    // Up a whole step — slightly distant, unusual
                    return new MusicStateConfig
                    {
                        State = state, PreferredMode = MusicalMode.Dorian,
                        TempoMin = 75, TempoMax = 95,
                        BaseTensionOffset = 0.15f, MaxTension = 0.55f, RootOffset = 2,
                        Pads = false, Melody = true, Bass = false, Percussion = false,
                        Strings = true, Kantele = true, FMModIndexMultiplier = 1.8f
                    };

                case GameMusicState.Ambient:
                    // Subdominant (up a 4th) — gentle, floating
                    return new MusicStateConfig
                    {
                        State = state, PreferredMode = MusicalMode.Major,
                        TempoMin = 60, TempoMax = 80,
                        BaseTensionOffset = -0.2f, MaxTension = 0.3f, RootOffset = 5,
                        Pads = true, Melody = false, Bass = false, Percussion = false,
                        Strings = true, Kantele = true
                    };

                case GameMusicState.Spooky:
                    // Down a tritone — maximum harmonic distance, unsettling
                    return new MusicStateConfig
                    {
                        State = state, PreferredMode = MusicalMode.Phrygian,
                        TempoMin = 55, TempoMax = 75,
                        BaseTensionOffset = 0.25f, MaxTension = 0.7f, RootOffset = -6,
                        Pads = true, Melody = true, Bass = false, Percussion = false,
                        Strings = true, Kantele = true, FMModIndexMultiplier = 2f
                    };

                default:
                    return new MusicStateConfig { State = state };
            }
        }
    }

    /// <summary>
    /// Main MonoBehaviour that bridges the procedural music system with Unity.
    /// 
    /// Attach this to a GameObject with an AudioSource.
    /// Control music via SetTension() and SetGameState() from your game logic.
    /// 
    /// SETUP:
    /// 1. Create an empty GameObject
    /// 2. Add an AudioSource component (no clip needed)
    /// 3. Add this component
    /// 4. Call SetTension() and SetGameState() from your game scripts
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralMusicController : MonoBehaviour
    {
        [Header("Musical Settings")]
        [Tooltip("Root note of the starting key")]
        public PitchClass StartingKey = PitchClass.C;

        [Tooltip("Starting scale mode")]
        public MusicalMode StartingMode = MusicalMode.Major;

        [Tooltip("Beats per minute")]
        [Range(60, 200)]
        public float Tempo = 100f;

        [Tooltip("How many beats before a chord change")]
        [Range(2, 8)]
        public int BeatsPerChord = 4;

        [Header("Game Control")]
        [Tooltip("Continuous tension: 0 = peaceful, 1 = intense")]
        [Range(0f, 1f)]
        public float Tension = 0.3f;

        [Tooltip("Current game music state")]
        public GameMusicState CurrentState = GameMusicState.Explore;

        [Header("Audio")]
        [Range(0f, 1f)]
        public float MasterVolume = 0.7f;

        [Header("Debug")]
        public bool ShowDebugInfo = false;
        public string CurrentChordName = "";
        public string CurrentKeyName = "";

        // Internal systems
        private MasterMixer _mixer;
        private CompositionEngine _composer;
        private Dictionary<GameMusicState, MusicStateConfig> _stateConfigs;

        // Instrument voice managers (indexed to match CompositionEngine indices)
        private VoiceManager _padVoices;
        private VoiceManager _leadVoices;
        private VoiceManager _bassVoices;
        private VoiceManager _kickVoices;
        private VoiceManager _snareVoices;
        private VoiceManager _hihatVoices;
        private VoiceManager _stringsVoices;
        private VoiceManager _kanteleVoices;

        private float _sampleRate;
        private bool _initialized;

        private GameMusicState _previousState;
        private float _stateTransitionTimer;

        void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            Initialize();
        }

        void Initialize()
        {
            // Initialize mixer
            _mixer = new MasterMixer(_sampleRate);
            _mixer.MasterVolume = MasterVolume;

            // Register instruments in order (indices must match CompositionEngine)
            _padVoices = _mixer.AddInstrument(InstrumentPreset.Pad);           // 0
            _leadVoices = _mixer.AddInstrument(InstrumentPreset.LegatoMelody); // 1 — legato lead instead of bippy FM
            _bassVoices = _mixer.AddInstrument(InstrumentPreset.Bass);         // 2
            _kickVoices = _mixer.AddInstrument(InstrumentPreset.Kick);         // 3
            _snareVoices = _mixer.AddInstrument(InstrumentPreset.Snare);       // 4
            _hihatVoices = _mixer.AddInstrument(InstrumentPreset.HiHat);       // 5
            _stringsVoices = _mixer.AddInstrument(InstrumentPreset.Strings);   // 6
            _kanteleVoices = _mixer.AddInstrument(InstrumentPreset.Kantele);   // 7

            // Initialize composition engine
            Key startKey = new Key(StartingKey, StartingMode);
            _composer = new CompositionEngine(startKey);
            _composer.Tempo = Tempo;
            _composer.BeatsPerChord = BeatsPerChord;

            // Set up state configurations
            _stateConfigs = new Dictionary<GameMusicState, MusicStateConfig>();
            foreach (GameMusicState state in Enum.GetValues(typeof(GameMusicState)))
            {
                _stateConfigs[state] = MusicStateConfig.GetDefault(state);
            }

            _previousState = CurrentState;
            ApplyStateConfig(_stateConfigs[CurrentState]);

            _initialized = true;
        }

        void Update()
        {
            if (!_initialized) return;

            // Detect state changes
            if (CurrentState != _previousState)
            {
                OnStateChanged(CurrentState);
                _previousState = CurrentState;
            }

            // Update composer with current tension
            var config = _stateConfigs[CurrentState];
            float effectiveTension = Mathf.Clamp01(Tension + config.BaseTensionOffset);

            // MaxTension only clamps HARMONIC tension (chord choices via TPS).
            // Layer entry uses the unclamped tension so instruments aren't cut off.
            _composer.TensionTarget = Mathf.Min(effectiveTension, config.MaxTension);
            _composer.LayerTension = effectiveTension;
            _composer.Tempo = Mathf.Lerp(config.TempoMin, config.TempoMax, Tension);
            _mixer.MasterVolume = MasterVolume;

            // Advance composition engine — it handles all scheduling internally
            _composer.Update(Time.deltaTime, out var noteOns, out var noteOffs);

            // Trigger note-ons
            var instruments = _mixer.GetInstruments();
            foreach (var evt in noteOns)
            {
                if (evt.InstrumentIndex < instruments.Count)
                    instruments[evt.InstrumentIndex].NoteOn(evt.MidiNote, evt.Velocity);
            }

            // Trigger note-offs
            foreach (var (midiNote, instIndex) in noteOffs)
            {
                if (instIndex < instruments.Count)
                    instruments[instIndex].NoteOff(midiNote);
            }

            // Debug
            if (ShowDebugInfo)
            {
                CurrentChordName = _composer.Chords.CurrentChord.ToString();
                CurrentKeyName = _composer.CurrentKey.ToString();
            }
        }

        /// <summary>
        /// Unity audio thread callback — fills the audio buffer.
        /// This runs on the audio thread, NOT the main thread.
        /// </summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_initialized || _mixer == null) return;

            _dspStopwatch.Restart();
            _mixer.FillBuffer(data, channels);
            _dspStopwatch.Stop();

            // Track DSP timing
            double dspMs = _dspStopwatch.Elapsed.TotalMilliseconds;
            _dspTimeAccum += dspMs;
            _dspCallCount++;

            // Calculate budget: how much time the audio thread HAS for this buffer
            double bufferDurationMs = (double)(data.Length / channels) / _sampleRate * 1000.0;
            _dspBudgetMs = bufferDurationMs;

            // Update averages periodically (not every call — too noisy)
            if (_dspCallCount >= 20)
            {
                _dspAvgMs = _dspTimeAccum / _dspCallCount;
                _dspPeakMs = System.Math.Max(_dspPeakMs * 0.95, dspMs); // Slowly decay peak
                _dspCpuPercent = (_dspAvgMs / bufferDurationMs) * 100.0;
                _dspTimeAccum = 0;
                _dspCallCount = 0;
            }

            // Always track instantaneous peak
            if (dspMs > _dspPeakMs) _dspPeakMs = dspMs;
        }

        // Profiling data (public so test GUI can read it)
        private System.Diagnostics.Stopwatch _dspStopwatch = new System.Diagnostics.Stopwatch();
        private double _dspTimeAccum;
        private int _dspCallCount;
        private double _dspBudgetMs;

        /// <summary>Average DSP time per audio buffer (milliseconds).</summary>
        public double DspAvgMs => _dspAvgMs;
        private double _dspAvgMs;

        /// <summary>Peak DSP time (milliseconds, slowly decaying).</summary>
        public double DspPeakMs => _dspPeakMs;
        private double _dspPeakMs;

        /// <summary>DSP CPU usage as percentage of audio budget (100% = buffer underrun).</summary>
        public double DspCpuPercent => _dspCpuPercent;
        private double _dspCpuPercent;

        /// <summary>Audio buffer duration in ms (the budget ceiling).</summary>
        public double DspBudgetMs => _dspBudgetMs;

        // ─────────────────────────────────────────────
        //  PUBLIC API - Call these from your game code
        // ─────────────────────────────────────────────

        /// <summary>
        /// Set the continuous tension parameter (0 = calm, 1 = intense).
        /// Affects chord choices, rhythmic density, timbral brightness, and tempo.
        /// </summary>
        public void SetTension(float tension)
        {
            Tension = Mathf.Clamp01(tension);
        }

        /// <summary>
        /// Switch to a new game music state.
        /// This triggers a musical transition (key change, instrument changes).
        /// </summary>
        public void SetGameState(GameMusicState state)
        {
            CurrentState = state;
        }

        /// <summary>
        /// Force a key change (modulation). Useful for dramatic moments.
        /// </summary>
        public void ForceModulation(PitchClass newRoot, MusicalMode newMode)
        {
            _composer.ChangeKey(new Key(newRoot, newMode));
        }

        /// <summary>
        /// Change just the key root, keeping the current mode.
        /// Also updates StartingKey so future state changes calculate offsets from the new root.
        /// </summary>
        public void SetKey(PitchClass newRoot)
        {
            StartingKey = newRoot;
            _composer.ChangeKey(new Key(newRoot, _composer.CurrentKey.Mode));
        }

        /// <summary>
        /// Get the current tension of the most recent chord (from TPS analysis).
        /// Useful for syncing visual effects to musical tension.
        /// </summary>
        public float GetCurrentMusicalTension()
        {
            return TonalPitchSpace.GetTension(_composer.Chords.CurrentChord, _composer.CurrentKey);
        }

        /// <summary>
        /// Get the current chord being played.
        /// </summary>
        public Chord GetCurrentChord()
        {
            return _composer.Chords.CurrentChord;
        }

        /// <summary>
        /// Immediately stop all sound.
        /// </summary>
        public void Panic()
        {
            foreach (var inst in _mixer.GetInstruments())
                inst.Panic();
        }

        /// <summary>
        /// Enable/disable individual composition layers at runtime.
        /// </summary>
        public void SetLayerEnabled(bool pad, bool melody, bool bass, bool percussion,
            bool strings = true, bool kantele = true)
        {
            _composer.EnablePad = pad;
            _composer.EnableMelody = melody;
            _composer.EnableBass = bass;
            _composer.EnablePercussion = percussion;
            _composer.EnableStrings = strings;
            _composer.EnableKantele = kantele;

            if (!pad) _padVoices.AllNotesOff();
            if (!melody) _leadVoices.AllNotesOff();
            if (!bass) _bassVoices.AllNotesOff();
            if (!strings) _stringsVoices.AllNotesOff();
            if (!kantele) _kanteleVoices.AllNotesOff();
            if (!percussion)
            {
                _kickVoices.AllNotesOff();
                _snareVoices.AllNotesOff();
                _hihatVoices.AllNotesOff();
            }
        }

        /// <summary>
        /// Get the list of voice managers (instruments) for direct synth access.
        /// Index order: 0=Pad, 1=Lead, 2=Bass, 3=Kick, 4=Snare, 5=HiHat
        /// </summary>
        public List<VoiceManager> GetInstruments()
        {
            return _mixer.GetInstruments();
        }

        /// <summary>
        /// Get the composition engine for advanced control.
        /// </summary>
        public CompositionEngine GetComposer()
        {
            return _composer;
        }

        // ─────────────────────────────────────────────
        //  INTERNAL
        // ─────────────────────────────────────────────

        private void OnStateChanged(GameMusicState newState)
        {
            var config = _stateConfigs[newState];

            // Calculate the target key root from starting key + state's offset
            int targetRoot = (((int)StartingKey + config.RootOffset) % 12 + 12) % 12;
            PitchClass targetPC = (PitchClass)targetRoot;

            // Modulate if key root or mode differs from current
            if (targetPC != _composer.CurrentKey.Root || config.PreferredMode != _composer.CurrentKey.Mode)
            {
                _composer.ChangeKey(new Key(targetPC, config.PreferredMode));
            }

            ApplyStateConfig(config);
        }

        private void ApplyStateConfig(MusicStateConfig config)
        {
            _composer.EnablePad = config.Pads;
            _composer.EnableMelody = config.Melody;
            _composer.EnableBass = config.Bass;
            _composer.EnablePercussion = config.Percussion;
            _composer.EnableStrings = config.Strings;
            _composer.EnableKantele = config.Kantele;

            if (!config.Pads) _padVoices.AllNotesOff();
            if (!config.Melody) _leadVoices.AllNotesOff();
            if (!config.Bass) _bassVoices.AllNotesOff();
            if (!config.Strings) _stringsVoices.AllNotesOff();
            if (!config.Kantele) _kanteleVoices.AllNotesOff();
            if (!config.Percussion)
            {
                _kickVoices.AllNotesOff();
                _snareVoices.AllNotesOff();
                _hihatVoices.AllNotesOff();
            }
        }

        void OnGUI()
        {
            if (!ShowDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Key: {CurrentKeyName}");
            GUILayout.Label($"Chord: {CurrentChordName}");
            GUILayout.Label($"Tension: {Tension:F2} (effective: {_composer.TensionTarget:F2})");
            GUILayout.Label($"Tempo: {_composer.Tempo:F0} BPM");
            GUILayout.Label($"State: {CurrentState}");
            GUILayout.Label($"Beat: {_composer.CurrentBeat:F1}");
            GUILayout.Label($"Musical Tension: {GetCurrentMusicalTension():F2}");
            GUILayout.EndArea();
        }
    }
}
