using UnityEngine;
using UnityEngine.InputSystem;
using ProceduralMusic.Core;
using ProceduralMusic.Bridge;
using ProceduralMusic.Synthesis;

namespace InventorySystem.Tools
{
    /// <summary>
    /// Flute instrument — plays notes directly through the procedural synth engine.
    ///
    /// NO audio clips needed. NO button prefabs. NO Canvas UI.
    ///
    /// HOW IT WORKS:
    ///   - ToolUseSystem calls OnFluteUsed() to toggle the ring open/closed.
    ///   - Opening the ring enters PlayerMusicPlayingState, locking movement.
    ///   - 8 scale notes from the live key are arranged in a circle around the player.
    ///   - Moving the cursor (mouse or right stick) over a note plays it immediately.
    ///     Moving to a different note stops the old one and starts the new one.
    ///     Moving off all notes stops sound.
    ///   - Closing the ring returns to PlayerIdleState.
    ///   - Sound goes directly to the LegatoMelody VoiceManager (index 1) in
    ///     ProceduralMusicController — no AudioClips required.
    ///   - The ring is drawn with GL in OnRenderObject (world-space, no Canvas).
    ///
    /// SETUP:
    ///   1. Add this component to the Player GameObject (same as PlayerStateManager).
    ///   2. Assign PlayerCamera (or leave null — finds Camera.main).
    ///   3. Assign StateManager (or leave null — auto-found on the same GameObject).
    ///   4. Assign PointAction — bind to <Mouse>/position + <Gamepad>/rightStick.
    ///      Set action type to Value, control type Vector2.
    ///   6. In ToolUseSystem, assign this component to the fluteTool field.
    /// </summary>
    public class FluteTool : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Main camera — used to project the point action into world space. Finds Camera.main if null.")]
        public Camera PlayerCamera;

        [Tooltip("PlayerStateManager on the player. Auto-found on the same GameObject if left empty.")]
        public PlayerStateManager StateManager;

        [Header("Input Actions")]
        [Tooltip("Tracks the cursor/stick position. Bind to <Mouse>/position AND <Gamepad>/rightStick.\n" +
                 "Set action type to Value, control type to Vector2.")]
        [SerializeField] private InputAction PointAction = new InputAction(
            "FlutePoint", InputActionType.Value, expectedControlType: "Vector2");

        [Header("Ring Layout")]
        [Tooltip("Radius of the note ring in world units.")]
        public float RingRadius = 2f;

        [Tooltip("Radius of each note dot in world units.")]
        public float NoteRadius = 0.35f;

        [Tooltip("Angle for the first note, in degrees. 90 = top of ring.")]
        public float StartAngleDeg = 90f;

        [Tooltip("Which MIDI octave the lowest ring note plays in. 4 = middle C octave.")]
        [Range(3, 6)]
        public int BaseOctave = 4;

        [Header("Music Ducking")]
        [Tooltip("Master volume of the music while the flute ring is open (0 = silence, 1 = full).")]
        [Range(0f, 1f)]
        public float DuckedVolume = 0.35f;

        [Header("Colors")]
        public Color RingLineColor    = new Color(0.65f, 0.88f, 1.00f, 0.45f);
        public Color NoteIdleColor    = new Color(0.80f, 0.93f, 1.00f, 0.80f);
        public Color NoteHoverColor   = new Color(1.00f, 1.00f, 0.65f, 1.00f);
        public Color NotePlayingColor = new Color(0.45f, 1.00f, 0.70f, 1.00f);

        // ── Runtime ───────────────────────────────────────────────────────

        private bool      _isOpen      = false;
        private int       _hoveredNote = -1;
        private int       _activeNote  = -1;
        private int[]     _midiNotes    = new int[8];
        private string[]  _noteNames    = new string[8];
        private Vector3[] _noteWorldPos = new Vector3[8];

        // Dedicated player flute voice — separate from the composition engine's lead voice
        private VoiceManager _fluteVoice;

        private static readonly string[] NoteTable =
            { "C","C#","D","D#","E","F","F#","G","G#","A","A#","B" };

