using System.Collections.Generic;
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

        [Header("Consumable Effects")]
        [Tooltip("How much happiness this item gives when consumed (0–1 range). " +
                 "Only applies to Consumable category items.")]
        [Range(0f, 1f)]
        public float happinessBoost = 0f;
        [Tooltip("The sound to play when this item is consumed.")]
        public AudioClip consumeSound;
        [Tooltip("Volume multiplier for the consume sound.")]
        [Range(0f, 1f)]
        public float consumeSoundVolume = 1f;

        [Header("Fuel")]
        [Tooltip("Can this item be used as campfire fuel?")]
        public bool isFuel = false;

        [Tooltip("How much fuel this item adds to the campfire per unit. " +
                 "Only used when isFuel is true.")]
        [Min(0f)]
        public float burnFuelValue = 0f;

        [Header("Display Stats")]
        [Tooltip("Stats/properties shown in the crafting detail pane and tooltip. " +
                 "Each entry is a label + value pair. Order here = order in UI.")]
        public List<ItemStatEntry> displayStats = new();

        /// <summary>
        /// Whether this item type can be stacked (maxStackSize > 1 and no per-instance state).
        /// Items with instance state (durability etc.) are never stackable even if maxStackSize > 1.
        /// </summary>
        public bool IsStackable => maxStackSize > 1 && !hasInstanceState;
    }
}
