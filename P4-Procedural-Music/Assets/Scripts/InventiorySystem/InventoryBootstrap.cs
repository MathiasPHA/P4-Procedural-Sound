using UnityEngine;
using InventorySystem.Data;
using InventorySystem.UI;

namespace InventorySystem
{
    [DefaultExecutionOrder(-50)]
    public class InventoryBootstrap : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private InventoryUIManager uiManager;

        [Header("Crafting")]
        [Tooltip("Assign the CraftingUIManager in the scene. Leave null to skip crafting init.")]
        [SerializeField] private CraftingUIManager craftingUIManager;

        [Header("UI Coordinator")]
        [Tooltip("Optional — leave unassigned, the singleton is found automatically at runtime.")]
        [SerializeField] private UICoordinator uiCoordinator;

        [Header("Test Items (remove for production)")]
        [SerializeField] private ItemData testWood;
        [SerializeField] private ItemData testStone;
        [SerializeField] private ItemData testAxe;
        [SerializeField] private ItemData testBerries;

        public static Inventory PlayerInventory { get; private set; }

        private void Awake()
        {
            if (itemDatabase != null)
                itemDatabase.Initialise();
            else
                Debug.LogError("[InventoryBootstrap] ItemDatabase is not assigned!");

            // Do NOT look up UICoordinator here — Awake order is non-deterministic
            // and UICoordinator.Instance may not be set yet even though it is
            // DontDestroyOnLoad. The lookup moves to Start() where all Awakes
            // are guaranteed to have finished.
        }

        private void Start()
        {
            // Resolve coordinator now — all Awake() calls are done, so
            // UICoordinator.Instance is guaranteed to be set if it exists.
            if (uiCoordinator == null)
                uiCoordinator = UICoordinator.Instance;

            if (uiCoordinator == null)
                uiCoordinator = FindFirstObjectByType<UICoordinator>(FindObjectsInactive.Include);

            if (uiCoordinator == null)
                Debug.LogError("[InventoryBootstrap] UICoordinator not found — Tab toggle will NOT work.");

            if (!ValidateReferences()) return;

            PlayerInventory = new Inventory();

            uiManager.Initialise(PlayerInventory);

            if (craftingUIManager != null)
            {
                craftingUIManager.Initialise(PlayerInventory);
                Debug.Log("[InventoryBootstrap] Crafting system initialized.");
            }

            if (uiCoordinator != null)
            {
                uiCoordinator.Initialise();
                Debug.Log("[InventoryBootstrap] UI Coordinator initialized.");
            }

            SpawnTestItems();

            Debug.Log("[InventoryBootstrap] Inventory system initialized.");
        }

        private bool ValidateReferences()
        {
            bool valid = true;
            if (itemDatabase == null) { Debug.LogError("[InventoryBootstrap] ItemDatabase is not assigned!"); valid = false; }
            if (uiManager == null) { Debug.LogError("[InventoryBootstrap] InventoryUIManager is not assigned!"); valid = false; }
            return valid;
        }

        private void SpawnTestItems()
        {
            if (testWood != null) PlayerInventory.AddItem(testWood, 32);
            if (testStone != null) PlayerInventory.AddItem(testStone, 15);
            if (testAxe != null) PlayerInventory.AddItem(testAxe, 1);
            if (testBerries != null) PlayerInventory.AddItem(testBerries, 8);
        }
    }
}