        // ─────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (PlayerCamera == null)
                PlayerCamera = Camera.main;

            if (StateManager == null)
                StateManager = GetComponent<PlayerStateManager>();

            // Default bindings if none are set in the Inspector
            if (PointAction.bindings.Count == 0)
            {
                PointAction.AddBinding("<Mouse>/position");
                PointAction.AddBinding("<Gamepad>/rightStick")
                    .WithProcessor("scaleVector2(x=500,y=500)");
            }

        }

        void OnDestroy()
        {
            PointAction.Disable();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Toggle the ring open or closed. Called by ToolUseSystem.</summary>
        public void OnFluteUsed()
        {
            if (_isOpen) CloseRing();
            else         OpenRing();
        }

        /// <summary>Force-close the ring (player unequips, opens inventory, etc).</summary>
        public void ForceClose()
        {
            if (_isOpen) CloseRing();
        }

        // ── Open / Close ──────────────────────────────────────────────────

        void OpenRing()
        {
            _isOpen = true;
            CacheFluteVoice();
            RebuildNoteData();
            UpdateNotePositions();

            PointAction.Enable();

            StateManager?.StartMusicPlaying();

            // Duck the background music while playing
            if (ProceduralMusicController.Instance != null)
                ProceduralMusicController.Instance.MasterVolume = DuckedVolume;
        }

        void CloseRing()
        {
            _isOpen      = false;
            _hoveredNote = -1;
            StopCurrentNote();

            PointAction.Disable();

            // Restore music volume
            if (ProceduralMusicController.Instance != null)
                ProceduralMusicController.Instance.MasterVolume = 0.7f;

            StateManager?.StopMusicPlaying();
        }

        // ── Update ────────────────────────────────────────────────────────

        void Update()
        {
            if (!_isOpen) return;

            UpdateNotePositions();
            UpdateHover();

            // Play whichever note is hovered — stop when cursor leaves all notes
            if (_hoveredNote != _activeNote)
            {
                if (_hoveredNote >= 0)
                    PlayNote(_hoveredNote);
                else
                    StopCurrentNote();
            }
        }

        void UpdateNotePositions()
        {
            for (int i = 0; i < 8; i++)
            {
                float deg = StartAngleDeg - i * 45f;
                float rad = deg * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * RingRadius;
                _noteWorldPos[i] = transform.position + new Vector3(offset.x, offset.y, 0f);
            }
        }

        void UpdateHover()
        {
            if (PlayerCamera == null) return;

            Vector2 rawPoint = PointAction.ReadValue<Vector2>();
            Vector3 worldPoint;

            // Mouse: screen-space pixel position → world
            // Gamepad stick: delta from player center, already scaled to world units
            if (IsMouseDriving())
            {
                float depth = Mathf.Abs(PlayerCamera.transform.position.z - transform.position.z);
                worldPoint = PlayerCamera.ScreenToWorldPoint(
                    new Vector3(rawPoint.x, rawPoint.y, depth));
                worldPoint.z = 0f;
            }
            else
            {
                // Right stick: treat as offset from player
                worldPoint = transform.position + new Vector3(rawPoint.x, rawPoint.y, 0f);
            }

            float best    = float.MaxValue;
            int   bestIdx = -1;
            for (int i = 0; i < 8; i++)
            {
                float d = Vector2.Distance(worldPoint, _noteWorldPos[i]);
                if (d < best) { best = d; bestIdx = i; }
            }

            _hoveredNote = (best < NoteRadius * 5f) ? bestIdx : -1;
        }

        /// <summary>
        /// Heuristic: if the last active control for PointAction is a mouse position,
        /// we need to unproject from screen space. A gamepad stick is already a Vector2
        /// in [-1,1] space that we scale to world units.
        /// </summary>
        bool IsMouseDriving()
        {
            var control = PointAction.activeControl;
            if (control == null) return true; // default to mouse
            return control.path.Contains("Mouse") || control.path.Contains("Pointer");
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
            ProceduralMusic.Core.Key   key     = GetCurrentKey();
            int[] degrees = key.GetScaleDegrees(); // 7 semitone offsets from root
            int   root    = (BaseOctave + 1) * 12 + (int)key.Root;

            for (int i = 0; i < 7; i++)
            {
                _midiNotes[i] = root + degrees[i];
                _noteNames[i] = NoteTable[((int)key.Root + degrees[i]) % 12];
            }

            // 8th note = octave root
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
            return new ProceduralMusic.Core.Key(ProceduralMusic.Core.PitchClass.C, ProceduralMusic.Core.MusicalMode.Major);
        }

        void CacheFluteVoice()
        {
            _fluteVoice = null;

            var controller = ProceduralMusicController.Instance
                             ?? FindFirstObjectByType<ProceduralMusicController>();

            if (controller == null)
            {
                Debug.LogWarning("[FluteTool] ProceduralMusicController not found in scene.");
                return;
            }

            _fluteVoice = controller.GetPlayerFluteVoice();

            if (_fluteVoice == null)
                Debug.LogWarning("[FluteTool] GetPlayerFluteVoice() returned null.");
        }

        // ── Rendering ─────────────────────────────────────────────────────
        // Drawn via OnGUI using WorldToScreenPoint — works reliably in all
        // 2D setups without any camera matrix or GL setup required.

        void OnGUI()
        {
            if (!_isOpen || PlayerCamera == null) return;

            // Draw ring outline as a series of line segments between note positions
            // (OnGUI has no line primitive so we connect the dots with labels)

            for (int i = 0; i < 8; i++)
            {
                Vector3 sp = PlayerCamera.WorldToScreenPoint(_noteWorldPos[i]);
                sp.y = Screen.height - sp.y; // flip Y — GUI origin is top-left
                if (sp.z < 0) continue;      // behind camera

                bool isActive  = (i == _activeNote);
                bool isHovered = (i == _hoveredNote);

                Color c = isActive  ? NotePlayingColor
                        : isHovered ? NoteHoverColor
                        : NoteIdleColor;

                float sz = (isHovered || isActive) ? 70f : 54f;

                // Draw dot
                GUI.color = c;
                GUI.DrawTexture(
                    new Rect(sp.x - sz * 0.5f, sp.y - sz * 0.5f, sz, sz),
                    Texture2D.whiteTexture);

                // Draw note name label on top
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment  = TextAnchor.MiddleCenter,
                    fontSize   = isHovered || isActive ? 14 : 11,
                    fontStyle  = FontStyle.Bold
                };
                GUI.color = Color.black;
                GUI.Label(new Rect(sp.x - 20f, sp.y - 10f, 40f, 20f), _noteNames[i], style);
            }

            // Draw connecting ring lines between adjacent dots
            if (Event.current.type == EventType.Repaint)
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector3 a = PlayerCamera.WorldToScreenPoint(_noteWorldPos[i]);
                    Vector3 b = PlayerCamera.WorldToScreenPoint(_noteWorldPos[(i + 1) % 8]);
                    a.y = Screen.height - a.y;
                    b.y = Screen.height - b.y;
                    if (a.z < 0 || b.z < 0) continue;

                    DrawGUILine(new Vector2(a.x, a.y), new Vector2(b.x, b.y), RingLineColor, 2f);
                }
            }

            GUI.color = Color.white;
        }

        // Draws a line in OnGUI using a 1x1 white texture rotated and scaled.
        void DrawGUILine(Vector2 from, Vector2 to, Color color, float width)
        {
            Vector2 dir    = (to - from).normalized;
            float   length = Vector2.Distance(from, to);
            float   angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 mid    = (from + to) * 0.5f;

            GUIUtility.RotateAroundPivot(angle, mid);
            GUI.color = color;
            GUI.DrawTexture(new Rect(mid.x - length * 0.5f, mid.y - width * 0.5f, length, width),
                            Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, mid);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, RingRadius);
            for (int i = 0; i < 8; i++)
            {
                float a = (StartAngleDeg - i * 45f) * Mathf.Deg2Rad;
                Gizmos.DrawWireSphere(
                    transform.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius,
                    NoteRadius);
            }
        }
#endif
    }
}