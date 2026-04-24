using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace InventorySystem.Data
{
    // Effect types that can be applied to the player
    [System.Serializable]
    public class ConsumableEffect
    {
        public EffectType effectType;

        [Header("Effect Parameters")]
        [Tooltip("Duration in seconds (0 = instant effect)")]
        public float duration = 0f;

        [Tooltip("Effect strength/magnitude")]
        public float magnitude = 1f;

        [Header("Visual Feedback")]
        [Tooltip("Tint color applied to player sprite during effect")]
        public Color tintColor = Color.white;

        [Tooltip("Message shown to player when effect is applied")]
        public string effectMessage = "";

        [Header("Audio")]
        [Tooltip("Sound played when effect starts")]
        public AudioClip effectSound;

        [Range(0f, 1f)]
        public float effectSoundVolume = 1f;
    }

    public enum EffectType
    {
        // Positive Effects
        RestoreHunger,      // Restore hunger (use magnitude)
        RestoreHappiness,   // Restore happiness (use magnitude)
        RestoreHealth,      // Restore health (use magnitude)
        SpeedBoost,         // Increase movement speed (magnitude = multiplier, e.g. 1.5 = 150% speed)
        Invincibility,      // Make player invincible for duration

        // Negative Effects
        DamageHealth,       // Deal damage to player (magnitude = damage amount)
        Poison,             // Damage over time (magnitude = damage per second)
        Slow,               // Reduce movement speed (magnitude = multiplier, e.g. 0.5 = 50% speed)
        Blindness,          // Reduce vision/darken screen
        Confusion,          // Reverse controls or add random movement

        // Utility Effects
        Teleport,           // Teleport to random location (magnitude = radius)
        Glow,               // Player emits light (magnitude = light radius)
        Invisibility,       // Make player invisible/transparent

        // Visual Only
        ColorTint,          // Just apply color tint (use tintColor)
        Shrink,             // Scale player down (magnitude = scale multiplier)
        Grow               // Scale player up (magnitude = scale multiplier)
    }

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

        [Header("Basic Consumable Effects")]
        [Tooltip("How much hunger this food item restores when consumed (0–1 range). " +
                 "Only applies to Consumable category items.")]
        [FormerlySerializedAs("happinessBoost")]
        [Range(0f, 1f)]
        public float hungerRestore = 0f;

        [Tooltip("How much happiness this item restores directly when consumed (0–1 range). " +
                 "Use for potions and special items — food should only restore hunger.")]
        [Range(0f, 1f)]
        public float happinessRestore = 0f;

        [Tooltip("The sound to play when this item is consumed.")]
        public AudioClip consumeSound;
        [Tooltip("Volume multiplier for the consume sound.")]
        [Range(0f, 1f)]
        public float consumeSoundVolume = 1f;

        [Header("Custom Consumable Effects")]
        [Tooltip("Additional effects applied when consuming this item. Can stack multiple effects!")]
        public List<ConsumableEffect> customEffects = new();

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

        /// <summary>
        /// Check if this item has any custom effects
        /// </summary>
        public bool HasCustomEffects => customEffects != null && customEffects.Count > 0;
    }
}