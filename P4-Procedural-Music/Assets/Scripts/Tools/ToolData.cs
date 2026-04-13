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
        [Tooltip("Damage dealt per swing to a harvestable resource.")]
        [Min(1)] public int damage = 1;

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
    }
}