using UnityEngine;
using InventorySystem.Harvesting;
using InventorySystem.Tools;

namespace InteractionSystem
{
    /// <summary>
    /// Interactable wrapper for HarvestableResource.
    /// When the player arrives in range, automatically triggers one harvest swing
    /// via ToolUseSystem — no second click needed.
    ///
    /// SETUP:
    ///   1. Attach alongside HarvestableResource on the same GameObject
    ///   2. Set actionVerb to "Chop" / "Mine" / "Pick Up" as appropriate
    ///   3. Ensure Collider2D is on the "Interactable" layer
    ///   4. ToolUseSystem must be on the player
    /// </summary>
    [RequireComponent(typeof(HarvestableResource))]
    public class HarvestInteractable : Interactable
    {
        private HarvestableResource _resource;

        private void Awake()
        {
            _resource = GetComponent<HarvestableResource>();
        }

        public override bool CanInteract()
        {
            return _resource != null && !_resource.IsDepleted;
        }

        public override void Interact(PlayerStateManager player)
        {
            // Face the player left or right toward the resource
            // (harvest animations only have left/right variants)
            float xDiff = transform.position.x - player.transform.position.x;
            player.playerDir = xDiff < 0 ? "Left" : "Right";

            // Trigger the swing immediately
            var toolUse = player.GetComponent<ToolUseSystem>();
            if (toolUse != null)
                toolUse.HarvestResource(_resource);
        }
    }
}