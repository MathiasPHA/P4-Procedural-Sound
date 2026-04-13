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
            Debug.Log("[Campfire] Interact() called.");

            if (_campfire == null)
            {
                Debug.LogError("[Campfire] _campfire is null.");
                return;
            }

            Inventory inventory = InventoryBootstrap.PlayerInventory;

            if (inventory == null)
            {
                Debug.LogError("[Campfire] PlayerInventory is null — InventoryBootstrap may not have run yet.");
                return;
            }

            InventorySlot activeSlot = inventory.Slots[inventory.ActiveHotbarIndex];
            Debug.Log($"[Campfire] Active hotbar index: {inventory.ActiveHotbarIndex}, slot empty: {activeSlot.IsEmpty}");

            if (activeSlot.IsEmpty)
            {
                Debug.Log("[Campfire] No item in active hotbar slot.");
                return;
            }

            ItemData heldItem = activeSlot.ItemData;
            Debug.Log($"[Campfire] Held item: {heldItem.displayName}, isFuel: {heldItem.isFuel}, burnFuelValue: {heldItem.burnFuelValue}");

            if (!heldItem.isFuel)
            {
                Debug.Log($"[Campfire] '{heldItem.displayName}' is not marked as fuel.");
                return;
            }

            _campfire.AddFuel(heldItem, 1);
            activeSlot.RemoveFromStack(1);
            inventory.NotifySlotChanged(inventory.ActiveHotbarIndex);
            inventory.NotifyChanged();
        }
    }
}