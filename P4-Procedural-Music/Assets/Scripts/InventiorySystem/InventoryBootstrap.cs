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

            // Wire up the inventory UI
            uiManager.Initialise(PlayerInventory);

            // Wire up the crafting UI
            if (craftingUIManager != null)
            {
                craftingUIManager.Initialise(PlayerInventory);
                Debug.Log("[InventoryBootstrap] Crafting system initialized.");
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
