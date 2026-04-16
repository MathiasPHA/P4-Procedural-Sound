using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using ProceduralMusic.Core;
using ProceduralMusic.Bridge;
using InventorySystem.Data;

namespace InventorySystem.Tools
{
    /// <summary>
    /// Attach this MonoBehaviour to a GameObject in the scene (e.g. the Player or a FluteManager object).
    /// When the player equips the flute ToolData item and right-clicks (or presses the use key),
    /// it opens the note ring UI — 8 scale notes arranged in a circle around the player.
    /// Each note plays an AudioClip from the ToolData's instrumentSounds list, pitched so it
    /// stays in the current key and mode of the ProceduralMusicController.
    ///
    /// SETUP:
    ///   1. Add this component to your Player (or a dedicated FluteManager GameObject).
    ///   2. Assign the FluteData ToolData ScriptableObject.
    ///   3. Assign the NoteButtonPrefab (a world-space UI button with a SpriteRenderer or UI Image).
    ///   4. Assign the Player transform so buttons orbit it.
    ///   5. The AudioSource is auto-added.
    ///
    /// HOW THE KEY SYNC WORKS:
    ///   ProceduralMusicController exposes GetComposer().CurrentKey which gives us the live Key
    ///   (root PitchClass + MusicalMode). We read its GetScaleDegrees() to build 8 pitches
    ///   (scale degrees 0–6 across one octave, then the octave root), then pitch-shift a single
    ///   base AudioClip using AudioSource.pitch to land on each semitone relative to base.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FluteTool : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────
        [Header("Flute Data")]
        [Tooltip("The ToolData ScriptableObject for the flute item.")]
        public ToolData FluteData;

        [Header("References")]
        [Tooltip("The player transform — note buttons orbit this position.")]
        public Transform Player;

        [Tooltip("Prefab instantiated for each note button. Should have a NoteButton component.")]
        public GameObject NoteButtonPrefab;

        [Header("Ring Layout")]
        [Tooltip("Radius of the note circle (world units).")]
        public float RingRadius = 1.8f;

        [Tooltip("Sorting order used on spawned note buttons so they stay visible above world sprites.")]
        public int NoteSortingOrder = 100;

        [Tooltip("Angle offset so the first note starts at the top (degrees).")]
        public float StartAngleDeg = 90f;

        [Header("Pitching")]
        [Tooltip("MIDI note number the base AudioClip is recorded at (default 60 = C4).")]
        public int BaseClipMidiNote = 60;

        [Tooltip("Which octave the lowest note of the ring plays in.")]
        [Range(3, 6)]
        public int BaseOctave = 4;

        [Header("Animation")]
        [Tooltip("Time (seconds) for buttons to scale in/out.")]
        public float AnimationDuration = 0.18f;

        // ─── Runtime ─────────────────────────────────────────────────────
        private AudioSource _audioSource;
        private List<GameObject> _noteButtons = new List<GameObject>();
        private bool _isOpen = false;
        private ToolData _activeInstrumentData;
        private PlayerStateManager _playerStateManager;
        private bool _didOverrideGameMusic;

        // Cached key data rebuilt each time ring opens
        private int[] _midiNotes = new int[8];
        private string[] _noteNames = new string[8];

        private static readonly string[] NoteNameTable =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        // ─────────────────────────────────────────────────────────────────

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D audio

            // Default orbit target to this transform when Player isn't assigned.
            if (Player == null)
                Player = transform;

