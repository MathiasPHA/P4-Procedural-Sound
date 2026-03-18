using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralMusic.Core;
using ProceduralMusic.Synthesis;
using ProceduralMusic.Composition;
using ProceduralMusic.Bridge;

namespace ProceduralMusic.Examples
{
    /// <summary>
    /// Comprehensive test harness with a full IMGUI interface.
    /// Tests every feature of the procedural music system:
    ///   - Tension slider (continuous control)
    ///   - Game state buttons
    ///   - Key root selection (all 12 pitch classes)
    ///   - Mode selection (all 6 modes)
    ///   - Individual layer toggles (pad, melody, bass, percussion)
    ///   - Tempo override
    ///   - Beats-per-chord control
    ///   - Master volume
    ///   - Isolated synth tests (trigger single notes per instrument)
    ///   - TPS visualizer (chord distances, tension values, attraction)
    ///   - Automated scenarios (explore→combat→victory sequence)
    ///   - Live audio waveform scope
    ///
    /// SETUP: Add this to ANY GameObject in a scene that has a
    ///        ProceduralMusicController. It auto-finds the controller.
    ///        No other setup needed.
    /// </summary>
    public class FullSystemTest : MonoBehaviour
    {
        // ─── References ──────────────────────────────────
        private ProceduralMusicController _music;
        private bool _ready;

        // ─── GUI state ───────────────────────────────────
        private int _selectedTab;
        private readonly string[] _tabNames = {
            "Controls", "Synth Test", "TPS Visualizer", "Scenarios", "Waveform"
        };

        // Controls tab
        private float _tension = 0.3f;
        private float _masterVol = 0.7f;
        private float _tempoOverride = 100f;
        private bool _useTempoOverride;
        private int _beatsPerChord = 4;
        private bool _enablePad = true;
        private bool _enableMelody = true;
        private bool _enableBass = true;
        private bool _enablePerc = true;
        private bool _enableStrings = true;
        private bool _enableKantele = true;
        private int _selectedKeyRoot;
        private int _selectedMode;
        private int _selectedState;
        private readonly string[] _keyNames = {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
        };
        private readonly string[] _modeNames = {
            "Major", "Natural Minor", "Harmonic Minor", "Dorian", "Mixolydian", "Aeolian",
            "Phrygian", "Locrian"
        };
        private readonly string[] _stateNames;

        // Synth test tab
        private int _testOctave = 4;
        private int _testInstrument;
        private readonly string[] _instrumentNames = {
            "HurdyGurdy (Drone)", "Flute (Legato)", "Bass (Sub)", "Log Drum", "Frame Drum", "Brush",
            "Strings", "Kantele", "SubDrone (Horror)", "ShriekString (Horror)"
        };
        private float _testVelocity = 0.8f;
        private int _lastTriggeredNote = -1;

        // TPS visualizer tab
        private int _tpsFromChordRoot;
        private int _tpsFromQuality;
        private int _tpsToChordRoot = 7; // G
        private int _tpsToQuality;
        private readonly string[] _qualityNames = {
            "Major", "Minor", "Dim", "Aug", "Dom7", "Maj7", "Min7"
        };
        private string _tpsResults = "";

        // Scenarios tab
        private bool _scenarioRunning;
        private int _scenarioStep;
        private float _scenarioTimer;
        private string _scenarioLog = "";
        private readonly List<(string label, float duration, GameMusicState state, float tension)> _scenario
            = new List<(string, float, GameMusicState, float)>
        {
            ("Wandering the woods",       6f, GameMusicState.Exploring,   0.15f),
            ("The forest is peaceful",    5f, GameMusicState.Exploring,   0.3f),
            ("Something stirs...",        4f, GameMusicState.Pressure,    0.4f),
            ("Pressure building",         4f, GameMusicState.Pressure,    0.7f),
            ("A troll attacks!",          8f, GameMusicState.Combat,      0.7f),
            ("The beast appears",         6f, GameMusicState.Combat,      0.9f),
            ("Safety at last",            6f, GameMusicState.Exploring2,  0.2f),
            ("Found a cabin",             5f, GameMusicState.Exploring2,  0.4f),
            ("Something feels wrong...",  6f, GameMusicState.Spooky,      0.3f),
            ("The nisse are watching",    6f, GameMusicState.Spooky,      0.6f),
            ("The beast is near",         6f, GameMusicState.Horror,      0.5f),
            ("IT SEES YOU",               6f, GameMusicState.Horror,      0.9f),
            ("Night falls...",            6f, GameMusicState.Night,       0.1f),
            ("Campfire under stars",      5f, GameMusicState.Night,       0.25f),
            ("Wolves in the distance",    5f, GameMusicState.Night,       0.5f),
            ("The darkness moves",        5f, GameMusicState.Night,       0.8f),
        };

        // Waveform scope
        private float[] _waveformBuffer = new float[1024];
        private int _waveformWriteIndex;
        private Texture2D _waveformTexture;
        private readonly int _scopeWidth = 512;
        private readonly int _scopeHeight = 150;

