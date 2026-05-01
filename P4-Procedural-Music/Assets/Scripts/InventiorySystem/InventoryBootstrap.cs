using UnityEngine;
using InventorySystem.Data;
using InventorySystem.UI;

namespace InventorySystem
{
    /// <summary>
    /// Game bootstrap that creates and wires up the inventory and crafting systems.
    ///
    /// Initialization order:
    ///   -100  InventoryInputProvider.Start() — resolves actions from PlayerInput clone
    ///    -50  InventoryBootstrap.Start()     — creates Inventory, wires UI + crafting, spawns test items
    ///      0  Everything else
    ///
    /// By running in Start() with execution order -50, we guarantee that:
    /// 1. PlayerInput has created its clone (in its own Awake/OnEnable)
    /// 2. InventoryInputProvider has resolved all actions (at -100)
    /// 3. Only THEN do we create the inventory and subscribe to input events
    ///
    /// UICoordinator is initialised LAST so it can subscribe to OnToggleInventory
    /// after both managers have set themselves up. This is the single owner of
    /// inventory + crafting open/close — Tab no longer reaches the managers
    /// directly.
    /// </summary>
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
        [Tooltip("Single owner of inventory + crafting open/close. " +
                 "Required for Tab toggle to work.")]
        [SerializeField] private UICoordinator uiCoordinator;

        [Header("Test Items (remove for production)")]
        [SerializeField] private ItemData testWood;
        [SerializeField] private ItemData testStone;
        [SerializeField] private ItemData testAxe;
        [SerializeField] private ItemData testBerries;

        /// <summary>
        /// Public accessor so other systems (crafting, etc.) can reach the inventory.
        /// </summary>
        public static Inventory PlayerInventory { get; private set; }

        private void Awake()
        {
            // Only initialise the item database in Awake — it's a ScriptableObject
            // with no dependency on scene objects, so it's safe to do early.

            if (uiCoordinator == null)
            {
                uiCoordinator = FindFirstObjectByType<UICoordinator>(FindObjectsInactive.Include);
            }

            if (itemDatabase != null)
            {
                itemDatabase.Initialise();
            }
            else
            {
                Debug.LogError("[InventoryBootstrap] ItemDatabase is not assigned!");
            }
        }

        private void Start()
        {
            // Validate references before proceeding
            if (!ValidateReferences()) return;

            // Create the runtime inventory
            PlayerInventory = new Inventory();

            // 1. Inventory UI
            uiManager.Initialise(PlayerInventory);

            // 2. Crafting UI
            if (craftingUIManager != null)
            {
                craftingUIManager.Initialise(PlayerInventory);
                Debug.Log("[InventoryBootstrap] Crafting system initialized.");
            }

            // 3. UI Coordinator — must come AFTER both managers so it subscribes
            //    to OnToggleInventory once everything else is ready.
            if (uiCoordinator != null)
            {
                uiCoordinator.Initialise();
                Debug.Log("[InventoryBootstrap] UI Coordinator initialized.");
            }
            else
            {
                Debug.LogError("[InventoryBootstrap] UICoordinator is not assigned — " +
                               "Tab toggle will NOT work. Assign a UICoordinator in the Inspector.");
            }

            // Spawn test items
            SpawnTestItems();

            Debug.Log("[InventoryBootstrap] Inventory system initialized.");
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (itemDatabase == null)
            {
                Debug.LogError("[InventoryBootstrap] ItemDatabase is not assigned!");
                valid = false;
            }

            if (uiManager == null)
            {
                Debug.LogError("[InventoryBootstrap] InventoryUIManager is not assigned!");
                valid = false;
            }

            return valid;
        }

        private void SpawnTestItems()
        {
            if (testWood != null)    PlayerInventory.AddItem(testWood, 32);
            if (testStone != null)   PlayerInventory.AddItem(testStone, 15);
            if (testAxe != null)     PlayerInventory.AddItem(testAxe, 1);
            if (testBerries != null) PlayerInventory.AddItem(testBerries, 8);
        }
    }
}