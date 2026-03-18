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
        Exploring,      // Calm folk wandering through the woods
        Exploring2,     // More upbeat/confident exploration (found something good)
        Pressure,       // Something is wrong, tension building
        Combat,         // Fighting trolls, nøkken, creatures
        Spooky,         // Unsettling, things aren't right
        Horror,         // Maximum dread — the beast is near
        Night,          // Nighttime — calm to terrifying depending on tension
        Cozy            // By the fire — warm, safe, gentle
    }

    /// <summary>
    /// Configuration for a game music state.
    /// </summary>
    /// <summary>
    /// Per-instrument layer threshold: the instrument plays when LayerTension is between Min and Max.
    /// Set Min=0, Max=1 for "always on". Set Min=0, Max=0 for "always off".
    /// Set Min=0.5, Max=1 for "only at high tension". Set Min=0, Max=0.4 for "only at low tension".
    /// </summary>
    [Serializable]
    public struct LayerRange
    {
        public float Min;
        public float Max;

        public LayerRange(float min, float max) { Min = min; Max = max; }

        public bool IsActive(float layerTension)
        {
            return layerTension >= Min && (Max >= 1f || layerTension <= Max);
        }

        public static LayerRange Always => new LayerRange(0f, 1f);
        public static LayerRange Off => new LayerRange(0f, 0f);
        public static LayerRange From(float min) => new LayerRange(min, 1f);
        public static LayerRange Until(float max) => new LayerRange(0f, max);
        public static LayerRange Between(float min, float max) => new LayerRange(min, max);
    }

    [Serializable]
    public class MusicStateConfig
    {
        public GameMusicState State;
        public MusicalMode PreferredMode = MusicalMode.Major;
        public float TempoMin = 80f;
        public float TempoMax = 120f;
        public float BaseTensionOffset = 0f;
        public float MaxTension = 1f;
        public int RootOffset = 0;
        public bool EnableDistortion = false;
        public float DistortionIntensity = 0.6f;
        public bool EnableVinyl = false;           // Broken vinyl effect (wow, crackle, dropout)
        public float VinylIntensity = 0.5f;        // How broken the vinyl sounds (0-1)
        public bool AllowTritones = false;
        // Per-instrument style: each instrument can use a different pattern pool per state
        public MusicStyle MelodyStyle = MusicStyle.Folk;
        public MusicStyle BassStyle = MusicStyle.Folk;
        public MusicStyle PercussionStyle = MusicStyle.Folk;
        public MusicStyle StringsStyle = MusicStyle.Folk;
        public MusicStyle KanteleStyle = MusicStyle.Folk;

        /// <summary>
        /// Convenience: sets ALL instrument styles at once.
        /// You can still override individual styles after setting this.
        /// </summary>
        public MusicStyle MusicStyleSetting
        {
            set
            {
                MelodyStyle = value;
                BassStyle = value;
                PercussionStyle = value;
                StringsStyle = value;
                KanteleStyle = value;
            }
        }
        public float FMModIndexMultiplier = 1f;
        public bool BassRootOnly = false;

        // Per-instrument layer ranges: when each instrument plays based on LayerTension
        public LayerRange Pad = LayerRange.Always;
        public LayerRange Melody = LayerRange.From(0.15f);
        public LayerRange Bass = LayerRange.From(0.4f);
        public LayerRange Percussion = LayerRange.From(0.55f);
        public LayerRange Strings = LayerRange.From(0.3f);
        public LayerRange Kantele = LayerRange.Always;
        public LayerRange SubDrone = LayerRange.Off;    // Horror only
        public LayerRange Shriek = LayerRange.Off;      // Horror only

        public static MusicStateConfig GetDefault(GameMusicState state)
        {
            switch (state)
            {
                case GameMusicState.Exploring:
                    // Wandering the Scandinavian woods — folk kantele, gentle flute
                    // Dorian gives a folk-minor flavor without being too dark
                    return new MusicStateConfig
                    {
                        State = state,
                        PreferredMode = MusicalMode.Dorian,
                        TempoMin = 78,
                        TempoMax = 100,
                        BaseTensionOffset = -0.1f,
                        MaxTension = 0.55f,
                        RootOffset = 0,
                        Pad = LayerRange.Always,
                        Kantele = LayerRange.Always,
                        Melody = LayerRange.From(0.1f),
                        Strings = LayerRange.From(0.35f),
                        Bass = LayerRange.From(0.5f),
                        Percussion = LayerRange.Off,
                        MusicStyleSetting = MusicStyle.Folk
                    };

                case GameMusicState.Exploring2:
                    // Found safety, a cabin, or something good — warmer, more confident
                    // Major key, brighter, kantele more active
                    return new MusicStateConfig
                    {
                        State = state,
                        PreferredMode = MusicalMode.Major,
                        TempoMin = 90,
                        TempoMax = 112,
                        BaseTensionOffset = -0.15f,
                        MaxTension = 0.45f,
                        RootOffset = 7,
                        Pad = LayerRange.Always,
                        Kantele = LayerRange.Always,
                        Melody = LayerRange.Always,
                        Strings = LayerRange.Always,
                        Bass = LayerRange.From(0.2f),
                        Percussion = LayerRange.From(0.25f),
                        MusicStyleSetting = MusicStyle.Triumphant
                    };

                case GameMusicState.Pressure:
                    // Something is stalking, the old man senses danger
                    // Natural minor, instruments layer in with dread
                    return new MusicStateConfig
                    {
                        State = state,
                        PreferredMode = MusicalMode.NaturalMinor,
                        TempoMin = 85,
                        TempoMax = 110,
                        BaseTensionOffset = 0.15f,
                        MaxTension = 0.75f,
                        RootOffset = 0,
                        EnableDistortion = true,
                        Pad = LayerRange.Always,
                        Kantele = LayerRange.Until(0.5f),  // Kantele drops out as pressure mounts
                        Melody = LayerRange.From(0.15f),
                        Strings = LayerRange.From(0.25f),
                        Bass = LayerRange.From(0.35f),
                        Percussion = LayerRange.From(0.5f),
                        MusicStyleSetting = MusicStyle.Tense,
                        FMModIndexMultiplier = 1.5f
                    };

                case GameMusicState.Combat:
                    // Fighting creatures — atmospheric, heavy, not JRPG
                    // Harmonic minor for that dark Scandinavian edge
                    return new MusicStateConfig
                    {
                        State = state,
                        PreferredMode = MusicalMode.HarmonicMinor,
                        TempoMin = 85,
                        TempoMax = 110,
                        BaseTensionOffset = 0.2f,
                        MaxTension = 0.65f,
                        RootOffset = -3,
                        EnableDistortion = true,
                        Pad = LayerRange.Always,
                        Kantele = LayerRange.Off,
                        Melody = LayerRange.From(0.1f),
                        Strings = LayerRange.Always,
                        Bass = LayerRange.Always,
                        Percussion = LayerRange.From(0.2f),
                        MusicStyleSetting = MusicStyle.Tense,
                        FMModIndexMultiplier = 1.5f
                    };

                case GameMusicState.Spooky:
                    // Things aren't right — the nisse are watching, shadows move wrong
                    // Phrygian with that unsettling flat 2nd
                    return new MusicStateConfig
                    {
                        State = state,
                        PreferredMode = MusicalMode.Phrygian,
                        TempoMin = 55,
                        TempoMax = 75,
                        BaseTensionOffset = 0.2f,
                        MaxTension = 0.65f,
                        RootOffset = -6,
                        EnableDistortion = true,
                        AllowTritones = true,
                        Pad = LayerRange.Always,
                        Kantele = LayerRange.Until(0.4f),  // Drops out as fear builds
                        Melody = LayerRange.From(0.2f),
                        Strings = LayerRange.From(0.3f),
                        Bass = LayerRange.From(0.5f),
                        Percussion = LayerRange.Off,
                        MusicStyleSetting = MusicStyle.Sparse,
                        FMModIndexMultiplier = 2f
                    };

                case GameMusicState.Horror:
                    // ABSOLUTE TERROR — ambient dread, less is more
                    // Sounds like a broken record player in an abandoned cabin.
                    // Locrian: most unstable scale. Tritones everywhere.
                    // Almost nothing plays. When it does, it's warped and broken.
                    // Audio cuts out randomly. Crackle. Wow/flutter.
                    // The drone grinds beneath everything like something breathing.
                    return new MusicStateConfig
                    {
                        State = state,
                        PreferredMode = MusicalMode.Locrian,
                        TempoMin = 40 ,
                        TempoMax = 20,
                        BaseTensionOffset = 0.3f,
                        MaxTension = 1f,
                        RootOffset = -1,
                        EnableDistortion = true,
                        DistortionIntensity = 0.7f,
                        EnableVinyl = true,
                        VinylIntensity = 0.35f,
                        AllowTritones = true,
                        Pad = LayerRange.Off,
                        Kantele = LayerRange.Until(0.7f),
                        Melody = LayerRange.Off,
                        Strings = LayerRange.Off,
                        Bass = LayerRange.Off,
                        Percussion = LayerRange.Off,
                        SubDrone = LayerRange.Always,           // Constant low rumble
                        Shriek = LayerRange.From(0.35f),        // High shriek creeps in with tension
                        MusicStyleSetting = MusicStyle.Horror,
                        FMModIndexMultiplier = 2.5f,
                        BassRootOnly = true
                    };

                case GameMusicState.Night:
                    // Night in the forest — calm at low tension, terrifying at high
                    // Aeolian (natural minor) at root — familiar but uneasy
                    // Everything layers in gradually: peaceful campfire → wolves howling
                    return new MusicStateConfig
                    {
                        State = state,
                        PreferredMode = MusicalMode.Aeolian,
                        TempoMin = 55,
                        TempoMax = 85,
                        BaseTensionOffset = 0f,
                        MaxTension = 0.7f,
                        RootOffset = 0,
                        EnableDistortion = true,            // Distortion scales with tension
                        AllowTritones = false,              // No tritones at low tension, but...
                        Pad = LayerRange.Always,
                        Kantele = LayerRange.Until(0.45f),  // Campfire plucking, fades as night darkens
                        Melody = LayerRange.Between(0.1f, 0.6f), // Flute plays in the middle range only
                        Strings = LayerRange.From(0.3f),    // Strings creep in
                        Bass = LayerRange.From(0.4f),       // Deep rumble as things get scary
                        Percussion = LayerRange.From(0.6f), // Heartbeat drums at high tension
                        MusicStyleSetting = MusicStyle.Sparse,
                        FMModIndexMultiplier = 1.8f
                    };

                case GameMusicState.Cozy:
                    // Safe by the campfire — the warmest the game ever sounds.
                    // Major key, very slow, kantele and flute are the stars.
                    // Drone is gentle, strings are soft and warm. No percussion.
                    // No bass — the low end is just the drone's warmth.
                    // This is the reward for surviving. Hot cocoa for your ears.
                    return new MusicStateConfig
                    {
                        State = state,
                        PreferredMode = MusicalMode.Major,
                        TempoMin = 62, TempoMax = 78,
                        BaseTensionOffset = -0.2f, MaxTension = 0.3f, RootOffset = 5,
                        EnableDistortion = false,
                        EnableVinyl = false,
                        AllowTritones = false,
                        Pad = LayerRange.Always,
                        Kantele = LayerRange.Always,
                        Melody = LayerRange.From(0.05f),    // Flute almost always plays
                        Strings = LayerRange.From(0.15f),   // Soft strings join early
                        Bass = LayerRange.Off,               // No bass — just warmth
                        Percussion = LayerRange.Off,         // No rhythm — just peace
                        SubDrone = LayerRange.Off,
                        Shriek = LayerRange.Off,
                        MusicStyleSetting = MusicStyle.Folk,
                        FMModIndexMultiplier = 0.8f,         // Softer timbres
                        BassRootOnly = false
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
        public GameMusicState CurrentState = GameMusicState.Exploring;

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
        private VoiceManager _subDroneVoices;
        private VoiceManager _shriekVoices;

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
            _subDroneVoices = _mixer.AddInstrument(InstrumentPreset.SubDrone); // 8
            _shriekVoices = _mixer.AddInstrument(InstrumentPreset.ShriekString); // 9

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

            // Distortion scales with tension when enabled for the current state
            if (config.EnableDistortion)
                _mixer.DistortionAmount = Mathf.Lerp(0f, config.DistortionIntensity, effectiveTension);
            else
                _mixer.DistortionAmount = 0f;

            // Broken vinyl effect scales with tension
            _mixer.VinylEnabled = config.EnableVinyl;
            if (config.EnableVinyl)
                _mixer.VinylAmount = Mathf.Lerp(0f, config.VinylIntensity, effectiveTension);
            else
                _mixer.VinylAmount = 0f;

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
        /// Simple enable/disable for layers (convenience method).
        /// Pass true = Always, false = Off.
        /// </summary>
        public void SetLayerEnabled(bool pad, bool melody, bool bass, bool percussion,
            bool strings = true, bool kantele = true)
        {
            _composer.PadRange = pad ? LayerRange.Always : LayerRange.Off;
            _composer.MelodyRange = melody ? LayerRange.Always : LayerRange.Off;
            _composer.BassRange = bass ? LayerRange.Always : LayerRange.Off;
            _composer.PercussionRange = percussion ? LayerRange.Always : LayerRange.Off;
            _composer.StringsRange = strings ? LayerRange.Always : LayerRange.Off;
            _composer.KanteleRange = kantele ? LayerRange.Always : LayerRange.Off;
        }

        /// <summary>
        /// Advanced: set per-instrument layer ranges directly.
        /// </summary>
        public void SetLayerRanges(LayerRange pad, LayerRange melody, LayerRange bass,
            LayerRange percussion, LayerRange strings, LayerRange kantele)
        {
            _composer.PadRange = pad;
            _composer.MelodyRange = melody;
            _composer.BassRange = bass;
            _composer.PercussionRange = percussion;
            _composer.StringsRange = strings;
            _composer.KanteleRange = kantele;
        }

        /// <summary>
        /// Toggle distortion on/off at runtime.
        /// When enabled, distortion amount scales with tension automatically.
        /// </summary>
        public void SetDistortion(bool enabled)
        {
            _mixer.DistortionEnabled = enabled;
            if (!enabled) _mixer.DistortionAmount = 0f;
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
            // Pass layer ranges to composition engine
            _composer.PadRange = config.Pad;
            _composer.MelodyRange = config.Melody;
            _composer.BassRange = config.Bass;
            _composer.PercussionRange = config.Percussion;
            _composer.StringsRange = config.Strings;
            _composer.KanteleRange = config.Kantele;
            _composer.SubDroneRange = config.SubDrone;
            _composer.ShriekRange = config.Shriek;
            _composer.AllowTritones = config.AllowTritones;
            _composer.BassRootOnly = config.BassRootOnly;

            // Per-instrument styles
            _composer.Melody.Style = config.MelodyStyle;
            _composer.Bass.Style = config.BassStyle;
            _composer.Rhythm.Style = config.PercussionStyle;
            _composer.CurrentStringStyle = config.StringsStyle;
            _composer.CurrentKanteleStyle = config.KanteleStyle;

            // Distortion
            _mixer.DistortionEnabled = config.EnableDistortion;
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