        // CPU performance history
        private float[] _cpuHistory = new float[100]; // ~20 seconds at 5Hz sample rate
        private int _cpuHistoryIndex;
        private float _cpuHistoryTimer;
        private Texture2D _cpuHistoryTexture;
        private readonly int _cpuGraphWidth = 500;
        private readonly int _cpuGraphHeight = 80;

        // Chord history for display
        private List<string> _chordHistory = new List<string>();
        private float _chordHistoryTimer;

        // GUI styling
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _activeButtonStyle;
        private GUIStyle _pianoKeyStyle;
        private GUIStyle _pianoBlackKeyStyle;
        private bool _stylesInitialized;
        private Vector2 _scrollPos;

        public FullSystemTest()
        {
            // Initialize state names from enum
            _stateNames = Enum.GetNames(typeof(GameMusicState));
        }

        void Start()
        {
            _music = FindObjectOfType<ProceduralMusicController>();
            if (_music == null)
            {
                Debug.LogError("[FullSystemTest] No ProceduralMusicController found in scene!\n" +
                    "Create a GameObject with AudioSource + ProceduralMusicController.");
                return;
            }

            _music.ShowDebugInfo = true;
            _ready = true;

            // Create waveform texture
            _waveformTexture = new Texture2D(_scopeWidth, _scopeHeight, TextureFormat.RGBA32, false);
            _waveformTexture.filterMode = FilterMode.Point;
            ClearWaveformTexture();

            // Create CPU history texture
            _cpuHistoryTexture = new Texture2D(_cpuGraphWidth, _cpuGraphHeight, TextureFormat.RGBA32, false);
            _cpuHistoryTexture.filterMode = FilterMode.Point;

            Debug.Log("[FullSystemTest] Ready. The test GUI should appear in the Game view.");
        }

        void Update()
        {
            if (!_ready) return;

            // Update chord history
            _chordHistoryTimer += Time.deltaTime;
            if (_chordHistoryTimer > 0.5f)
            {
                _chordHistoryTimer = 0f;
                string chordName = _music.GetCurrentChord().ToString();
                if (_chordHistory.Count == 0 || _chordHistory[_chordHistory.Count - 1] != chordName)
                {
                    _chordHistory.Add(chordName);
                    if (_chordHistory.Count > 16) _chordHistory.RemoveAt(0);
                }
            }

            // Run automated scenario
            if (_scenarioRunning)
            {
                UpdateScenario();
            }
        }

        /// <summary>
        /// Capture audio for the waveform scope.
        /// This piggybacks on the same GameObject's audio pipeline.
        /// </summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            // Sample every Nth sample into our display buffer
            int step = Mathf.Max(1, data.Length / channels / _waveformBuffer.Length);
            for (int i = 0; i < data.Length; i += step * channels)
            {
                _waveformBuffer[_waveformWriteIndex] = data[i];
                _waveformWriteIndex = (_waveformWriteIndex + 1) % _waveformBuffer.Length;
            }
        }

        // ─────────────────────────────────────────────
        //  GUI
        // ─────────────────────────────────────────────

        void InitStyles()
        {
            if (_stylesInitialized) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            _activeButtonStyle = new GUIStyle(GUI.skin.button);
            _activeButtonStyle.normal.textColor = Color.green;
            _activeButtonStyle.fontStyle = FontStyle.Bold;

            _pianoKeyStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                fixedWidth = 35,
                fixedHeight = 60
            };

            _pianoBlackKeyStyle = new GUIStyle(_pianoKeyStyle);
            _pianoBlackKeyStyle.normal.textColor = Color.white;

