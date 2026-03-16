using UnityEngine;
using InventorySystem.Data;
using InventorySystem.UI;

namespace InventorySystem
{
    /// <summary>
    /// Game bootstrap that creates and wires up the inventory system.
    /// Attach to a GameObject in your scene (e.g. "GameManager").
    ///
    /// For the prototype demo, this also spawns some test items so you can
    /// immediately see drag/drop and hotbar working.
    /// </summary>
    public class InventoryBootstrap : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private InventoryUIManager uiManager;

        [Header("Test Items (remove for production)")]
        [SerializeField] private ItemData testWood;
        [SerializeField] private ItemData testStone;
        [SerializeField] private ItemData testAxe;
        [SerializeField] private ItemData testBerries;

        /// <summary>
        /// Public accessor so other systems (crafting, etc.) can reach the inventory.
        /// In a larger project, use a service locator or DI instead.
        /// </summary>
        public static Inventory PlayerInventory { get; private set; }

        private void Awake()
        {
            // 1. Initialise the item database (builds the ID lookup dictionary)
            itemDatabase.Initialise();

            // 2. Create the runtime inventory
            PlayerInventory = new Inventory();

            // 3. Wire up the UI
            uiManager.Initialise(PlayerInventory);
        }

        private void Start()
        {
            // 4. Spawn test items so the prototype has something to interact with
            SpawnTestItems();
        }

        private void SpawnTestItems()
        {
            if (testWood != null)
                PlayerInventory.AddItem(testWood, 32);

            if (testStone != null)
                PlayerInventory.AddItem(testStone, 15);

            if (testAxe != null)
                PlayerInventory.AddItem(testAxe, 1);

            if (testBerries != null)
                PlayerInventory.AddItem(testBerries, 8);

            Debug.Log("[Bootstrap] Test items added to inventory.");
        }
    }
}
