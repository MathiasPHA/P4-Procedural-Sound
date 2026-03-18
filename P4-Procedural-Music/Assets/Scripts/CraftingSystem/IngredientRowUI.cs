using UnityEngine;
using UnityEngine.UI;
using InventorySystem.Data;

namespace InventorySystem.UI
{
    /// <summary>
    /// Displays a single ingredient requirement in the recipe detail pane.
    /// Shows the ingredient's icon, name, and a have/need count.
    /// The count text turns green when satisfied, red when short.
    ///
    /// BUILD THE PREFAB:
    ///   1. Create a horizontal layout GameObject (~180x28)
    ///   2. Add child: Image for icon (24x24, preserve aspect)
    ///   3. Add child: TMP text for name (flexible width, left-aligned)
    ///   4. Add child: TMP text for count (right-aligned, e.g. "5/10")
    ///   5. Attach this script and wire references
    /// </summary>
    public class IngredientRowUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMPro.TextMeshProUGUI nameText;
        [SerializeField] private TMPro.TextMeshProUGUI countText;

        [Header("Colours")]
        [SerializeField] private Color satisfiedColour = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color shortColour = new Color(1f, 0.4f, 0.4f);

        /// <summary>
        /// Set up this row with an ingredient's data and current counts.
        /// </summary>
        public void Initialise(ItemData item, int have, int need)
        {
            if (iconImage != null)
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = item.icon != null;
            }

            if (nameText != null)
                nameText.text = item.displayName;

            UpdateCount(have, need);
        }

        /// <summary>
        /// Update just the have/need count. Called when inventory changes
        /// without needing to rebuild the entire detail pane.
        /// </summary>
        public void UpdateCount(int have, int need)
        {
            if (countText == null) return;

            // Clamp display of 'have' to the 'need' amount so it reads "10/10" not "32/10"
            int displayHave = Mathf.Min(have, need);
            countText.text = $"{displayHave}/{need}";
            countText.color = have >= need ? satisfiedColour : shortColour;
        }
    }
}
