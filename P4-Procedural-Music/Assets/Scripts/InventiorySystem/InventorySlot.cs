using System;

namespace InventorySystem.Data
{
    /// <summary>
    /// Represents a single slot in the inventory.
    /// Holds an optional ItemInstance and a quantity for stackable items.
    /// 
    /// For non-stackable items (tools with durability), quantity is always 0 or 1
    /// and the ItemInstance carries the per-item state.
    /// For stackable items, the ItemInstance is shared/simple and quantity tracks the count.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public ItemInstance Instance { get; private set; }
        public int Quantity { get; private set; }

        public bool IsEmpty => Instance == null || Quantity <= 0;
        public ItemData ItemData => Instance?.Data;
        public bool IsFull => !IsEmpty && Quantity >= Instance.Data.maxStackSize;

        /// <summary>
        /// How many more of this item the slot can accept.
        /// </summary>
        public int RemainingCapacity =>
            IsEmpty ? 0 : Instance.Data.maxStackSize - Quantity;

        /// <summary>
        /// Place an item instance into an empty slot.
        /// </summary>
        public bool Set(ItemInstance instance, int quantity = 1)
        {
            if (instance == null || quantity <= 0) return false;
            if (!IsEmpty) return false; // slot must be empty — use AddToStack for merging

            Instance = instance;
            Quantity = Math.Min(quantity, instance.Data.maxStackSize);
            return true;
        }

        /// <summary>
        /// Adds quantity to an existing stack of the same item.
        /// Returns the number of items that could NOT fit (overflow).
        /// </summary>
        public int AddToStack(int amount)
        {
            if (IsEmpty || amount <= 0) return amount;

            int canFit = RemainingCapacity;
            int toAdd = Math.Min(amount, canFit);
            Quantity += toAdd;

            return amount - toAdd; // overflow
        }

        /// <summary>
        /// Removes quantity from the stack.
        /// If quantity reaches 0, the slot is cleared entirely.
        /// Returns the number actually removed.
        /// </summary>
        public int RemoveFromStack(int amount)
        {
            if (IsEmpty || amount <= 0) return 0;

            int toRemove = Math.Min(amount, Quantity);
            Quantity -= toRemove;

            if (Quantity <= 0) Clear();

            return toRemove;
        }

        /// <summary>
        /// Splits the stack, removing half (rounded down) and returning it
        /// as a new InventorySlot the caller can place elsewhere.
        /// Returns null if the slot has 1 or fewer items.
        /// </summary>
        public InventorySlot SplitStack()
        {
            if (IsEmpty || Quantity <= 1) return null;

            int halfAmount = Quantity / 2;
            Quantity -= halfAmount;

            var splitSlot = new InventorySlot();

            // For instanced items (tools), clone the instance.
            // For stackable materials, share the same instance reference — they're stateless.
            var splitInstance = Instance.Data.hasInstanceState
                ? new ItemInstance(Instance)
                : Instance;

            splitSlot.Set(splitInstance, halfAmount);
            return splitSlot;
        }

        /// <summary>
        /// Whether this slot can accept the given item (for merging stacks).
        /// True if the slot is empty OR holds the same stackable item with room.
        /// </summary>
        public bool CanAccept(ItemData data)
        {
            if (IsEmpty) return true;
            if (ItemData.id != data.id) return false;
            if (!data.IsStackable) return false;
            return !IsFull;
        }

        public void Clear()
        {
            Instance = null;
            Quantity = 0;
        }

        // --- Save / Load ---

        public SlotSaveData ToSaveData()
        {
            return new SlotSaveData
            {
                instanceData = IsEmpty
                    ? default
                    : Instance.ToSaveData(),
                quantity = Quantity,
                isEmpty = IsEmpty
            };
        }
    }

    [Serializable]
    public struct SlotSaveData
    {
        public ItemInstanceSaveData instanceData;
        public int quantity;
        public bool isEmpty;
    }
}