            _playerStateManager = GetComponent<PlayerStateManager>();
            if (_playerStateManager == null)
                _playerStateManager = GetComponentInParent<PlayerStateManager>();
            if (_playerStateManager == null && Player != null)
                _playerStateManager = Player.GetComponent<PlayerStateManager>();
        }

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Call this from your ToolUseSystem / equip logic when the player uses the flute.
        /// Toggles the note ring open/closed.
        /// </summary>
        public void OnFluteUsed()
        {
            if (_isOpen)
                CloseRing();
            else
                OpenRing();
        }

        /// <summary>
        /// Sets which instrument data this ring should use for note playback.
        /// If null, FluteData is used as fallback.
        /// </summary>
        public void SetActiveInstrumentData(ToolData toolData)
        {
            _activeInstrumentData = toolData;
        }

        /// <summary>
        /// Force-close the ring (e.g. when player unequips, takes damage, opens inventory).
        /// </summary>
        public void ForceClose()
        {
            if (_isOpen) CloseRing();
        }

        // ─── Ring Open/Close ──────────────────────────────────────────────

        void OpenRing()
        {
            if (NoteButtonPrefab == null)
            {
                Debug.LogWarning("FluteTool: Note ring did not open because NoteButtonPrefab is missing.");
                return;
            }

            if (Player == null)
            {
                Debug.LogWarning("FluteTool: Note ring did not open because Player transform is missing.");
                return;
            }

            _isOpen = true;

            if (_playerStateManager != null)
                _playerStateManager.StartMusicPlaying();

            if (GameStateManager.Instance != null && !GameStateManager.Instance.IsManualOverride)
            {
                GameStateManager.Instance.EnterCozy();
                _didOverrideGameMusic = true;
            }

            if (EventSystem.current == null)
                Debug.LogWarning("FluteTool: No EventSystem found. Ring appears, but pointer interactions may fail.");

            RebuildNoteData();
            SpawnButtons();
        }

        void CloseRing()
        {
            _isOpen = false;

            if (_playerStateManager != null)
                _playerStateManager.StopMusicPlaying();

            if (_didOverrideGameMusic && GameStateManager.Instance != null && GameStateManager.Instance.IsManualOverride)
            {
                GameStateManager.Instance.ReturnToAuto();
                _didOverrideGameMusic = false;
            }

            StartCoroutine(DespawnButtons());
        }

        // ─── Note Data ────────────────────────────────────────────────────

        /// <summary>
        /// Reads the live Key from ProceduralMusicController and builds 8 MIDI notes:
        /// scale degrees 0–6 (one full scale) + the octave root.
        /// </summary>
        void RebuildNoteData()
        {
            Key currentKey = GetCurrentKey();
            int[] degrees = currentKey.GetScaleDegrees(); // 7 intervals, semitone offsets from root
            int rootMidi = ((BaseOctave + 1) * 12) + (int)currentKey.Root;

            for (int i = 0; i < 7; i++)
            {
                _midiNotes[i] = rootMidi + degrees[i];
                _noteNames[i] = NoteNameTable[(int)currentKey.Root + degrees[i] < 12
                    ? (int)currentKey.Root + degrees[i]
                    : ((int)currentKey.Root + degrees[i]) % 12];
            }

            // 8th note = octave root
            _midiNotes[7] = rootMidi + 12;
            _noteNames[7] = NoteNameTable[(int)currentKey.Root];
        }

        Key GetCurrentKey()
        {
            if (ProceduralMusicController.Instance != null)
            {
                var composer = ProceduralMusicController.Instance.GetComposer();
                if (composer != null)
                    return composer.CurrentKey;
            }
            // Fallback: C Major
            return new Key(PitchClass.C, MusicalMode.Major);
        }

        // ─── Button Spawning ──────────────────────────────────────────────

        void SpawnButtons()
        {
            if (NoteButtonPrefab == null || Player == null)
            {
                Debug.LogWarning("FluteTool: NoteButtonPrefab or Player not assigned.");
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                Vector3 pos = GetButtonPosition(i);
                GameObject btn = Instantiate(NoteButtonPrefab, pos, Quaternion.identity);
                btn.transform.SetParent(Player, worldPositionStays: true);

                var spriteRenderer = btn.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                    spriteRenderer.sortingOrder = NoteSortingOrder;

                // Configure the NoteButton component
                var noteBtn = btn.GetComponent<NoteButton>();
                if (noteBtn != null)
                {
                    int noteIndex = i; // Capture for lambda
                    int midiNote = _midiNotes[i];
                    string noteName = _noteNames[i];

                    noteBtn.Setup(noteName, () => PlayNote(midiNote));
                }
                else
                {
                    Debug.LogWarning($"FluteTool: NoteButtonPrefab is missing a NoteButton component on button {i}.");
                }

                // Scale-in animation
                btn.transform.localScale = Vector3.zero;
                StartCoroutine(ScaleTo(btn.transform, Vector3.one, AnimationDuration));

                _noteButtons.Add(btn);
            }
        }

        IEnumerator DespawnButtons()
        {
            // Scale all buttons out simultaneously
            List<Coroutine> anims = new List<Coroutine>();
            foreach (var btn in _noteButtons)
                if (btn != null)
                    anims.Add(StartCoroutine(ScaleTo(btn.transform, Vector3.zero, AnimationDuration)));

            yield return new WaitForSeconds(AnimationDuration);

            foreach (var btn in _noteButtons)
                if (btn != null) Destroy(btn);
            _noteButtons.Clear();
        }

        Vector3 GetButtonPosition(int index)
        {
            float angleDeg = StartAngleDeg - (index * 360f / 8f);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * RingRadius;
            return Player.position + new Vector3(offset.x, offset.y, 0f);
        }

        // ─── Audio Playback ───────────────────────────────────────────────

        /// <summary>
        /// Plays the flute sound pitched to the target MIDI note.
        /// Pitch-shifts the base AudioClip using AudioSource.pitch (ratio of target/base frequency).
        /// </summary>
        void PlayNote(int midiNote)
        {
            ToolData activeData = _activeInstrumentData != null ? _activeInstrumentData : FluteData;

            if (activeData == null || activeData.instrumentSounds == null || activeData.instrumentSounds.Count == 0)
            {
                Debug.LogWarning("FluteTool: No instrument sounds assigned in ToolData.");
                return;
            }

            // Pick the base clip (use the first clip, or randomise if multiple)
            AudioClip clip = activeData.instrumentSounds.Count == 1
                ? activeData.instrumentSounds[0]
                : activeData.instrumentSounds[Random.Range(0, activeData.instrumentSounds.Count)];

            // Pitch ratio: 2^(semitones/12)
            int semitoneDiff = midiNote - BaseClipMidiNote;
            float pitchRatio = Mathf.Pow(2f, semitoneDiff / 12f);

            // Apply small random variation from ToolData
            float variation = Random.Range(-activeData.instrumentPitchVariation, activeData.instrumentPitchVariation);
            pitchRatio *= Mathf.Pow(2f, variation);

            _audioSource.pitch = pitchRatio;
            _audioSource.volume = activeData.instrumentVolume;
            _audioSource.PlayOneShot(clip);
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        IEnumerator ScaleTo(Transform t, Vector3 target, float duration)
        {
            if (t == null) yield break;
            Vector3 start = t.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            if (t != null) t.localScale = target;
        }

        void Update()
        {
            // Keep buttons orbiting the player if they move
            if (_isOpen && Player != null)
            {
                for (int i = 0; i < _noteButtons.Count; i++)
                {
                    if (_noteButtons[i] != null)
                        _noteButtons[i].transform.position = GetButtonPosition(i);
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (Player == null) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.4f);
            // Draw the ring
            int segments = 64;
            Vector3 prev = Player.position + new Vector3(RingRadius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * 2f * Mathf.PI / segments;
                Vector3 next = Player.position + new Vector3(Mathf.Cos(a) * RingRadius, Mathf.Sin(a) * RingRadius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
            // Draw note positions
            Gizmos.color = Color.cyan;
            for (int i = 0; i < 8; i++)
                Gizmos.DrawSphere(GetButtonPosition(i), 0.1f);
        }
#endif
    }
}
