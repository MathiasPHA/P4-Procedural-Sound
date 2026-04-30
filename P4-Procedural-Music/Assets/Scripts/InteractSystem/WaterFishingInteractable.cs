using UnityEngine;
using InventorySystem;
using InventorySystem.Data;
using InventorySystem.Tools;
using FishingSystem;

namespace InteractionSystem
{
    /// <summary>
    /// Interactable wrapper for a chunk's water collision GameObject.
    /// Lets the player fish by hovering water with a fishing rod equipped
    /// and clicking — reusing your existing Interactable / InteractionDetector
    /// / InteractionPromptUI flow.
    ///
    /// CanInteract() returns false when no fishing rod is equipped, so the
    /// prompt only appears in the right context. Interact() switches the
    /// player into PlayerFishingState directly (no walk-to-interact step —
    /// fishing happens from where the player is standing).
    ///
    /// PromptPosition is overridden to follow the cursor so the "Fish" text
    /// appears over the actual water tile being hovered, not at a fixed point
    /// on a chunk-sized collider.
    /// </summary>
    public class WaterFishingInteractable : Interactable
    {
        [Header("Fishing Rod Detection")]
        [Tooltip("ToolType value used by the fishing rod. Matches FishingRod_TD's toolType (8).")]
        [SerializeField] private ToolType fishingRodToolType = (ToolType)8;

        // Cached so we can position the prompt over the cursor.
        private Camera _cam;
        private Vector3 _cursorWorld;
        private static ToolUseSystem _playerTools; // cached after first lookup

        private void Awake()
        {
            _cam = Camera.main;
            SetActionVerb("Fish");
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Mouse.current == null || _cam == null) return;
            Vector2 screen = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Vector3 world = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            world.z = 0f;
            _cursorWorld = world;
        }

        // Show the prompt over the cursor, not at the chunk's pivot.
        public override Vector3 PromptPosition => _cursorWorld + new Vector3(0f, 0.6f, 0f);

        // Skip walk-to-interact — fishing happens from the player's current position.
        public override bool InteractImmediately => true;

        // Only valid when a fishing rod is equipped — otherwise the prompt is hidden.
        public override bool CanInteract()
        {
            if (FishingManager.Instance == null) return false;
            if (FishingManager.Instance.IsFishing) return false;

            var toolData = GetEquippedToolData();
            return toolData != null && toolData.toolType == fishingRodToolType;
        }

        public override void Interact(PlayerStateManager player)
        {
            if (!CanInteract()) return;

            // Fishing happens at the cursor, not at the player.
            player.fishingState.CastPosition = _cursorWorld;
            player.SwitchState(player.fishingState);
        }

        // Pulls the equipped ToolData via the player's ToolUseSystem.
        // Cached after first lookup since the player doesn't change scenes.
        private static ToolData GetEquippedToolData()
        {
            if (_playerTools == null)
                _playerTools = Object.FindObjectOfType<ToolUseSystem>();
            return _playerTools != null ? _playerTools.EquippedToolData : null;
        }
    }
}