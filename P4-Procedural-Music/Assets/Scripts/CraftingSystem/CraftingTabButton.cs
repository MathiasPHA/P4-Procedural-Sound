using System;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.UI
{
    /// <summary>
    /// UI component for a single tab button in the crafting panel's tab bar.
    /// Displays a label and underline highlight; fires a callback on click.
    ///
    /// BUILD THE PREFAB:
    ///   1. Create a GameObject (~100x36) with a Button component
    ///   2. Add a child TMP text for the label
    ///   3. Add a child Image at the bottom for the underline highlight
    ///      (anchor bottom-stretch, height ~3px)
    ///   4. Attach this script and wire references
    ///   5. Set Button navigation to None
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CraftingTabButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMPro.TextMeshProUGUI labelText;
        [SerializeField] private Image underlineImage;

        [Header("Colours")]
        [SerializeField] private Color activeTextColour = new Color(1f, 0.95f, 0.85f);
        [SerializeField] private Color inactiveTextColour = new Color(0.6f, 0.55f, 0.5f);
        [SerializeField] private Color underlineColour = new Color(0.85f, 0.65f, 0.3f);

        private Button _button;
        private int _tabIndex;
        private Action<int> _onClick;

        /// <summary>
        /// Set up this tab button. Called by CraftingUIManager during init.
        /// </summary>
        public void Initialise(int index, string label, Action<int> onClick)
        {
            _tabIndex = index;
            _onClick = onClick;

            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClicked);

            if (labelText != null)
                labelText.text = label;

            SetActive(false);
        }

        /// <summary>
        /// Show or hide the active tab highlight.
        /// </summary>
        public void SetActive(bool active)
        {
            if (labelText != null)
                labelText.color = active ? activeTextColour : inactiveTextColour;

            if (underlineImage != null)
                underlineImage.color = active
                    ? underlineColour
                    : new Color(underlineColour.r, underlineColour.g, underlineColour.b, 0f);
        }

        private void OnClicked()
        {
            _onClick?.Invoke(_tabIndex);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }
    }
}
