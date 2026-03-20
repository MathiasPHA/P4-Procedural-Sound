using System;
using UnityEngine;

namespace InventorySystem.Data
{
    /// <summary>
    /// A single stat/property line displayed on the crafting detail pane
    /// and item tooltip. Purely data — no logic.
    ///
    /// Examples:
    ///   label = "One-handed",   value = ""          (tag-style, no number)
    ///   label = "Slash",        value = "15 (5-40)"
    ///   label = "Block force",  value = "20"
    ///   label = "Movement speed", value = "-5%"
    ///   label = "Restores",     value = "25 HP"
    ///
    /// The value field is a string so it can hold anything:
    /// ranges, percentages, plain numbers, or nothing at all.
    /// </summary>
    [Serializable]
    public struct ItemStatEntry
    {
        [Tooltip("Stat name shown on the left (e.g. 'Slash', 'Block force', 'Restores')")]
        public string label;

        [Tooltip("Stat value shown on the right (e.g. '15', '-5%', '25 HP'). " +
                 "Leave empty for tag-style entries like 'One-handed'.")]
        public string value;

        [Tooltip("Optional colour tint for the value text. " +
                 "Use white for neutral, green for positive, red for negative.")]
        public StatValueColour colour;
    }

    /// <summary>
    /// Simple colour hint for stat values. The UI maps these to actual colours.
    /// </summary>
    public enum StatValueColour
    {
        Neutral,
        Positive,
        Negative
    }
}
