using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using ProceduralMusic.Core;
using ProceduralMusic.Bridge;
using ProceduralMusic.Synthesis;

namespace InventorySystem.Tools
{
    /// <summary>
    /// Flute instrument — plays notes through the procedural synth, ring drawn with Unity UI.
    ///
    /// SETUP:
    ///   1. Add this component to the Player GameObject.
    ///
    ///   2. CREATE THE CANVAS:
    ///      - Right-click Player in Hierarchy → UI → Canvas
    ///      - Canvas Inspector: Render Mode = World Space, Event Camera = Main Camera
    ///      - RectTransform: Width = 500, Height = 500
    ///      - Transform Scale: X = 0.01, Y = 0.01, Z = 0.01
    ///      - Assign it to the "Ring Canvas" field below
    ///
    ///   3. CREATE THE NOTE BUTTON PREFAB:
    ///      - In the canvas, right-click → UI → Image  (one note dot)
    ///      - RectTransform: Width = 80, Height = 80
    ///      - Image: any color, assign a circle sprite if you have one
    ///      - Right-click that Image → UI → Text - TextMeshPro
    ///        Set font size ~24, alignment Center/Middle, stretch anchors to fill parent
    ///      - Drag the Image into the Project window → makes a Prefab
    ///      - Delete the scene instance (canvas stays empty)
    ///      - Assign the prefab to "Note Button Prefab" below
    ///
    ///   4. Assign PlayerCamera, StateManager, PointAction as before.
    ///
    /// PROCEDURAL MUSIC OFF (b-test scenes):
    ///   If no ProceduralMusicController is present in the scene, FluteTool
    ///   auto-spawns a StandaloneFluteAudioHost — a minimal AudioSource that
    ///   renders a single LegatoLead voice through OnAudioFilterRead. No
    ///   manual scene setup required: the flute just works.
    /// </summary>
    public class FluteTool : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Main camera. Auto-finds Camera.main if empty.")]
        public Camera PlayerCamera;

        [Tooltip("PlayerStateManager. Auto-found on the same GameObject if empty.")]
        public PlayerStateManager StateManager;

        [Tooltip("World Space Canvas that is a child of the Player.")]
        public Canvas RingCanvas;

        [Tooltip("Prefab for each note button — an Image with a TextMeshPro child.")]
        public GameObject NoteButtonPrefab;

        [Header("Input Actions")]
        [Tooltip("Mouse position. Bind to <Mouse>/position. Type = Value, Control = Vector2.")]
        [SerializeField]
        private InputAction PointAction = new InputAction(
            "FlutePoint", InputActionType.Value, expectedControlType: "Vector2");

        [Header("Ring Layout")]
        [Tooltip("Radius in canvas units. Canvas is 500x500 at 0.01 scale, so 150 = 1.5 world units.")]
        public float RingRadius = 150f;

        [Tooltip("Angle of the first note in degrees. 90 = top of the ring.")]
        public float StartAngleDeg = 90f;

        [Tooltip("Which MIDI octave the lowest note plays in. 4 = middle C octave.")]
        [Range(3, 6)]
        public int BaseOctave = 4;

        [Header("Music Ducking")]
        [Tooltip("Background music volume while the ring is open.")]
        [Range(0f, 1f)]
        public float DuckedVolume = 0.35f;

        [Header("Standalone Fallback")]
        [Tooltip("Volume of the standalone flute host when ProceduralMusicController is absent " +
                 "(e.g. b-test scenes). Has no effect when the full music system is running.")]
        [Range(0f, 1f)]
        public float StandaloneVolume = 0.7f;

        [Header("Colors")]
        public Color NoteIdleColor = new Color(0.80f, 0.93f, 1.00f, 0.85f);
        public Color NoteHoverColor = new Color(1.00f, 1.00f, 0.65f, 1.00f);
        public Color NotePlayingColor = new Color(0.45f, 1.00f, 0.70f, 1.00f);
        public Color LabelIdleColor = new Color(0.10f, 0.10f, 0.15f, 1.00f);
        public Color LabelActiveColor = Color.white;

        // ── Runtime ───────────────────────────────────────────────────────

        /// <summary>True while the note ring is open — PauseManager checks this to skip pause.</summary>
        public bool IsOpen => _isOpen;

        private bool _isOpen = false;
        private int _hoveredNote = -1;
        private int _activeNote = -1;

        // Fullscreen invisible blocker — swallows all clicks while the ring is open
        // so the player can't interact with anything behind it.
        private GameObject _blockerGO;

        private int[] _midiNotes = new int[8];
        private string[] _noteNames = new string[8];

        private List<RectTransform> _noteRects = new List<RectTransform>();
        private List<Image> _noteImages = new List<Image>();
        private List<TMP_Text> _noteLabels = new List<TMP_Text>();

