using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Base component for anything the player can interact with in the world.
    /// Attach a subclass (PickupInteractable, HarvestInteractable, etc.) to
    /// the GameObject alongside a Collider2D on the "Interactable" layer.
    ///
    /// The InteractionDetector on the player finds these via mouse hover,
    /// shows the action prompt, and triggers Interact() after walking in range.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("Verb shown in the prompt above this object (e.g. 'Pick Up', 'Chop', 'Open').")]
        [SerializeField] private string actionVerb = "Interact";

        [Tooltip("How close the player must be to perform the interaction.")]
        [SerializeField] private float interactRange = 1.5f;

        [Tooltip("World-space offset from pivot for the interact range center. " +
                 "Use when the object's pivot isn't at its visual center " +
                 "(e.g. mob pivot at feet, but interact zone should be at body).")]
        [SerializeField] private Vector2 interactCenterOffset = Vector2.zero;

        [Tooltip("Offset from pivot where the prompt appears (world space, added to transform.position).")]
        [SerializeField] private Vector2 promptOffset = new Vector2(0f, 1.2f);

        /// <summary>The verb displayed in the UI prompt.</summary>
        public string ActionVerb => actionVerb;

        /// <summary>Max distance from player to interact.</summary>
        public float InteractRange => interactRange;

        /// <summary>
        /// World position of the interact zone center. Distance checks in
        /// PlayerMoveToInteractState measure from the player to this point.
        /// Defaults to transform.position when interactCenterOffset is zero.
        /// </summary>
        public Vector3 InteractCenter => transform.position + (Vector3)interactCenterOffset;

        /// <summary>World position where the prompt UI should appear.</summary>
        public virtual Vector3 PromptPosition => transform.position + (Vector3)promptOffset;

        /// <summary>Change the action verb at runtime (e.g. MobInteractable sets "Attack").</summary>
        public void SetActionVerb(string verb) => actionVerb = verb;

        /// <summary>Change the interact range at runtime.</summary>
        public void SetInteractRange(float range) => interactRange = Mathf.Max(0f, range);

        /// <summary>
        /// Called when the player arrives in range and completes the interaction.
        /// Implement per-type logic (pickup, harvest, open UI, etc.).
        /// </summary>
        /// <param name="player">The player's state manager.</param>
        public abstract void Interact(PlayerStateManager player);

        /// <summary>
        /// Override to return false if this object can no longer be interacted with
        /// (e.g. depleted resource, already picked up). The detector will skip it.
        /// </summary>
        public virtual bool CanInteract() => true;

        /// <summary>
        /// Override to return true to skip the walk-to-interact step. Used when
        /// the interaction is meant to fire from the player's current position
        /// (e.g. fishing — the player casts from where they stand, not where the
        /// water is). When true, ToolUseSystem calls Interact() directly on click
        /// instead of switching to moveToInteractState.
        /// </summary>
        public virtual bool InteractImmediately => false;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(InteractCenter, interactRange);

            // Small marker at the interact center itself
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(InteractCenter, 0.08f);

            // Prompt position
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(PromptPosition, 0.08f);
        }
#endif
    }
}