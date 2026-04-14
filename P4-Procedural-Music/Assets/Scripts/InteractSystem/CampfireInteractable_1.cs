using UnityEngine;
using InteractionSystem;
using InventorySystem;
using InventorySystem.Data;

namespace InteractionSystem
{
    public class CampfireInteractable : Interactable
    {
        private CampfireController _campfire;

        private void Awake()
        {
            _campfire = GetComponent<CampfireController>();

            if (_campfire == null)
                Debug.LogError("[CampfireInteractable] No CampfireController found on this GameObject.", this);
        }

        public override bool CanInteract() => _campfire != null;

        public override void Interact(PlayerStateManager player)
        {
            if (_campfire == null) return;

            Inventory inventory = InventoryBootstrap.PlayerInventory;
            if (inventory == null) return;

            InventorySlot activeSlot = inventory.Slots[inventory.ActiveHotbarIndex];
            if (activeSlot.IsEmpty) return;

            ItemData heldItem = activeSlot.ItemData;
            if (!heldItem.isFuel) return;

            _campfire.AddFuel(heldItem, 1);
            activeSlot.RemoveFromStack(1);
            inventory.NotifySlotChanged(inventory.ActiveHotbarIndex);
            inventory.NotifyChanged();
        }
    }
}