        // Dedicated player flute voice — separate from the composition engine's lead
        private VoiceManager _fluteVoice;

        // Damage event subscription (closes the ring on attack)
        private HappinessSystem _happinessSystem;
        private bool _subscribedToDamage;

        private static readonly string[] NoteTable =
            { "C","C#","D","D#","E","F","F#","G","G#","A","A#","B" };

        // ─────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (PlayerCamera == null)
                PlayerCamera = Camera.main;

            if (StateManager == null)
                StateManager = GetComponent<PlayerStateManager>();

            if (PointAction.bindings.Count == 0)
                PointAction.AddBinding("<Mouse>/position");
        }

        void Start()
        {
            // HappinessSystem may not exist yet in Awake (load order),
            // so subscribe here. OpenRing also retries in case the system
            // loads even later (e.g. on first scene where the player is spawned).
            TrySubscribeToDamage();
        }

        void OnDestroy()
        {
            if (_happinessSystem != null && _subscribedToDamage)
            {
                _happinessSystem.OnDamaged -= HandleDamaged;
                _subscribedToDamage = false;
            }
            PointAction.Disable();
        }

        void TrySubscribeToDamage()
        {
            if (_subscribedToDamage) return;

            _happinessSystem = HappinessSystem.Instance
                               ?? FindObjectOfType<HappinessSystem>();

            if (_happinessSystem != null)
            {
                _happinessSystem.OnDamaged += HandleDamaged;
                _subscribedToDamage = true;
            }
        }

        /// <summary>
        /// Called by HappinessSystem when the player takes damage that survived
        /// i-frame filtering. We close the ring immediately so the player can't
        /// keep playing the flute through a hit.
        /// </summary>
        void HandleDamaged(float amountLost)
        {
            if (_isOpen) CloseRing();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Toggle the ring open or closed. Called by ToolUseSystem.</summary>
        public void OnFluteUsed()
        {
            if (_isOpen) CloseRing();
            else OpenRing();
        }

        /// <summary>Force-close the ring (unequip, inventory open, etc).</summary>
        public void ForceClose()
        {
            if (_isOpen) CloseRing();
        }

        // ── Open / Close ──────────────────────────────────────────────────

        void OpenRing()
        {
            if (RingCanvas == null || NoteButtonPrefab == null)
            {
                Debug.LogWarning("[FluteTool] RingCanvas or NoteButtonPrefab not assigned.");
                return;
            }

            _isOpen = true;
            TrySubscribeToDamage(); // Late-load safety net
            CacheFluteVoice();
            RebuildNoteData();

            // Create a dedicated Screen Space Overlay canvas for the blocker.
            // The RingCanvas is World Space (tiny, parented to the player) so stretching
            // a RectTransform there won't cover the screen. A separate overlay canvas
            // sits on top of everything and swallows all pointer events.
            _blockerGO = new GameObject("FluteBlocker");
            var blockerCanvas = _blockerGO.AddComponent<UnityEngine.Canvas>();
            blockerCanvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            blockerCanvas.sortingOrder = 900; // High enough to be above game UI, below note ring
            _blockerGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            _blockerGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var blockerImgGO = new GameObject("BlockerImage");
            blockerImgGO.transform.SetParent(_blockerGO.transform, false);
            var blockerRT = blockerImgGO.AddComponent<RectTransform>();
            blockerRT.anchorMin = Vector2.zero;
            blockerRT.anchorMax = Vector2.one;
            blockerRT.offsetMin = Vector2.zero;
            blockerRT.offsetMax = Vector2.zero;
            var blockerImg = blockerImgGO.AddComponent<UnityEngine.UI.Image>();
            blockerImg.color = Color.clear; // Transparent but blocks raycasts

            SpawnNoteButtons();

            PointAction.Enable();
            StateManager?.StartMusicPlaying();

            if (ProceduralMusicController.Instance != null)
                ProceduralMusicController.Instance.MasterVolume = DuckedVolume;
        }

        void CloseRing()
        {
            _isOpen = false;
            _hoveredNote = -1;
            StopCurrentNote();
            DestroyNoteButtons();

            if (_blockerGO != null)
            {
                Destroy(_blockerGO);
                _blockerGO = null;
            }

            PointAction.Disable();

            if (ProceduralMusicController.Instance != null)
                ProceduralMusicController.Instance.MasterVolume = 0.7f;

            StateManager?.StopMusicPlaying();
        }

        // ── Spawning ──────────────────────────────────────────────────────

        void SpawnNoteButtons()
        {
            _noteRects.Clear();
            _noteImages.Clear();
            _noteLabels.Clear();

            for (int i = 0; i < 8; i++)
            {
                GameObject btn = Instantiate(NoteButtonPrefab, RingCanvas.transform);
                RectTransform rt = btn.GetComponent<RectTransform>();

                // Position in a circle in canvas-space coordinates
                float deg = StartAngleDeg - i * 45f;
                float rad = deg * Mathf.Deg2Rad;
                rt.anchoredPosition = new Vector2(
                    Mathf.Cos(rad) * RingRadius,
                    Mathf.Sin(rad) * RingRadius);

                rt.localScale = Vector3.one;

                Image img = btn.GetComponent<Image>();
                if (img != null) img.color = NoteIdleColor;

                TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = _noteNames[i];
                    label.color = LabelIdleColor;
                }

                _noteRects.Add(rt);
                _noteImages.Add(img);
                _noteLabels.Add(label);
            }
        }

        void DestroyNoteButtons()
        {
            foreach (var rt in _noteRects)
                if (rt != null) Destroy(rt.gameObject);

            _noteRects.Clear();
            _noteImages.Clear();
            _noteLabels.Clear();
        }

        // ── Update ────────────────────────────────────────────────────────

        void Update()
        {
            if (!_isOpen) return;

            // Hurt is handled by the OnDamaged event (closes the ring instantly).
            // Death polling here catches deaths from non-damage sources (starvation, etc.).
            if (StateManager != null && StateManager.CurrentState == StateManager.deathState)
            {
                CloseRing();
                return;
            }

            UpdateHover();
            UpdateButtonVisuals();

            // Play on hover, switch notes as cursor moves
            if (_hoveredNote != _activeNote)
            {
                if (_hoveredNote >= 0)
                    PlayNote(_hoveredNote);
                else
                    StopCurrentNote();
            }
        }

        void UpdateHover()
        {
            if (PlayerCamera == null || _noteRects.Count == 0) return;

            Vector2 mouseScreen = PointAction.ReadValue<Vector2>();

            float best = float.MaxValue;
            int bestIdx = -1;

            for (int i = 0; i < _noteRects.Count; i++)
            {
                if (_noteRects[i] == null) continue;

                // Convert each button's world position to screen space
                Vector2 noteScreen = RectTransformUtility.WorldToScreenPoint(
                    PlayerCamera, _noteRects[i].position);

                float d = Vector2.Distance(mouseScreen, noteScreen);
                if (d < best) { best = d; bestIdx = i; }
            }

            // Pick radius based on the button's approximate screen size
            float pickRadius = GetNoteScreenRadius();
            _hoveredNote = (best < pickRadius) ? bestIdx : -1;
        }

        float GetNoteScreenRadius()
        {
            if (_noteRects.Count == 0) return 40f;

            RectTransform rt = _noteRects[0];
            float worldSize = rt.rect.width * RingCanvas.transform.lossyScale.x;

            float screenSize;
            if (PlayerCamera.orthographic)
                screenSize = worldSize * Screen.height / (PlayerCamera.orthographicSize * 2f);
            else
                screenSize = worldSize / (2f * Mathf.Tan(PlayerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad))
                             * Screen.height
                             / Vector3.Distance(PlayerCamera.transform.position, rt.position);

            return Mathf.Max(screenSize * 0.6f, 35f);
        }

        void UpdateButtonVisuals()
        {
            for (int i = 0; i < _noteImages.Count; i++)
            {
                if (_noteImages[i] == null) continue;

                bool isActive = (i == _activeNote);
                bool isHovered = (i == _hoveredNote);

                _noteImages[i].color = isActive ? NotePlayingColor
                                     : isHovered ? NoteHoverColor
                                     : NoteIdleColor;

                _noteRects[i].localScale = (isHovered || isActive)
                    ? Vector3.one * 1.2f
                    : Vector3.one;

                if (_noteLabels[i] != null)
                    _noteLabels[i].color = (isActive || isHovered)
                        ? LabelActiveColor
                        : LabelIdleColor;
            }
        }

        // ── Sound ─────────────────────────────────────────────────────────

        void PlayNote(int index)
        {
            StopCurrentNote();
            _activeNote = index;
            _fluteVoice?.NoteOn(_midiNotes[index], 0.85f);
        }

        void StopCurrentNote()
        {
            if (_activeNote < 0) return;
            _fluteVoice?.NoteOff(_midiNotes[_activeNote]);
            _activeNote = -1;
        }

        // ── Note data ─────────────────────────────────────────────────────

        void RebuildNoteData()
        {
            ProceduralMusic.Core.Key key = GetCurrentKey();
            int[] degrees = key.GetScaleDegrees();
            int root = (BaseOctave + 1) * 12 + (int)key.Root;

            for (int i = 0; i < 7; i++)
            {
                _midiNotes[i] = root + degrees[i];
                _noteNames[i] = NoteTable[((int)key.Root + degrees[i]) % 12];
            }

            _midiNotes[7] = root + 12;
            _noteNames[7] = NoteTable[(int)key.Root % 12];
        }

        ProceduralMusic.Core.Key GetCurrentKey()
        {
            if (ProceduralMusicController.Instance != null)
            {
                var composer = ProceduralMusicController.Instance.GetComposer();
                if (composer != null) return composer.CurrentKey;
            }
            return new ProceduralMusic.Core.Key(
                ProceduralMusic.Core.PitchClass.C,
                ProceduralMusic.Core.MusicalMode.Major);
        }

        // ── Voice resolution ──────────────────────────────────────────────

        /// <summary>
        /// Resolve the flute VoiceManager. Priority:
        ///   1. ProceduralMusicController in scene → use its dedicated flute voice.
        ///   2. Existing StandaloneFluteAudioHost in scene → use its voice.
        ///   3. Spawn a new StandaloneFluteAudioHost on the fly.
        /// </summary>
        void CacheFluteVoice()
        {
            _fluteVoice = null;

            // 1. Full procedural music system, when available
            var controller = ProceduralMusicController.Instance
                             ?? FindObjectOfType<ProceduralMusicController>();

            if (controller != null)
            {
                _fluteVoice = controller.GetPlayerFluteVoice();
                if (_fluteVoice != null) return;

                Debug.LogWarning("[FluteTool] ProceduralMusicController found but " +
                                 "GetPlayerFluteVoice() returned null — falling back to standalone host.");
            }

            // 2. & 3. Find or create the standalone audio host (b-test / no procedural music)
            var host = StandaloneFluteAudioHost.Instance
                       ?? FindObjectOfType<StandaloneFluteAudioHost>();

            if (host == null)
            {
                var go = new GameObject("FluteAudioHost");
                // RequireComponent ensures the AudioSource is added automatically.
                host = go.AddComponent<StandaloneFluteAudioHost>();
            }

            host.Volume = StandaloneVolume;
            _fluteVoice = host.Voice;

            if (_fluteVoice == null)
                Debug.LogWarning("[FluteTool] StandaloneFluteAudioHost has no voice.");
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.25f);
            float worldR = RingRadius * 0.01f; // canvas units → world units at 0.01 scale
            for (int i = 0; i < 8; i++)
            {
                float a = (StartAngleDeg - i * 45f) * Mathf.Deg2Rad;
                Gizmos.DrawWireSphere(
                    transform.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * worldR, 0.08f);
            }
        }
