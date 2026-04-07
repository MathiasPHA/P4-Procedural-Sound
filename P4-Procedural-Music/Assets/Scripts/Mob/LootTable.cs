using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;

namespace MobSystem.Data
{
    /// <summary>
    /// A reusable loot table for mob drops. Each entry has a drop chance
    /// and quantity range. Evaluated per-entry (not mutually exclusive),
    /// so a mob can drop multiple different items from the same table.
    ///
    /// CREATE: Right-click → Create → Mobs → Loot Table
    ///
    /// USAGE:
    ///   Assign to MobData.lootTable. On death, MobController calls
    ///   LootTable.Roll() and spawns the results as WorldItems.
    /// </summary>
    [CreateAssetMenu(fileName = "New Loot Table", menuName = "Mobs/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Tooltip("Each entry is evaluated independently — multiple items can drop from one kill.")]
        public List<LootEntry> entries = new List<LootEntry>();

        /// <summary>
        /// Roll all entries and return a list of (item, quantity) results.
        /// Each entry is an independent chance — not weighted against the others.
        /// </summary>
        public List<LootResult> Roll()
        {
            var results = new List<LootResult>();

            foreach (var entry in entries)
            {
                if (entry.item == null) continue;
                if (Random.value > entry.dropChance) continue;

                int qty = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                if (qty <= 0) continue;

                results.Add(new LootResult { item = entry.item, quantity = qty });
            }

            return results;
        }

        [System.Serializable]
        public class LootEntry
        {
            [Tooltip("The item to drop.")]
            public ItemData item;

            [Tooltip("Probability this item drops (0–1). Each entry rolls independently.")]
            [Range(0f, 1f)]
            public float dropChance = 1f;

            [Tooltip("Minimum quantity when this entry hits.")]
            [Min(1)]
            public int minQuantity = 1;

            [Tooltip("Maximum quantity when this entry hits (inclusive).")]
            [Min(1)]
            public int maxQuantity = 1;
        }

        public struct LootResult
        {
            public ItemData item;
            public int quantity;
        }
    }
}
