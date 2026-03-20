using UnityEngine;
using InventorySystem.UI;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Handles player interaction with a crafting station in the world.
    /// When the player enters the trigger area, this station becomes the
    /// active station on the CraftingUIManager. When the player leaves,
    /// it reverts to hand-crafting.
    ///
    /// SETUP:
    ///   1. Add a BaseCraftingStation subclass to your station GameObject
    ///      (e.g. a Workbench with StationType = Workbench)
    ///   2. Add a trigger Collider2D (circle or box) for the interaction range
    ///   3. Attach this script
    ///   4. Wire the station and craftingUIManager references
    ///   5. Tag the player GameObject as "Player"
    ///
    /// INTERACTION MODES:
    ///   - Proximity (default): Entering the trigger auto-activates the station.
    ///     Good for Valheim-style where being near the bench is enough.
    ///   - Interact key: Set requireInteract to true. The station only activates
    ///     when the player presses E (or whatever your interact binding is).
    ///     You'd wire that up to your own interact system.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CraftingStationInteraction : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The crafting station component on this GameObject.")]
        [SerializeField] private BaseCraftingStation station;

        [Tooltip("Reference to the CraftingUIManager in the scene.")]
        [SerializeField] private CraftingUIManager craftingUIManager;

        [Header("Settings")]
        [Tooltip("Tag used to identify the player. Must match the player's tag.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("If true, the player must press an interact key (not implemented here). " +
                 "If false, entering the trigger range is enough.")]
        [SerializeField] private bool requireInteract = false;

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

            if (!requireInteract)
            {
                ActivateStation();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = false;
            DeactivateStation();
        }

        // =====================================================================
        // Activation
        // =====================================================================

        /// <summary>
        /// Call this from your interact system if requireInteract is true.
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

            // Only clear if WE are the current station
            if (craftingUIManager.ActiveStation == station)
            {
                craftingUIManager.ClearStation();
                Debug.Log($"[CraftingStation] Deactivated: {station.StationType}");
            }
        }
    }
}