#endif
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Standalone audio host
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal self-contained audio host for the player's flute, used when
    /// ProceduralMusicController is absent from the scene (b-test setups
    /// where procedural music is disabled).
    ///
    /// Owns a single LegatoLead VoiceManager and renders it into Unity's audio
    /// output via OnAudioFilterRead. A tiny silent looping clip on the
    /// AudioSource keeps the filter callback firing every audio buffer.
    ///
    /// Spawned automatically by FluteTool — no manual scene setup required.
    /// Existing instances in the scene are reused.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class StandaloneFluteAudioHost : MonoBehaviour
    {
        public static StandaloneFluteAudioHost Instance { get; private set; }

        [Tooltip("Output volume for the standalone flute host. Overwritten by FluteTool " +
                 "when it spawns the host, but you can pin it here for manually-placed hosts.")]
        [Range(0f, 1f)]
        public float Volume = 0.7f;

        /// <summary>The VoiceManager this host renders. Call NoteOn / NoteOff to play.</summary>
        public VoiceManager Voice => _voice;

        private VoiceManager _voice;
        private float _sampleRate;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A host already exists — destroy this duplicate but leave its
                // GameObject alone in case other components share it.
                Destroy(this);
                return;
            }
            Instance = this;

            _sampleRate = AudioSettings.outputSampleRate;
            _voice = new VoiceManager(InstrumentPreset.LegatoMelody, _sampleRate);

            // Drive the AudioSource with a tiny silent clip so OnAudioFilterRead
            // is invoked every audio buffer. The actual audio is generated
            // procedurally inside that callback.
            var src = GetComponent<AudioSource>();
            if (src.clip == null)
                src.clip = AudioClip.Create("FluteHostSilence", 1, 1, (int)_sampleRate, false);
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;  // 2D — uniform volume regardless of camera position
            src.Play();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Runs on the AUDIO thread — do NOT call Unity APIs here. Pure math only.
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (_voice == null) return;

            int sampleFrames = data.Length / channels;
            for (int i = 0; i < sampleFrames; i++)
            {
                _voice.GetStereoSample(out float l, out float r);
                data[i * channels] += l * Volume;
                if (channels > 1)
                    data[i * channels + 1] += r * Volume;
            }
        }
    }
}