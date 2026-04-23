using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace InventorySystem.Tools
{
    /// <summary>
    /// Attach this to the NoteButton prefab used by FluteTool.
    ///
    /// Works in WORLD SPACE — this is a 2D sprite-based button, not a Canvas UI button,
    /// so it uses a Collider2D + IPointerClickHandler for interaction.
    ///
    /// PREFAB SETUP:
    ///   - SpriteRenderer (the circle/bubble visual)
    ///   - Collider2D (CircleCollider2D recommended, set as trigger)
    ///   - This NoteButton component
    ///   - Optional: TextMeshPro child object for the note name label
    ///
    /// If you're using a Canvas-based UI, swap IPointerClickHandler for a Button component
    /// and call Setup() from FluteTool the same way.
    /// </summary>
    public class NoteButton : MonoBehaviour, IPointerClickHandler
    {
        [Header("Visuals")]
        [Tooltip("The SpriteRenderer to tint on hover/press.")]
        public SpriteRenderer ButtonSprite;

        [Tooltip("Optional label showing the note name (e.g. 'D', 'F#').")]
        public TextMeshPro NoteLabel;

        [Header("Colors")]
        public Color NormalColor  = new Color(0.85f, 0.92f, 1.00f, 0.90f);
        public Color HoverColor   = new Color(1.00f, 1.00f, 1.00f, 1.00f);
        public Color PressedColor = new Color(0.60f, 0.85f, 1.00f, 1.00f);

        // ─── Runtime ──────────────────────────────────────────────────────
        private Action _onPlay;
        private bool _hovered = false;
        private float _lastTriggerTime = -1f;
        private const float TriggerDebounceSeconds = 0.05f;

        void Awake()
        {
            if (ButtonSprite == null)
                ButtonSprite = GetComponent<SpriteRenderer>();
        }

        // ─────────────────────────────────────────────────────────────────

        /// <summary>Called by FluteTool after spawning the button.</summary>
        public void Setup(string noteName, Action onPlay)
        {
            _onPlay = onPlay;
            if (NoteLabel != null)
                NoteLabel.text = noteName;
            SetColor(NormalColor);
        }

        // ─── Pointer Events ───────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            SetColor(PressedColor);
            TryTriggerPlay();
            // Brief flash back to hover after pressing
            Invoke(nameof(ResetToHover), 0.12f);
        }

        void OnMouseEnter()
        {
            _hovered = true;
            SetColor(HoverColor);
        }

        void OnMouseExit()
        {
            _hovered = false;
            SetColor(NormalColor);
        }

        void OnMouseDown()
        {
            SetColor(PressedColor);
            TryTriggerPlay();
        }

        void OnMouseUp()
        {
            SetColor(_hovered ? HoverColor : NormalColor);
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        void ResetToHover() => SetColor(_hovered ? HoverColor : NormalColor);

        void TryTriggerPlay()
        {
            if (_lastTriggerTime >= 0f && Time.unscaledTime - _lastTriggerTime < TriggerDebounceSeconds)
                return;

            _lastTriggerTime = Time.unscaledTime;
            _onPlay?.Invoke();
        }

        void SetColor(Color c)
        {
            if (ButtonSprite != null)
                ButtonSprite.color = c;
        }
    }
}
