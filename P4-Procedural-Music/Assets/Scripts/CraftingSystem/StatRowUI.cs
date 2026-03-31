using UnityEngine;
using UnityEngine.UI;
using InventorySystem.Data;

namespace InventorySystem.UI
{
    /// <summary>
    /// Displays a single stat/property line in the crafting detail pane.
    /// Label on the left, value on the right.
    ///
    /// For tag-style entries (empty value), only the label is shown
    /// centred across the full width.
    ///
    /// BUILD THE PREFAB:
    ///   1. Create a horizontal layout GameObject (~220x22)
    ///   2. Add child: TMP text for label (flexible width, left-aligned, 13px)
    ///   3. Add child: TMP text for value (preferred width 70, right-aligned, 13px)
    ///   4. Attach this script and wire references
    /// </summary>
    public class StatRowUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMPro.TextMeshProUGUI labelText;
        [SerializeField] private TMPro.TextMeshProUGUI valueText;

        [Header("Colours")]
        [SerializeField] private Color labelColour = new Color(0.75f, 0.72f, 0.65f);
        [SerializeField] private Color neutralColour = new Color(0.9f, 0.88f, 0.82f);
        [SerializeField] private Color positiveColour = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color negativeColour = new Color(1f, 0.4f, 0.4f);

        /// <summary>
        /// Populate this row from an ItemStatEntry.
        /// </summary>
        public void Initialise(ItemStatEntry stat)
        {
            bool hasValue = !string.IsNullOrEmpty(stat.value);

            if (labelText != null)
            {
                labelText.text = stat.label;
                labelText.color = labelColour;
            }

            if (valueText != null)
            {
                if (hasValue)
                {
                    valueText.text = stat.value;
                    valueText.color = GetValueColour(stat.colour);
                    valueText.gameObject.SetActive(true);
                }
                else
                {
                    // Tag-style entry (e.g. "One-handed") — hide value text
                    valueText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Initialise from raw strings (used when auto-generating stats
        /// from runtime data like durability).
        /// </summary>
        public void Initialise(string label, string value, StatValueColour colour = StatValueColour.Neutral)
        {
            Initialise(new ItemStatEntry
            {
                label = label,
                value = value,
                colour = colour
            });
        }

        private Color GetValueColour(StatValueColour colour)
        {
            return colour switch
            {
                StatValueColour.Positive => positiveColour,
                StatValueColour.Negative => negativeColour,
                _ => neutralColour
            };
        }
    }
}
