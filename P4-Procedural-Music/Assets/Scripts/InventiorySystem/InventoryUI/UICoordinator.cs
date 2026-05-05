using UnityEngine;
using InventorySystem.Crafting;
using InventorySystem.Input;

namespace InventorySystem.UI
{
    public class UICoordinator : MonoBehaviour
    {
        public static UICoordinator Instance { get; private set; }

        [Header("Managed Panels")]
        [SerializeField] private InventoryUIManager inventoryUI;
        [SerializeField] private CraftingUIManager craftingUI;

        [Header("Input")]
        [SerializeField] private InventoryInputProvider inputProvider;

        public bool ArePanelsOpen { get; private set; }

        private bool _initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (inputProvider != null)
                inputProvider.OnToggleInventory -= Toggle;
        }

        // =====================================================================
        // Initialisation — called by InventoryBootstrap.Start()
        // =====================================================================

        public void Initialise()
        {
            if (_initialized)
            {
                Debug.LogWarning("[UICoordinator] Initialise() called more than once — ignoring.");
                return;
            }

            AutoAssignIfNeeded();

            if (inventoryUI == null || craftingUI == null || inputProvider == null)
            {
                Debug.LogError("[UICoordinator] Missing references after auto-assign. " +
                               "Assign InventoryUIManager, CraftingUIManager, and " +
                               "InventoryInputProvider in the Inspector.");
                return;
            }

            inputProvider.OnToggleInventory += Toggle;
            ArePanelsOpen = false;
            _initialized = true;

            Debug.Log("[UICoordinator] Initialized successfully.");
        }

        private void AutoAssignIfNeeded()
        {
            if (inventoryUI == null)
                inventoryUI = FindFirstObjectByType<InventoryUIManager>(FindObjectsInactive.Include);

            if (craftingUI == null)
                craftingUI = FindFirstObjectByType<CraftingUIManager>(FindObjectsInactive.Include);

            if (inputProvider == null)
                inputProvider = FindFirstObjectByType<InventoryInputProvider>(FindObjectsInactive.Include);
        }

        // =====================================================================
        // Public API
        // =====================================================================

        public void Toggle()
        {
            var fishing = FishingSystem.FishingManager.Instance;
            if (fishing != null && fishing.IsFishing)
            {
                fishing.CancelFishing();
                return;
            }

            // Flute ring open — close it instead of opening inventory.
            var flute = FindFirstObjectByType<InventorySystem.Tools.FluteTool>();
            if (flute != null && flute.IsOpen)
            {
                flute.ForceClose();
                return;
            }

            if (ArePanelsOpen) ClosePanels();
            else OpenPanels();
        }

        public void OpenPanels()
        {
            if (ArePanelsOpen) return;
            ArePanelsOpen = true;
            inventoryUI.ForceOpen();
            craftingUI.ForceOpen();
        }

        public void ClosePanels()
        {
            if (!ArePanelsOpen) return;
            ArePanelsOpen = false;
            inventoryUI.ForceClose();
            craftingUI.ForceClose();
        }

        public void OpenWithStation(ICraftingStation station)
        {
            craftingUI.SetActiveStation(station);
            OpenPanels();
        }

        public void SetScrollBlocked(bool blocked) => inventoryUI?.SetScrollBlocked(blocked);

        public void ReopenAfterPlacement()
        {
            ArePanelsOpen = false;
            OpenPanels();
        }
    }
}