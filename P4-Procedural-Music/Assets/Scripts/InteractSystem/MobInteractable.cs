using UnityEngine;
using InteractionSystem;
using InventorySystem.Tools;

namespace MobSystem
{
    /// <summary>
    /// Makes a mob clickable through the InteractionDetector system.
    /// When the player hovers a mob and left-clicks, MoveToInteractState
    /// walks the player over, then Interact() fires and attacks the mob
    /// using ToolUseSystem's damage logic.
    ///
    /// Works alongside GnomeInteractable — both go through the same
    /// InteractionDetector → MoveToInteractState → Interact() pipeline.
    /// Clicking a gnome opens trade; clicking a wolf walks over and swings.
    ///
    /// The existing TryHitMob() (swing at whatever is in front of you)
    /// still works for when the player is already in range.
    ///
    /// SETUP:
    ///   Added automatically by MobController — no manual setup needed.
    ///   Just add the Mob layer to InteractionDetector's interactableLayer mask.
    /// </summary>
    public class MobInteractable : Interactable
    {
        private MobController _mob;

        /// <summary>The mob this interactable represents.</summary>
        public MobController Mob => _mob;

        private void Awake()
        {
            _mob = GetComponent<MobController>();
        }

        /// <summary>
        /// Configure the interactable from MobData. Called by MobController.
        /// </summary>
        public void Configure(MobController mob)
        {
            _mob = mob;
            SetActionVerb("Attack");
            SetInteractRange(30f);
        }

        public override void Interact(PlayerStateManager player)
        {
            if (_mob == null || _mob.CurrentHealth <= 0) return;

            // Find ToolUseSystem on the player and delegate the attack
            var toolUse = player.GetComponent<ToolUseSystem>();
            if (toolUse != null)
            {
                toolUse.AttackMob(_mob);
            }
            else
            {
                Debug.LogWarning("[MobInteractable] No ToolUseSystem found on player!");
            }
        }

        public override bool CanInteract()
        {
            // Can't attack a dead mob (also handles destroyed objects)
            return _mob != null && _mob.CurrentHealth > 0;
        }
    }
}
