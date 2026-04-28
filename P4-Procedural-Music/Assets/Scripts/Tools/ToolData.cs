using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Data
{
    /// <summary>
    /// Links an inventory item (category: Tool) to its harvesting properties.
    ///
    /// CREATE ONE PER TOOL:
    ///   Right-click → Create → Inventory → Tool Data
    ///   Assign the matching ItemData, set the tool type and stats.
    /// </summary>
    [CreateAssetMenu(fileName = "New Tool", menuName = "Inventory/Tool Data")]
    public class ToolData : ScriptableObject
    {
        [Tooltip("The inventory item this tool corresponds to.")]
        public ItemData item;

        [Tooltip("What type of tool this is — determines which resources it can harvest.")]
        public ToolType toolType;

        [Header("Stats")]
        [Tooltip("Damage dealt per swing to a harvestable resource (and to mobs).")]
        [Min(0)] public int damage = 1;

        [Tooltip("Damage dealt per swing to PlacedStructures. " +
                 "0 = this tool cannot damage structures at all (default for axes, pickaxes, hand). " +
                 "Set this to 5 on the Hammer so it can demolish placed buildings.")]
        [Min(0)] public int structureDamage = 0;

        [Tooltip("Seconds between swings.")]
        [Min(0.1f)] public float cooldown = 0.5f;

        [Tooltip("How far in front of the player the tool can reach (in units).")]
        [Min(0.1f)] public float range = 1.5f;

        [Tooltip("Durability cost per swing. Only applies to items with hasInstanceState.")]
        [Min(0)] public int durabilityCost = 1;

        [Header("Passive Durability Drain")]
        [Tooltip("Should this item lose durability over time while equipped?")]
        public bool drainsOverTime = false;

        [Tooltip("Seconds between each passive durability tick.")]
        [Min(0.1f)] public float drainInterval = 5f;

        [Tooltip("How much durability is lost per tick.")]
        [Min(1)] public int drainAmount = 1;

        [Header("isInstrument")]
        [Tooltip("If true, this tool behaves like an instrument and plays sounds when used.")]
        public bool isInstrument = false;

        [Tooltip("Audio clips to play when this tool is used as an instrument.")]
        public List<AudioClip> instrumentSounds = new();

        [Tooltip("Volume used when playing instrument sounds.")]
        [Range(0f, 1f)] public float instrumentVolume = 1f;

        [Tooltip("Random pitch variation applied to instrument sounds.")]
        [Range(0f, 0.5f)] public float instrumentPitchVariation = 0.05f;
    }
}