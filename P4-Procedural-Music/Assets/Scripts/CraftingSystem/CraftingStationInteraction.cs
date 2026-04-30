using UnityEngine;
using InventorySystem.UI;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Tracks the player's presence in a crafting station's interaction range
    /// and updates CraftingUIManager.ActiveStation accordingly. Two supported
    /// modes:
    ///
    ///   1. PROXIMITY MODE  (requireInteract = false)
    ///      Entering the trigger auto-activates the station; leaving clears it.
    ///      Used by stations that should open the panel just by being near them
    ///      (Valheim-style benches, simple cooking pots, etc.).
    ///
    ///   2. CLICK + LEASH MODE  (requireInteract = true)
    ///      Entering the trigger does NOT open the panel — opening is handled
    ///      by a separate click-to-interact path (WorldStationInteractable +
    ///      OpenCraftingUIAction). This component just acts as a "leash":
    ///      when the player walks out of range, it clears the station so the
    ///      crafting panel reverts to hand-craft instead of leaving the world
    ///      station active forever.
    ///
    ///      Use this on workbenches, forges, or any station whose UI is
    ///      opened by a deliberate click rather than by proximity.
    ///
    /// SETUP:
    ///   1. Add a BaseCraftingStation subclass to your station GameObject
    ///      (e.g. a Workbench with StationType = Workbench).
    ///   2. Add a trigger Collider2D (circle or box) sized to the interaction range.
    ///   3. Attach this script.
    ///   4. Wire the station and craftingUIManager references (auto-found if left empty).
    ///   5. Tag the player GameObject as "Player".
    ///   6. For click-opened stations, tick "Require Interact" so the trigger
    ///      doesn't auto-activate on enter — it'll only deactivate on exit.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CraftingStationInteraction : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The crafting station component on this GameObject. " +
                 "Auto-resolved from this GameObject if left empty.")]
        [SerializeField] private BaseCraftingStation station;

        [Tooltip("Reference to the CraftingUIManager in the scene. " +
                 "Auto-resolved if left empty.")]
        [SerializeField] private CraftingUIManager craftingUIManager;

        [Header("Mode")]
        [Tooltip("If true, entering the trigger does NOT open the panel — only " +
                 "leaving it clears the active station (leash mode for click-opened " +
                 "stations like the workbench). If false, entering auto-activates " +
                 "the station (proximity mode, e.g. cooking pot).")]
        [SerializeField] private bool requireInteract = false;

        [Header("Settings")]
        [Tooltip("Tag used to identify the player. Must match the player's tag.")]
        [SerializeField] private string playerTag = "Player";

        private bool _playerInRange;

        private void Start()
        {
            if (station == null)
            {
                station = GetComponent<BaseCraftingStation>();
                if (station == null)
                    Debug.LogError($"[CraftingStationInteraction] No BaseCraftingStation " +
                                   $"found on {gameObject.name}!");
            }

            if (craftingUIManager == null)
            {
                craftingUIManager = FindAnyObjectByType<CraftingUIManager>();
                if (craftingUIManager == null)
                    Debug.LogError("[CraftingStationInteraction] CraftingUIManager not found in scene!");
            }

            // Ensure the collider is a trigger
            var col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[CraftingStationInteraction] Collider on {gameObject.name} " +
                                 "is not set as a trigger. Setting it now.");
                col.isTrigger = true;
            }
        }

        // =====================================================================
        // Proximity detection
        // =====================================================================

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = true;

            // Proximity mode: auto-open on enter.
            // Leash mode (requireInteract): do nothing — opening is handled elsewhere.
            if (!requireInteract)
            {
                ActivateStation();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = false;

            // Always clear on exit, regardless of mode. This is what makes
            // leash mode useful — even a click-opened station gets cleaned up
            // when the player walks away. DeactivateStation is guarded so it
            // only clears if this station is currently the active one.
            DeactivateStation();
        }

        // =====================================================================
        // Activation
        // =====================================================================

        /// <summary>
        /// Call this from your interact system if requireInteract is true and
        /// you want the trigger itself to also support a toggle interaction.
        /// (Most click-opened stations don't need this — they open via
        /// OpenCraftingUIAction instead.)
        /// </summary>
        public void InteractPressed()
        {
            if (!_playerInRange) return;

            if (craftingUIManager.ActiveStation == station)
                DeactivateStation();
            else
                ActivateStation();
        }

        private void ActivateStation()
        {
            if (craftingUIManager == null || station == null) return;

            craftingUIManager.SetActiveStation(station);
            Debug.Log($"[CraftingStation] Activated: {station.StationType}");
        }

        private void DeactivateStation()
        {
            if (craftingUIManager == null) return;

            // Only clear if WE are the current station. Prevents a workbench
            // from clearing the cooking pot's active state when the player
            // walks past one on the way out of the other.
            if (craftingUIManager.ActiveStation == station)
            {
                craftingUIManager.ClearStation();
                Debug.Log($"[CraftingStation] Deactivated: {station.StationType}");
            }
        }
    }
}