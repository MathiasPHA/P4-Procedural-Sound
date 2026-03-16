using UnityEngine;

namespace InventorySystem.Data
{
    [CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier used for saving/loading and recipe lookups")]
        public string id;
        public string displayName;

        [TextArea(2, 4)]
        public string description;

        [TextArea(1, 3)]
        [Tooltip("Short flavour text or lore note shown in tooltip")]
        public string loreNote;

        [Header("Visuals")]
        public Sprite icon;

        [Header("Classification")]
        public ItemCategory category;

        [Tooltip("Max stack size. 1 = non-stackable (tools, weapons)")]
        [Min(1)]
        public int maxStackSize = 1;

        public bool isDroppable = true;

        [Header("Instance Properties")]
        [Tooltip("Does this item track per-instance state like durability?")]
        public bool hasInstanceState;

        [Tooltip("Max durability for tools/weapons. Ignored if hasInstanceState is false")]
        [Min(0)]
        public int maxDurability = 100;

        /// <summary>
        /// Whether this item type can be stacked (maxStackSize > 1 and no per-instance state).
        /// Items with instance state (durability etc.) are never stackable even if maxStackSize > 1.
        /// </summary>
        public bool IsStackable => maxStackSize > 1 && !hasInstanceState;
    }
}
