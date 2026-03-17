using System;
using System.Collections.Generic;

namespace InventorySystem.Data
{
    /// <summary>
    /// The player's inventory. Pure C# — no MonoBehaviour dependency.
    /// Manages a fixed array of slots and a hotbar subset.
    /// Fires events when slots change so UI and crafting can react.
    /// 
    /// This is the single source of truth for what the player is carrying.
    /// Both the InventoryUI and any CraftingStation read from and write to this.
    /// </summary>
    public class Inventory
    {
        public const int DefaultSlotCount = 32;
        public const int HotbarSize = 8;

        public InventorySlot[] Slots { get; }
        public int SlotCount => Slots.Length;

        /// <summary>
        /// The hotbar is simply the first HotbarSize slots.
        /// This avoids syncing two separate arrays.
        /// </summary>
        public ReadOnlySpan<InventorySlot> Hotbar => new ReadOnlySpan<InventorySlot>(Slots, 0, HotbarSize);

        /// <summary>
        /// Currently selected hotbar index (0-based).
        /// </summary>
        public int ActiveHotbarIndex { get; private set; }

        // --- Events ---

        /// <summary>Fired whenever a specific slot's contents change.</summary>
        public event Action<int> OnSlotChanged;

        /// <summary>Fired whenever any change occurs (useful for crafting recipe refresh).</summary>
        public event Action OnInventoryChanged;

        /// <summary>Fired when the active hotbar selection changes.</summary>
        public event Action<int> OnHotbarSelectionChanged;

        public Inventory(int slotCount = DefaultSlotCount)
        {
            Slots = new InventorySlot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                Slots[i] = new InventorySlot();
            }
        }

        // =====================================================================
        // Public API — used by crafting, pickups, UI, and game logic
        // =====================================================================

        /// <summary>
        /// Attempts to add an item to the inventory.
        /// First tries to merge into existing stacks, then fills empty slots.
        /// Returns the number of items that could NOT fit (overflow).
        /// </summary>
        public int AddItem(ItemData data, int amount = 1)
        {
            if (data == null || amount <= 0) return amount;

            int remaining = amount;

            // If stackable, try to top up existing stacks first
            if (data.IsStackable)
            {
                for (int i = 0; i < Slots.Length && remaining > 0; i++)
                {
                    if (!Slots[i].IsEmpty && Slots[i].ItemData.id == data.id && !Slots[i].IsFull)
                    {
                        remaining = Slots[i].AddToStack(remaining);
                        NotifySlotChanged(i);
                    }
                }
            }

            // Place remainder in empty slots
            while (remaining > 0)
            {
                int emptyIndex = FindFirstEmptySlot();
                if (emptyIndex < 0) break; // inventory full

                var instance = new ItemInstance(data);
                int toPlace = Math.Min(remaining, data.maxStackSize);
                Slots[emptyIndex].Set(instance, toPlace);
                remaining -= toPlace;
                NotifySlotChanged(emptyIndex);
            }

            if (remaining < amount)
            {
                OnInventoryChanged?.Invoke();
            }

            return remaining;
        }

        /// <summary>
        /// Adds a specific ItemInstance (with its existing state) to the inventory.
        /// Used when picking up world items that have durability, etc.
        /// </summary>
        public bool AddInstance(ItemInstance instance, int quantity = 1)
        {
            if (instance == null) return false;

            // If the item has per-instance state, it can't merge into existing stacks
            if (instance.Data.hasInstanceState)
            {
                int emptyIndex = FindFirstEmptySlot();
                if (emptyIndex < 0) return false;

                Slots[emptyIndex].Set(instance, 1);
                NotifySlotChanged(emptyIndex);
                OnInventoryChanged?.Invoke();
                return true;
            }

            // Stackable — delegate to AddItem
            int overflow = AddItem(instance.Data, quantity);
            return overflow == 0;
        }

        /// <summary>
        /// Removes a quantity of the given item id from the inventory.
        /// Pulls from the last matching slots first (preserves hotbar order).
        /// Returns true if the full amount was removed.
        /// </summary>
        public bool RemoveItem(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
            if (GetItemCount(itemId) < amount) return false;

            int remaining = amount;

            // Remove from end first to preserve hotbar items
            for (int i = Slots.Length - 1; i >= 0 && remaining > 0; i--)
            {
                if (Slots[i].IsEmpty || Slots[i].ItemData.id != itemId) continue;

                int removed = Slots[i].RemoveFromStack(remaining);
                remaining -= removed;
                NotifySlotChanged(i);
            }

            OnInventoryChanged?.Invoke();
            return remaining <= 0;
        }

        /// <summary>
        /// Checks whether the inventory contains at least `amount` of the given item.
        /// </summary>
        public bool HasItem(string itemId, int amount = 1)
        {
            return GetItemCount(itemId) >= amount;
        }