            _stylesInitialized = true;
        }

        void OnGUI()
        {
            if (!_ready) return;
            InitStyles();

            float panelWidth = 620f;
            float panelHeight = Screen.height - 20f;

            GUILayout.BeginArea(new Rect(10, 10, panelWidth, panelHeight));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // Title
            GUILayout.Label("PROCEDURAL MUSIC SYSTEM - FULL TEST", _headerStyle);
            GUILayout.Space(5);

            // Status bar
            DrawStatusBar();
            GUILayout.Space(5);

            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            GUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0: DrawControlsTab(); break;
                case 1: DrawSynthTestTab(); break;
                case 2: DrawTPSVisualizerTab(); break;
                case 3: DrawScenariosTab(); break;
                case 4: DrawWaveformTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ─── Status Bar ──────────────────────────────────

        void DrawStatusBar()
        {
            GUILayout.BeginVertical(_boxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Key: {_music.CurrentKeyName}", GUILayout.Width(140));
            GUILayout.Label($"Chord: {_music.CurrentChordName}", GUILayout.Width(140));
            GUILayout.Label($"State: {_music.CurrentState}", GUILayout.Width(120));
            GUILayout.Label($"Tension: {_music.Tension:F2}", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            // Chord history ribbon
            if (_chordHistory.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Progression: ", GUILayout.Width(80));
                string history = string.Join(" → ", _chordHistory.TakeLast(8));
                GUILayout.Label(history);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        // ─── Tab 0: Controls ─────────────────────────────

        void DrawControlsTab()
        {
            // ── Tension ──
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("TENSION (continuous game parameter)", _headerStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Calm", GUILayout.Width(40));
            _tension = GUILayout.HorizontalSlider(_tension, 0f, 1f);
            GUILayout.Label("Intense", GUILayout.Width(50));
            GUILayout.EndHorizontal();
            _music.SetTension(_tension);

            // Quick-set buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0%")) _tension = 0f;
            if (GUILayout.Button("25%")) _tension = 0.25f;
            if (GUILayout.Button("50%")) _tension = 0.5f;
            if (GUILayout.Button("75%")) _tension = 0.75f;
            if (GUILayout.Button("100%")) _tension = 1f;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(5);

            // ── Game State ──
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("GAME STATE", _headerStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _stateNames.Length; i++)
            {
                var state = (GameMusicState)i;
                bool isActive = _music.CurrentState == state;
                if (GUILayout.Button(_stateNames[i], isActive ? _activeButtonStyle : GUI.skin.button))
                {
                    _music.SetGameState(state);
                    _selectedState = i;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(5);

            // ── Key & Mode ──
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("KEY & MODE", _headerStyle);

            GUILayout.Label("Root note:");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 12; i++)
            {
                bool isActive = _selectedKeyRoot == i;
                if (GUILayout.Button(_keyNames[i], isActive ? _activeButtonStyle : GUI.skin.button,
                    GUILayout.Width(40)))
                {
                    _selectedKeyRoot = i;
                    ApplyKeyChange();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Mode:");
            int newMode = GUILayout.SelectionGrid(_selectedMode, _modeNames, 3);
            if (newMode != _selectedMode)
            {
                _selectedMode = newMode;
                ApplyKeyChange();
            }
            GUILayout.EndVertical();

            GUILayout.Space(5);

            // ── Layer Toggles ──
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("LAYER TOGGLES", _headerStyle);
            GUILayout.BeginHorizontal();

            bool newPad = GUILayout.Toggle(_enablePad, " Pads");
            bool newMel = GUILayout.Toggle(_enableMelody, " Melody");
            bool newBas = GUILayout.Toggle(_enableBass, " Bass");
            bool newPer = GUILayout.Toggle(_enablePerc, " Percussion");
            bool newStr = GUILayout.Toggle(_enableStrings, " Strings");
            bool newKan = GUILayout.Toggle(_enableKantele, " Kantele");

            if (newPad != _enablePad || newMel != _enableMelody ||
                newBas != _enableBass || newPer != _enablePerc ||
                newStr != _enableStrings || newKan != _enableKantele)
            {
                _enablePad = newPad;
                _enableMelody = newMel;
                _enableBass = newBas;
                _enablePerc = newPer;
                _enableStrings = newStr;
                _enableKantele = newKan;
                ApplyLayerToggles();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(5);

            // ── Tempo & Timing ──
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("TEMPO & TIMING", _headerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Master Volume: {_masterVol:F2}", GUILayout.Width(150));
            _masterVol = GUILayout.HorizontalSlider(_masterVol, 0f, 1f);
            _music.MasterVolume = _masterVol;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Beats Per Chord: {_beatsPerChord}", GUILayout.Width(150));
            if (GUILayout.Button("-", GUILayout.Width(30))) _beatsPerChord = Mathf.Max(2, _beatsPerChord - 1);
            if (GUILayout.Button("+", GUILayout.Width(30))) _beatsPerChord = Mathf.Min(8, _beatsPerChord + 1);
            _music.BeatsPerChord = _beatsPerChord;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PANIC (silence all)", GUILayout.Height(30)))
            {
                _music.Panic();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        // ─── Tab 1: Synth Test ───────────────────────────

        void DrawSynthTestTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("ISOLATED INSTRUMENT TEST", _headerStyle);
            GUILayout.Label("Trigger individual notes to hear each synth engine in isolation.");
            GUILayout.Space(5);

            // Instrument selector
            GUILayout.Label("Instrument:");
            _testInstrument = GUILayout.SelectionGrid(_testInstrument, _instrumentNames, 3);

            GUILayout.Space(5);

            // Octave selector
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Octave: {_testOctave}", GUILayout.Width(80));
            if (GUILayout.Button("−", GUILayout.Width(30))) _testOctave = Mathf.Max(1, _testOctave - 1);
            if (GUILayout.Button("+", GUILayout.Width(30))) _testOctave = Mathf.Min(7, _testOctave + 1);
            GUILayout.EndHorizontal();

            // Velocity
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Velocity: {_testVelocity:F2}", GUILayout.Width(120));
            _testVelocity = GUILayout.HorizontalSlider(_testVelocity, 0.1f, 1f);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Piano keyboard (one octave)
            GUILayout.Label("Click to trigger notes:");
            GUILayout.BeginHorizontal();

            // White keys
            string[] whiteNoteNames = { "C", "D", "E", "F", "G", "A", "B" };
            int[] whiteOffsets =       {  0,   2,   4,   5,   7,   9,  11  };
            for (int i = 0; i < 7; i++)
            {
                if (GUILayout.Button(whiteNoteNames[i], GUILayout.Width(40), GUILayout.Height(60)))
                {
                    TriggerTestNote(whiteOffsets[i]);
                }
            }
            if (GUILayout.Button("C+", GUILayout.Width(40), GUILayout.Height(60)))
            {
                TriggerTestNote(12);
            }
            GUILayout.EndHorizontal();

            // Black keys (with spacers)
            GUILayout.BeginHorizontal();
            GUILayout.Space(25);
            if (GUILayout.Button("C#", GUILayout.Width(35), GUILayout.Height(40)))
                TriggerTestNote(1);
            GUILayout.Space(5);
            if (GUILayout.Button("D#", GUILayout.Width(35), GUILayout.Height(40)))
                TriggerTestNote(3);
            GUILayout.Space(40); // gap for E-F
            if (GUILayout.Button("F#", GUILayout.Width(35), GUILayout.Height(40)))
                TriggerTestNote(6);
            GUILayout.Space(5);
            if (GUILayout.Button("G#", GUILayout.Width(35), GUILayout.Height(40)))
                TriggerTestNote(8);
            GUILayout.Space(5);
            if (GUILayout.Button("A#", GUILayout.Width(35), GUILayout.Height(40)))
                TriggerTestNote(10);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Quick test patterns
            GUILayout.Label("Quick Patterns:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("C Major Chord")) TriggerChordTest(new[] { 0, 4, 7 });
            if (GUILayout.Button("C Minor Chord")) TriggerChordTest(new[] { 0, 3, 7 });
            if (GUILayout.Button("Cmaj7")) TriggerChordTest(new[] { 0, 4, 7, 11 });
            if (GUILayout.Button("C7")) TriggerChordTest(new[] { 0, 4, 7, 10 });
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("C Scale Up"))
                StartCoroutine(PlayScaleCoroutine(new[] { 0, 2, 4, 5, 7, 9, 11, 12 }));
            if (GUILayout.Button("Chromatic Up"))
                StartCoroutine(PlayScaleCoroutine(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }));
            if (GUILayout.Button("Stop Note"))
            {
                if (_lastTriggeredNote >= 0)
                {
                    var instruments = GetInstrumentList();
                    if (_testInstrument < instruments.Count)
                        instruments[_testInstrument].NoteOff(_lastTriggeredNote);
                }
            }
            GUILayout.EndHorizontal();

            if (_lastTriggeredNote >= 0)
            {
                float freq = TonalPitchSpace.MidiToFrequency(_lastTriggeredNote);
                GUILayout.Label($"Last note: MIDI {_lastTriggeredNote} ({freq:F1} Hz) on {_instrumentNames[_testInstrument]}");
            }

            GUILayout.EndVertical();
        }

        // ─── Tab 2: TPS Visualizer ──────────────────────

        void DrawTPSVisualizerTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("TONAL PITCH SPACE ANALYSIS", _headerStyle);
            GUILayout.Label("Calculate distances, tension, and attraction between chords.");
            GUILayout.Space(5);

            // From chord
            GUILayout.Label("FROM chord:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Root:", GUILayout.Width(40));
            _tpsFromChordRoot = GUILayout.SelectionGrid(_tpsFromChordRoot, _keyNames, 12);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Quality:", GUILayout.Width(50));
            _tpsFromQuality = GUILayout.SelectionGrid(_tpsFromQuality, _qualityNames, 7);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // To chord
            GUILayout.Label("TO chord:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Root:", GUILayout.Width(40));
            _tpsToChordRoot = GUILayout.SelectionGrid(_tpsToChordRoot, _keyNames, 12);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Quality:", GUILayout.Width(50));
            _tpsToQuality = GUILayout.SelectionGrid(_tpsToQuality, _qualityNames, 7);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (GUILayout.Button("Calculate TPS Distances", GUILayout.Height(30)))
            {
                CalculateTPS();
            }

            GUILayout.Space(5);

            // Show all diatonic chord tensions in current key
            if (GUILayout.Button("Show All Diatonic Tensions (Current Key)", GUILayout.Height(30)))
            {
                CalculateAllDiatonicTensions();
            }

            // Show attraction values for all 12 pitch classes
            if (GUILayout.Button("Show Pitch Attractions (Current Chord)", GUILayout.Height(30)))
            {
                CalculateAttractions();
            }

            GUILayout.Space(5);

            if (!string.IsNullOrEmpty(_tpsResults))
            {
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Label(_tpsResults);
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
        }

        // ─── Tab 3: Scenarios ────────────────────────────

        void DrawScenariosTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("AUTOMATED TEST SCENARIOS", _headerStyle);
            GUILayout.Label("Runs through a scripted game sequence to test all states and transitions.");
            GUILayout.Space(5);

            // Scenario list
            GUILayout.Label("Sequence:");
            for (int i = 0; i < _scenario.Count; i++)
            {
                var step = _scenario[i];
                string prefix = (_scenarioRunning && i == _scenarioStep) ? "►  " : "    ";
                string highlight = (_scenarioRunning && i == _scenarioStep) ? " ◄ PLAYING" : "";
                GUILayout.Label($"{prefix}{i + 1}. {step.label} [{step.state}, T={step.tension:F1}] " +
                    $"({step.duration}s){highlight}");
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (!_scenarioRunning)
            {
                if (GUILayout.Button("▶  Run Full Scenario", GUILayout.Height(35)))
                {
                    StartScenario();
                }
            }
            else
            {
                if (GUILayout.Button("■  Stop Scenario", GUILayout.Height(35)))
                {
                    _scenarioRunning = false;
                    _scenarioLog += "Scenario stopped by user.\n";
                }

                // Progress bar
                if (_scenarioStep < _scenario.Count)
                {
                    float stepProgress = _scenarioTimer / _scenario[_scenarioStep].duration;
                    GUILayout.Label($"Step {_scenarioStep + 1}/{_scenario.Count}  " +
                        $"({stepProgress * 100:F0}%)", GUILayout.Width(150));
                }
            }
            GUILayout.EndHorizontal();

            // Quick individual state tests
            GUILayout.Space(10);
            GUILayout.Label("Quick State Tests (5 seconds each):");
            GUILayout.BeginHorizontal();
            foreach (GameMusicState state in Enum.GetValues(typeof(GameMusicState)))
            {
                if (GUILayout.Button(state.ToString()))
                {
                    _music.SetGameState(state);
                    _music.SetTension(0.5f);
                    _scenarioLog += $"Quick test: {state} at tension 0.5\n";
                }
            }
            GUILayout.EndHorizontal();

            // Log
            GUILayout.Space(5);
            if (!string.IsNullOrEmpty(_scenarioLog))
            {
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Label("Log:");
                // Show last 8 lines
                var lines = _scenarioLog.Split('\n');
                int start = Mathf.Max(0, lines.Length - 9);
                for (int i = start; i < lines.Length; i++)
                    GUILayout.Label(lines[i]);
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
        }

        // ─── Tab 4: Waveform & Performance ──────────────

        void DrawWaveformTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("LIVE AUDIO WAVEFORM", _headerStyle);

            // Update texture from buffer
            UpdateWaveformTexture();
            GUILayout.Label(_waveformTexture);

            // Audio stats
            float peak = 0f;
            float rms = 0f;
            for (int i = 0; i < _waveformBuffer.Length; i++)
            {
                float abs = Mathf.Abs(_waveformBuffer[i]);
                if (abs > peak) peak = abs;
                rms += _waveformBuffer[i] * _waveformBuffer[i];
            }
            rms = Mathf.Sqrt(rms / _waveformBuffer.Length);

            GUILayout.Label($"Peak: {peak:F4}  |  RMS: {rms:F4}  |  Peak dB: {(peak > 0 ? 20f * Mathf.Log10(peak) : -100f):F1} dB");

            GUILayout.EndVertical();

            GUILayout.Space(5);

            // ── CPU Performance ──
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("DSP PERFORMANCE", _headerStyle);

            double avgMs = _music.DspAvgMs;
            double peakMs = _music.DspPeakMs;
            double cpuPct = _music.DspCpuPercent;
            double budgetMs = _music.DspBudgetMs;

            // Color-coded CPU usage
            string cpuColor;
            string cpuStatus;
            if (cpuPct < 15)      { cpuColor = "green";  cpuStatus = "EXCELLENT"; }
            else if (cpuPct < 30) { cpuColor = "green";  cpuStatus = "GOOD"; }
            else if (cpuPct < 50) { cpuColor = "yellow"; cpuStatus = "MODERATE"; }
            else if (cpuPct < 80) { cpuColor = "orange"; cpuStatus = "HIGH"; }
            else                  { cpuColor = "red";    cpuStatus = "CRITICAL"; }

            GUILayout.Label($"Audio Thread CPU:  {cpuPct:F1}%  [{cpuStatus}]");
            GUILayout.Label($"DSP Time:  avg {avgMs:F2}ms  |  peak {peakMs:F2}ms  |  budget {budgetMs:F1}ms");

            // CPU usage bar
            GUILayout.BeginHorizontal();
            GUILayout.Label("Usage:", GUILayout.Width(45));
            float barWidth = 400f;
            Rect barRect = GUILayoutUtility.GetRect(barWidth, 18);
            GUI.Box(barRect, "");
            float fillWidth = Mathf.Clamp01((float)cpuPct / 100f) * barRect.width;
            Color barColor = cpuPct < 30 ? Color.green : cpuPct < 60 ? Color.yellow : Color.red;
            Color oldColor = GUI.color;
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUILayout.EndHorizontal();

            // Track CPU history
            _cpuHistoryTimer += Time.deltaTime;
            if (_cpuHistoryTimer > 0.2f)
            {
                _cpuHistoryTimer = 0f;
                _cpuHistory[_cpuHistoryIndex] = (float)cpuPct;
                _cpuHistoryIndex = (_cpuHistoryIndex + 1) % _cpuHistory.Length;
            }

            // Draw CPU history graph
            GUILayout.Label("CPU History (last ~20 seconds):");
            UpdateCpuHistoryTexture();
            GUILayout.Label(_cpuHistoryTexture);

            GUILayout.Space(5);

            // ── Voice Counts ──
            GUILayout.Label("ACTIVE VOICES", _headerStyle);
            var instruments = GetInstrumentList();
            int totalActive = 0;
            int totalMax = 0;

            GUILayout.BeginHorizontal();
            for (int i = 0; i < instruments.Count && i < _instrumentNames.Length; i++)
            {
                int active = instruments[i].ActiveVoiceCount;
                int max = instruments[i].MaxVoices;
                totalActive += active;
                totalMax += max;
                GUILayout.Label($"{_instrumentNames[i].Split('(')[0].Trim()}: {active}/{max}",
                    GUILayout.Width(100));
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Total: {totalActive}/{totalMax} voices active");

            GUILayout.Space(5);

            // ── Quick audio tests ──
            GUILayout.Label("Audio Tests:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("A440 (1s)"))
            {
                if (instruments.Count > 1)
                {
                    instruments[1].NoteOn(69, 0.8f);
                    _lastTriggeredNote = 69;
                    Invoke(nameof(StopTestTone), 1f);
                }
            }
            if (GUILayout.Button("Low C (1s)"))
            {
                if (instruments.Count > 2)
                {
                    instruments[2].NoteOn(36, 0.8f);
                    _lastTriggeredNote = 36;
                    Invoke(nameof(StopTestTone), 1f);
                }
            }
            if (GUILayout.Button("Kick"))
            {
                if (instruments.Count > 3) instruments[3].NoteOn(36, 0.9f);
            }
            if (GUILayout.Button("Snare"))
            {
                if (instruments.Count > 4) instruments[4].NoteOn(38, 0.85f);
            }
            if (GUILayout.Button("HiHat"))
            {
                if (instruments.Count > 5) instruments[5].NoteOn(42, 0.6f);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        // ─────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────

        private void ApplyKeyChange()
        {
            _music.ForceModulation((PitchClass)_selectedKeyRoot, (MusicalMode)_selectedMode);
        }

        private void ApplyLayerToggles()
        {
            _music.SetLayerEnabled(_enablePad, _enableMelody, _enableBass, _enablePerc,
                _enableStrings, _enableKantele);
        }

        private List<VoiceManager> GetInstrumentList()
        {
            return _music.GetInstruments();
        }

        private void TriggerTestNote(int semitoneOffset)
        {
            int midi = (_testOctave + 1) * 12 + semitoneOffset;
            var instruments = GetInstrumentList();
            if (_testInstrument < instruments.Count)
            {
                // Stop previous note on this instrument
                if (_lastTriggeredNote >= 0)
                    instruments[_testInstrument].NoteOff(_lastTriggeredNote);

                instruments[_testInstrument].NoteOn(midi, _testVelocity);
                _lastTriggeredNote = midi;

                float freq = TonalPitchSpace.MidiToFrequency(midi);
                Debug.Log($"[SynthTest] Note ON: MIDI {midi} ({freq:F1} Hz) vel={_testVelocity:F2} " +
                    $"on {_instrumentNames[_testInstrument]}");
            }
        }

        private void TriggerChordTest(int[] offsets)
        {
            var instruments = GetInstrumentList();
            int inst = _testInstrument;
            // For chords, prefer the pad instrument
            if (inst >= 3) inst = 0; // percussion can't chord, use pad

            if (inst < instruments.Count)
            {
                instruments[inst].AllNotesOff();
                foreach (int offset in offsets)
                {
                    int midi = (_testOctave + 1) * 12 + offset;
                    instruments[inst].NoteOn(midi, _testVelocity);
                }
            }
        }

        private System.Collections.IEnumerator PlayScaleCoroutine(int[] offsets)
        {
            var instruments = GetInstrumentList();
            if (_testInstrument >= instruments.Count) yield break;

            foreach (int offset in offsets)
            {
                if (_lastTriggeredNote >= 0)
                    instruments[_testInstrument].NoteOff(_lastTriggeredNote);

                int midi = (_testOctave + 1) * 12 + offset;
                instruments[_testInstrument].NoteOn(midi, _testVelocity);
                _lastTriggeredNote = midi;
                yield return new WaitForSeconds(0.25f);
            }

            yield return new WaitForSeconds(0.5f);
            if (_lastTriggeredNote >= 0)
                instruments[_testInstrument].NoteOff(_lastTriggeredNote);
        }

        private void StopTestTone()
        {
            if (_lastTriggeredNote >= 0)
            {
                var instruments = GetInstrumentList();
                foreach (var inst in instruments)
                    inst.NoteOff(_lastTriggeredNote);
            }
        }

        // ─── TPS Calculations ────────────────────────────

        private void CalculateTPS()
        {
            Chord from = new Chord((PitchClass)_tpsFromChordRoot, (ChordQuality)_tpsFromQuality);
            Chord to = new Chord((PitchClass)_tpsToChordRoot, (ChordQuality)_tpsToQuality);
            Key currentKey = new Key(_music.StartingKey, _music.StartingMode);

            float chordDist = TonalPitchSpace.ChordDistance(from, to, currentKey);
            float tensionFrom = TonalPitchSpace.GetTension(from, currentKey);
            float tensionTo = TonalPitchSpace.GetTension(to, currentKey);
            int fifths = TonalPitchSpace.CircleOfFifthsDistance((int)from.Root, (int)to.Root);
            float dissonanceFrom = TonalPitchSpace.GetChordDissonance(from);
            float dissonanceTo = TonalPitchSpace.GetChordDissonance(to);

            _tpsResults = $"═══ TPS Analysis ═══\n" +
                $"From: {from}    To: {to}\n" +
                $"Key context: {currentKey}\n\n" +
                $"Chord Distance (δ):     {chordDist:F2}\n" +
                $"Circle of 5ths dist:    {fifths}\n\n" +
                $"Tension ({from}):       {tensionFrom:F2}\n" +
                $"Tension ({to}):         {tensionTo:F2}\n" +
                $"Tension change:          {tensionTo - tensionFrom:+0.00;-0.00}\n\n" +
                $"Dissonance ({from}):    {dissonanceFrom:F2}\n" +
                $"Dissonance ({to}):      {dissonanceTo:F2}\n";
        }

        private void CalculateAllDiatonicTensions()
        {
            Key key = new Key(_music.StartingKey, _music.StartingMode);
            Chord[] diatonic = key.GetDiatonicChords();
            string[] numerals = { "I", "ii", "iii", "IV", "V", "vi", "vii°" };

            _tpsResults = $"═══ Diatonic Tensions in {key} ═══\n\n";
            for (int i = 0; i < diatonic.Length; i++)
            {
                float tension = TonalPitchSpace.GetTension(diatonic[i], key);
                float dissonance = TonalPitchSpace.GetChordDissonance(diatonic[i]);
                string bar = new string('█', Mathf.RoundToInt(tension * 4));
                _tpsResults += $"  {numerals[i],-5} {diatonic[i],-12} " +
                    $"tension={tension:F2}  dissonance={dissonance:F1}  {bar}\n";
            }

            // Also show suggested chords at different tension levels
            _tpsResults += $"\n═══ Chord Suggestions (from {diatonic[0]}) ═══\n";
            foreach (float target in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                var suggestions = TonalPitchSpace.SuggestNextChords(diatonic[0], key, target, false, 3);
                string names = string.Join(", ", suggestions.Select(c => c.ToString()));
                _tpsResults += $"  Target {target:F2}: {names}\n";
            }
        }

        private void CalculateAttractions()
        {
            Chord chord = _music.GetCurrentChord();
            Key key = new Key(_music.StartingKey, _music.StartingMode);

            _tpsResults = $"═══ Pitch Attractions (chord: {chord}, key: {key}) ═══\n\n";
            _tpsResults += "  PC   Name   Stability   Attraction   Role\n";
            _tpsResults += "  ──   ────   ─────────   ──────────   ────\n";

            int[] scalePCs = key.GetScalePitchClasses();
            int[] chordPCs = chord.GetPitchClasses();

            for (int pc = 0; pc < 12; pc++)
            {
                float stability = TonalPitchSpace.GetPitchStability(pc, chordPCs, scalePCs, (int)key.Root);
                float attraction = TonalPitchSpace.GetAttraction(pc, chord, key);

                string role;
                if (pc == (int)key.Root && chordPCs.Contains(pc)) role = "ROOT";
                else if (chordPCs.Contains(pc)) role = "chord tone";
                else if (scalePCs.Contains(pc)) role = "scale tone";
                else role = "chromatic";

                string aBar = new string('▓', Mathf.RoundToInt(attraction * 5));
                string sBar = new string('█', Mathf.RoundToInt(stability));

                _tpsResults += $"  {pc,-4} {_keyNames[pc],-6} {stability:F1} {sBar,-5}   " +
                    $"{attraction:F3} {aBar,-6}   {role}\n";
            }
        }

        // ─── Scenario Runner ─────────────────────────────

        private void StartScenario()
        {
            _scenarioRunning = true;
            _scenarioStep = 0;
            _scenarioTimer = 0f;
            _scenarioLog = $"[{Time.time:F1}] Scenario started.\n";
            ApplyScenarioStep();
        }

        private void UpdateScenario()
        {
            _scenarioTimer += Time.deltaTime;

            if (_scenarioStep < _scenario.Count &&
                _scenarioTimer >= _scenario[_scenarioStep].duration)
            {
                _scenarioStep++;
                _scenarioTimer = 0f;

                if (_scenarioStep >= _scenario.Count)
                {
                    _scenarioRunning = false;
                    _scenarioLog += $"[{Time.time:F1}] Scenario complete!\n";
                }
                else
                {
                    ApplyScenarioStep();
                }
            }

            // Smooth tension interpolation within step
            if (_scenarioRunning && _scenarioStep < _scenario.Count)
            {
                _tension = _scenario[_scenarioStep].tension;
                _music.SetTension(_tension);
            }
        }

        private void ApplyScenarioStep()
        {
            var step = _scenario[_scenarioStep];
            _music.SetGameState(step.state);
            _music.SetTension(step.tension);
            _tension = step.tension;
            _scenarioLog += $"[{Time.time:F1}] Step {_scenarioStep + 1}: {step.label} " +
                $"(state={step.state}, tension={step.tension})\n";
        }

        // ─── Waveform Rendering ──────────────────────────

        private void ClearWaveformTexture()
        {
            Color[] clear = new Color[_scopeWidth * _scopeHeight];
            Color bg = new Color(0.1f, 0.1f, 0.15f);
            for (int i = 0; i < clear.Length; i++) clear[i] = bg;
            _waveformTexture.SetPixels(clear);
            _waveformTexture.Apply();
        }

        private void UpdateWaveformTexture()
        {
            if (_waveformTexture == null) return;

            Color bg = new Color(0.1f, 0.1f, 0.15f);
            Color waveColor = new Color(0.3f, 0.8f, 0.4f);
            Color centerLine = new Color(0.25f, 0.25f, 0.35f);

            Color[] pixels = new Color[_scopeWidth * _scopeHeight];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            // Center line
            int centerY = _scopeHeight / 2;
            for (int x = 0; x < _scopeWidth; x++)
                pixels[centerY * _scopeWidth + x] = centerLine;

            // Draw waveform
            int step = Mathf.Max(1, _waveformBuffer.Length / _scopeWidth);
            for (int x = 0; x < _scopeWidth; x++)
            {
                int bufIdx = (x * step + _waveformWriteIndex) % _waveformBuffer.Length;
                float sample = _waveformBuffer[bufIdx];
                int y = Mathf.Clamp(centerY + Mathf.RoundToInt(sample * centerY * 0.9f), 0, _scopeHeight - 1);

                // Draw vertical line from center to sample
                int minY = Mathf.Min(centerY, y);
                int maxY = Mathf.Max(centerY, y);
                for (int py = minY; py <= maxY; py++)
                    pixels[py * _scopeWidth + x] = waveColor;
            }

            _waveformTexture.SetPixels(pixels);
            _waveformTexture.Apply();
        }

        private void UpdateCpuHistoryTexture()
        {
            if (_cpuHistoryTexture == null) return;

            Color bg = new Color(0.1f, 0.1f, 0.15f);
            Color gridLine = new Color(0.2f, 0.2f, 0.25f);

            Color[] pixels = new Color[_cpuGraphWidth * _cpuGraphHeight];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            // Draw threshold lines at 25%, 50%, 75%
            foreach (float threshold in new[] { 0.25f, 0.5f, 0.75f })
            {
                int y = Mathf.RoundToInt(threshold * (_cpuGraphHeight - 1));
                for (int x = 0; x < _cpuGraphWidth; x++)
                    pixels[y * _cpuGraphWidth + x] = gridLine;
            }

            // Draw CPU history as a filled graph
            int samplesCount = _cpuHistory.Length;
            float xStep = (float)_cpuGraphWidth / samplesCount;

            for (int i = 0; i < samplesCount; i++)
            {
                int histIdx = (_cpuHistoryIndex + i) % samplesCount;
                float cpuPct = _cpuHistory[histIdx];
                int barHeight = Mathf.Clamp(Mathf.RoundToInt(cpuPct / 100f * (_cpuGraphHeight - 1)), 0, _cpuGraphHeight - 1);

                int x = Mathf.RoundToInt(i * xStep);
                if (x >= _cpuGraphWidth) x = _cpuGraphWidth - 1;

                // Color: green → yellow → red
                Color barColor;
                if (cpuPct < 30f) barColor = Color.Lerp(new Color(0.2f, 0.6f, 0.3f), new Color(0.6f, 0.8f, 0.2f), cpuPct / 30f);
                else if (cpuPct < 60f) barColor = Color.Lerp(new Color(0.6f, 0.8f, 0.2f), new Color(0.9f, 0.6f, 0.1f), (cpuPct - 30f) / 30f);
                else barColor = Color.Lerp(new Color(0.9f, 0.6f, 0.1f), new Color(0.9f, 0.2f, 0.1f), Mathf.Clamp01((cpuPct - 60f) / 40f));

                for (int y = 0; y < barHeight; y++)
                {
                    int px = Mathf.Clamp(x, 0, _cpuGraphWidth - 1);
                    pixels[y * _cpuGraphWidth + px] = barColor;
                }
            }

            _cpuHistoryTexture.SetPixels(pixels);
            _cpuHistoryTexture.Apply();
        }
    }
}
