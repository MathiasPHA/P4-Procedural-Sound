using UnityEngine;
using InventorySystem.Crafting;
using InventorySystem.Input;

namespace InventorySystem.UI
{
    /// <summary>
    /// Single source of truth for inventory + crafting panel visibility.
    ///
    /// Neither InventoryUIManager nor CraftingUIManager subscribe to
    /// OnToggleInventory themselves any more — this coordinator owns that
    /// subscription and drives both panels together so they are always in sync.
    ///
    /// All external callers (PauseManager, OpenCraftingUIAction, placement
    /// re-open) must go through this coordinator. Never call ForceOpen /
    /// ForceClose on the individual managers directly.
    ///
    /// SETUP:
    ///   1. Add this component to any persistent scene GameObject (e.g. UIRoot).
    ///   2. Assign InventoryUIManager, CraftingUIManager, and InventoryInputProvider
    ///      in the Inspector.
    ///   3. Call Initialise() from InventoryBootstrap AFTER both managers have
    ///      been Initialised (the coordinator must wire up last).
    ///   4. In PauseManager, swap the dual close calls for coordinator.ClosePanels().
    ///   5. In OpenCraftingUIAction, swap the dual open calls for
    ///      coordinator.OpenWithStation(station).
    /// </summary>
    public class UICoordinator : MonoBehaviour
    {
        [Header("Managed Panels")]
        [SerializeField] private InventoryUIManager inventoryUI;
        [SerializeField] private CraftingUIManager  craftingUI;

        [Header("Input")]
        [SerializeField] private InventoryInputProvider inputProvider;

        /// <summary>True when both panels are currently open.</summary>
        public bool ArePanelsOpen { get; private set; }

        private bool _initialized;

        // =====================================================================
        // Initialisation — called by InventoryBootstrap after both managers
        // =====================================================================

        public void Initialise()
        {
            if (_initialized)
            {
                Debug.LogWarning("[UICoordinator] Initialise() called more than once — ignoring.");
                return;
            }

            if (inventoryUI == null || craftingUI == null || inputProvider == null)
            {
                Debug.LogError("[UICoordinator] Missing references — coordinator disabled. " +
                               "Assign InventoryUIManager, CraftingUIManager, and InventoryInputProvider.");
                return;
            }

            inputProvider.OnToggleInventory += Toggle;

            ArePanelsOpen  = false;
            _initialized   = true;

            Debug.Log("[UICoordinator] Initialized successfully.");
        }

        private void OnDestroy()
        {
            if (inputProvider != null)
                inputProvider.OnToggleInventory -= Toggle;
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Toggle both panels together. Bound to the inventory toggle input action.
        /// </summary>
        public void Toggle()
        {
            if (ArePanelsOpen)
                ClosePanels();
            else
                OpenPanels();
        }

        /// <summary>
        /// Open both panels unconditionally.
        /// No-ops if already open.
        /// </summary>
        public void OpenPanels()
        {
            if (ArePanelsOpen) return;

            ArePanelsOpen = true;
            inventoryUI.ForceOpen();
            craftingUI.ForceOpen();
        }

        /// <summary>
        /// Close both panels unconditionally.
        /// No-ops if already closed.
        /// </summary>
        public void ClosePanels()
        {
            if (!ArePanelsOpen) return;

            ArePanelsOpen = false;
            inventoryUI.ForceClose();
            craftingUI.ForceClose();
        }

        /// <summary>
        /// Open both panels and focus the crafting panel on a specific station.
        /// Used by world-station interactions (cooking pot, workbench, etc.).
        /// If the panels are already open, only the station target changes.
        /// </summary>
        public void OpenWithStation(ICraftingStation station)
        {
            // Set the station first so OpenPanel() rebuilds with the right recipes.
            craftingUI.SetActiveStation(station);
            OpenPanels();
        }

        /// <summary>
        /// Re-open both panels after a buildable placement finishes.
        /// Called by CraftingUIManager when _closedForPlacement is set.
        /// </summary>
        public void ReopenAfterPlacement()
        {
            // Force-open even if ArePanelsOpen is false — placement closed them
            // without going through ClosePanels(), so the flag may be stale.
            ArePanelsOpen = false;
            OpenPanels();
        }
    }
}