        /// <summary>
        /// Total count of a specific item across all slots.
        /// </summary>
        public int GetItemCount(string itemId)
        {
            int count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].ItemData.id == itemId)
                {
                    count += Slots[i].Quantity;
                }
            }
            return count;
        }

        /// <summary>
        /// Returns all item IDs currently in the inventory (deduplicated).
        /// Useful for recipe discovery checks.
        /// </summary>
        public HashSet<string> GetAllItemIds()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty)
                {
                    ids.Add(Slots[i].ItemData.id);
                }
            }
            return ids;
        }

        /// <summary>
        /// Returns all items of a given category. Useful for filtered displays.
        /// </summary>
        public List<InventorySlot> GetSlotsOfCategory(ItemCategory category)
        {
            var result = new List<InventorySlot>();
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].ItemData.category == category)
                {
                    result.Add(Slots[i]);
                }
            }
            return result;
        }

        // =====================================================================
        // Slot manipulation — used by UI drag/drop
        // =====================================================================

        /// <summary>
        /// Swaps the contents of two slots. Used by drag-and-drop reordering.
        /// </summary>
        public void SwapSlots(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= Slots.Length || indexB < 0 || indexB >= Slots.Length) return;
            if (indexA == indexB) return;

            (Slots[indexA], Slots[indexB]) = (Slots[indexB], Slots[indexA]);

            NotifySlotChanged(indexA);
            NotifySlotChanged(indexB);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Attempts to merge the source slot's stack into the target slot.
        /// If they're different items, swaps instead.
        /// Returns true if any change occurred.
        /// </summary>
        public bool MergeOrSwap(int sourceIndex, int targetIndex)
        {
            if (sourceIndex == targetIndex) return false;

            var source = Slots[sourceIndex];
            var target = Slots[targetIndex];

            // Target empty — just move
            if (target.IsEmpty)
            {
                SwapSlots(sourceIndex, targetIndex);
                return true;
            }

            // Same stackable item — merge
            if (!source.IsEmpty && !target.IsEmpty
                && source.ItemData.id == target.ItemData.id
                && source.ItemData.IsStackable)
            {
                int overflow = target.AddToStack(source.Quantity);
                if (overflow <= 0)
                {
                    source.Clear();
                }
                else
                {
                    source.RemoveFromStack(source.Quantity - overflow);
                }

                NotifySlotChanged(sourceIndex);
                NotifySlotChanged(targetIndex);
                OnInventoryChanged?.Invoke();
                return true;
            }

            // Different items or non-stackable — swap
            SwapSlots(sourceIndex, targetIndex);
            return true;
        }

        /// <summary>
        /// Splits the stack in the given slot and places the split half
        /// into the first available empty slot. Returns the index of the
        /// new slot, or -1 if no space / nothing to split.
        /// </summary>
        public int SplitStack(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Slots.Length) return -1;

            var splitResult = Slots[slotIndex].SplitStack();
            if (splitResult == null) return -1;

            int emptyIndex = FindFirstEmptySlot();
            if (emptyIndex < 0)
            {
                // No space — merge back
                Slots[slotIndex].AddToStack(splitResult.Quantity);
                return -1;
            }

            // Move the split data into the empty slot
            Slots[emptyIndex].Set(splitResult.Instance, splitResult.Quantity);

            NotifySlotChanged(slotIndex);
            NotifySlotChanged(emptyIndex);
            OnInventoryChanged?.Invoke();
            return emptyIndex;
        }

        // =====================================================================
        // Hotbar
        // =====================================================================

        /// <summary>
        /// The inventory slot index that is currently equipped, or -1 if nothing is.
        /// This can be a hotbar slot OR any inventory slot (right-click equip).
        /// </summary>
        public int EquippedSlotIndex { get; private set; } = -1;

        /// <summary>Fired when the equipped item changes. Parameter is the new slot index (-1 = unequipped).</summary>
        public event Action<int> OnEquippedChanged;

        /// <summary>Fired when a consumable is used. Parameter is the ItemInstance that was consumed.</summary>
        public event Action<ItemInstance> OnItemConsumed;

        public void SetActiveHotbar(int index)
        {
            if (index < 0 || index >= HotbarSize) return;
            if (index == ActiveHotbarIndex) return;

            ActiveHotbarIndex = index;
            OnHotbarSelectionChanged?.Invoke(index);
        }

        /// <summary>
        /// The ItemInstance currently selected on the hotbar. May be null.
        /// </summary>
        public ItemInstance ActiveHotbarItem =>
            Slots[ActiveHotbarIndex].IsEmpty ? null : Slots[ActiveHotbarIndex].Instance;

        /// <summary>
        /// The currently equipped ItemInstance. May be null.
        /// </summary>
        public ItemInstance EquippedItem =>
            EquippedSlotIndex >= 0 && !Slots[EquippedSlotIndex].IsEmpty
                ? Slots[EquippedSlotIndex].Instance
                : null;

        /// <summary>
        /// Valheim-style hotbar press: selects the hotbar slot and toggles equip.
        /// If the slot holds a consumable, it's used immediately instead of equipping.
        /// </summary>
        public void HotbarUse(int hotbarIndex)
        {
            if (hotbarIndex < 0 || hotbarIndex >= HotbarSize) return;

            // Always update the visual selection
            ActiveHotbarIndex = hotbarIndex;
            OnHotbarSelectionChanged?.Invoke(hotbarIndex);

            var slot = Slots[hotbarIndex];
            if (slot.IsEmpty) return;

            UseSlot(hotbarIndex);
        }

        /// <summary>
        /// Use an item in any slot (hotbar or inventory).
        /// Tools/Buildables: toggle equip on/off.
        /// Consumables: consume one and remove from stack.
        /// Materials: no action.
        /// </summary>
        public void UseSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Slots.Length) return;

            var slot = Slots[slotIndex];
            if (slot.IsEmpty) return;

            switch (slot.ItemData.category)
            {
                case ItemCategory.Tool:
                case ItemCategory.Buildable:
                    ToggleEquip(slotIndex);
                    break;

                case ItemCategory.Consumable:
                    ConsumeFromSlot(slotIndex);
                    break;

                case ItemCategory.Material:
                    // Materials can't be used directly
                    break;
            }
        }

        /// <summary>
        /// Equip the item in the given slot. If already equipped, unequip.
        /// If a different item was equipped, swap to the new one.
        /// </summary>
        private void ToggleEquip(int slotIndex)
        {
            int previousEquipped = EquippedSlotIndex;

            if (EquippedSlotIndex == slotIndex)
            {
                // Already equipped — unequip
                EquippedSlotIndex = -1;
            }
            else
            {
                // Equip the new slot
                EquippedSlotIndex = slotIndex;
            }

            // Notify UI to update both the old and new slot visuals
            if (previousEquipped >= 0)
                NotifySlotChanged(previousEquipped);
            if (EquippedSlotIndex >= 0)
                NotifySlotChanged(EquippedSlotIndex);

            OnEquippedChanged?.Invoke(EquippedSlotIndex);
        }

        /// <summary>
        /// Consume one item from the slot and fire the consumed event.
        /// </summary>
        private void ConsumeFromSlot(int slotIndex)
        {
            var slot = Slots[slotIndex];
            if (slot.IsEmpty) return;

            var consumed = slot.Instance;

            slot.RemoveFromStack(1);
            NotifySlotChanged(slotIndex);
            OnInventoryChanged?.Invoke();

            // If the consumed item was equipped and the slot is now empty, unequip
            if (EquippedSlotIndex == slotIndex && slot.IsEmpty)
            {
                EquippedSlotIndex = -1;
                OnEquippedChanged?.Invoke(-1);
            }

            OnItemConsumed?.Invoke(consumed);
        }

        // =====================================================================
        // Save / Load
        // =====================================================================

        public InventorySaveData ToSaveData()
        {
            var data = new InventorySaveData
            {
                slots = new SlotSaveData[Slots.Length],
                activeHotbarIndex = ActiveHotbarIndex
            };

            for (int i = 0; i < Slots.Length; i++)
            {
                data.slots[i] = Slots[i].ToSaveData();
            }

            return data;
        }

        public void LoadFromSaveData(InventorySaveData data, ItemDatabase db)
        {
            for (int i = 0; i < Slots.Length && i < data.slots.Length; i++)
            {
                Slots[i].Clear();

                if (data.slots[i].isEmpty) continue;

                var instance = db.ResolveInstance(data.slots[i].instanceData);
                if (instance != null)
                {
                    Slots[i].Set(instance, data.slots[i].quantity);
                }

                NotifySlotChanged(i);
            }

            ActiveHotbarIndex = data.activeHotbarIndex;
            OnInventoryChanged?.Invoke();
        }

        // =====================================================================
        // Internals
        // =====================================================================

        private int FindFirstEmptySlot()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsEmpty) return i;
            }
            return -1;
        }

        /// <summary>
        /// Clears a specific slot and fires change events.
        /// Used by UI when dropping items to the world.
        /// </summary>
        public void ClearSlot(int index)
        {
            if (index < 0 || index >= Slots.Length) return;
            Slots[index].Clear();
            NotifySlotChanged(index);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Manually fires the inventory changed event.
        /// Use sparingly — prefer methods that fire it automatically.
        /// </summary>
        public void NotifyChanged()
        {
            OnInventoryChanged?.Invoke();
        }

        private void NotifySlotChanged(int index)
        {
            OnSlotChanged?.Invoke(index);
        }
    }

    [Serializable]
    public struct InventorySaveData
    {
        public SlotSaveData[] slots;
        public int activeHotbarIndex;
    }
}