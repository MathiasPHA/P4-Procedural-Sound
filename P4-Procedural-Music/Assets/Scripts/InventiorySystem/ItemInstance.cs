using System;

namespace InventorySystem.Data
{
    /// <summary>
    /// Runtime wrapper around an ItemData definition.
    /// Stackable materials share a single instance per slot (state is just quantity).
    /// Tools/weapons each get their own instance to track durability, etc.
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        public readonly ItemData Data;

        // --- Per-instance mutable state ---
        public int CurrentDurability { get; private set; }

        public ItemInstance(ItemData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));

            if (data.hasInstanceState)
            {
                CurrentDurability = data.maxDurability;
            }
        }

        /// <summary>
        /// Copy constructor for cloning an instance (e.g. when splitting stacks
        /// of instanced items, if that ever applies).
        /// </summary>
        public ItemInstance(ItemInstance other)
        {
            Data = other.Data;
            CurrentDurability = other.CurrentDurability;
        }

        /// <summary>
        /// Reduces durability by the given amount. Returns true if the item broke (reached 0).
        /// No-ops on items without instance state.
        /// </summary>
        public bool ReduceDurability(int amount)
        {
            if (!Data.hasInstanceState) return false;

            CurrentDurability = Math.Max(0, CurrentDurability - amount);
            return CurrentDurability <= 0;
        }

        /// <summary>
        /// Restores durability (repair). Clamped to maxDurability.
        /// </summary>
        public void Repair(int amount)
        {
            if (!Data.hasInstanceState) return;
            CurrentDurability = Math.Min(Data.maxDurability, CurrentDurability + amount);
        }

        /// <summary>
        /// Durability as a 0-1 fraction for UI display (health bars, etc.).
        /// Returns 1 for items without instance state.
        /// </summary>
        public float DurabilityNormalized =>
            Data.hasInstanceState && Data.maxDurability > 0
                ? (float)CurrentDurability / Data.maxDurability
                : 1f;

        // --- Serialization helpers for save/load ---

        public ItemInstanceSaveData ToSaveData()
        {
            return new ItemInstanceSaveData
            {
                itemId = Data.id,
                currentDurability = CurrentDurability
            };
        }
    }

    /// <summary>
    /// Flat serializable struct for saving an ItemInstance to disk.
    /// Resolved back to a full ItemInstance through the ItemDatabase.
    /// </summary>
    [Serializable]
    public struct ItemInstanceSaveData
    {
        public string itemId;
        public int currentDurability;
    }
}
