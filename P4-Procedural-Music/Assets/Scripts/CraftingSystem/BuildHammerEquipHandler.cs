using UnityEngine;
using InventorySystem.Data;
using InventorySystem.Crafting;
using InventorySystem.UI;

namespace InventorySystem.Building
{
    /// <summary>
    /// Bridges the Build Hammer tool with the crafting UI.
    /// When the player equips the hammer, this swaps the active crafting
    /// station to the BuildHammerStation and auto-opens the build menu.
    /// When unequipped, it reverts to hand-crafting and closes the panel.
    ///
    /// SETUP:
    ///   1. Attach to the Player GameObject
    ///   2. Assign the BuildHammerStation and CraftingUIManager references
    ///   3. Set hammerItemId to match the hammer's ItemData.id (e.g. "Hammer")
    /// </summary>
    public class BuildHammerEquipHandler : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The BuildHammerStation used for structure recipes.")]
        [SerializeField] private BuildHammerStation buildHammerStation;

        [Tooltip("The crafting UI manager that displays the recipe panel.")]
        [SerializeField] private CraftingUIManager craftingUI;

        [Header("Configuration")]
        [Tooltip("The ItemData.id of the hammer tool. Must match exactly.")]
        [SerializeField] private string hammerItemId = "Hammer";

        private Inventory _inventory;
        private bool _hammerEquipped;

        /// <summary>True while the hammer is the equipped item.</summary>
        public bool IsHammerEquipped => _hammerEquipped;

        private void Start()
        {
            _inventory = InventoryBootstrap.PlayerInventory;

            if (_inventory != null)
                _inventory.OnEquippedChanged += OnEquippedChanged;
            else
                Debug.LogWarning("[BuildHammerEquipHandler] PlayerInventory not available.");
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnEquippedChanged -= OnEquippedChanged;
        }

        private void OnEquippedChanged(int slotIndex)
        {
            bool wasEquipped = _hammerEquipped;

            // Check if the newly equipped item is the hammer
            if (slotIndex >= 0)
            {
                var item = _inventory.EquippedItem;
                _hammerEquipped = item != null
                                  && item.Data.category == ItemCategory.Tool
                                  && item.Data.id == hammerItemId;
            }
            else
            {
                _hammerEquipped = false;
            }

            // --- Transition: equipped hammer ---
            if (_hammerEquipped && !wasEquipped)
            {
                if (craftingUI != null && buildHammerStation != null)
                {
                    craftingUI.SetActiveStation(buildHammerStation);
                    craftingUI.OpenBuildMenu();
                }
            }

            // --- Transition: unequipped hammer ---
            if (!_hammerEquipped && wasEquipped)
            {
                if (craftingUI != null)
                {
                    craftingUI.CloseBuildMenu();
                    craftingUI.ClearStation();
                }
            }
        }
    }
}
