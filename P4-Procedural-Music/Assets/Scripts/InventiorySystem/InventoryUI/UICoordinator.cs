using UnityEngine;
using UnityEngine.SceneManagement;
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

        // =====================================================================
        // Lifetime
        // =====================================================================

        private void Awake()
        {
            // Singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (inputProvider != null)
                inputProvider.OnToggleInventory -= Toggle;
        }

        // =====================================================================
        // Scene handling
        // =====================================================================

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AutoAssignIfNeeded();

            if (!_initialized)
                Initialise();
        }

        // =====================================================================
        // Auto wiring
        // =====================================================================

        private void AutoAssignIfNeeded()
        {
            if (inventoryUI == null)
                inventoryUI = FindSingle<InventoryUIManager>();

            if (craftingUI == null)
                craftingUI = FindSingle<CraftingUIManager>();

            if (inputProvider == null)
                inputProvider = FindSingle<InventoryInputProvider>();
        }

        private T FindSingle<T>() where T : Object
        {
            var all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (all.Length > 1)
                Debug.LogWarning($"[UICoordinator] Multiple {typeof(T).Name} found — using first.");

            return all.Length > 0 ? all[0] : null;
        }

        // =====================================================================
        // Initialisation
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
                Debug.LogError("[UICoordinator] Missing references EVEN AFTER auto-assign.");
                return;
            }

            inputProvider.OnToggleInventory += Toggle;

            ArePanelsOpen = false;
            _initialized = true;

            Debug.Log("[UICoordinator] Initialized successfully.");
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

            if (ArePanelsOpen)
                ClosePanels();
            else
                OpenPanels();
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

        public void ReopenAfterPlacement()
        {
            ArePanelsOpen = false;
            OpenPanels();
        }
    }
}