using System;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.UI
{
    /// <summary>
    /// UI component for a single tab button in the crafting panel's tab bar.
    /// Displays a label and an underline / background highlight; fires a
    /// callback on click.
    ///
    /// The "underlineImage" slot is generic — it can be a thin underline strip
    /// or a full button-background sprite. Its alpha is driven by
    /// activeAlpha / inactiveAlpha so inactive tabs can either disappear
    /// entirely (inactiveAlpha = 0) or remain dimly visible
    /// (e.g. inactiveAlpha = 0.4) for better discoverability.
    ///
    /// BUILD THE PREFAB:
    ///   1. Create a GameObject (~100x36) with a Button component
    ///   2. Add a child TMP text for the label
    ///   3. Add a child Image for the underline OR background highlight
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
        [SerializeField] private Color inactiveTextColour = new Color(0.85f, 0.78f, 0.65f);
        [SerializeField] private Color underlineColour = new Color(0.85f, 0.65f, 0.3f);

        [Header("Underline / Background Alpha")]
        [Tooltip("Alpha applied to the underline/background image when this tab IS selected.")]
        [Range(0f, 1f)]
        [SerializeField] private float activeAlpha = 1f;

        [Tooltip("Alpha applied to the underline/background image when this tab is NOT selected.\n" +
                 "Set to 0 for a classic underline that only shows on the selected tab.\n" +
                 "Set to ~0.3–0.5 to keep the button shape dimly visible on inactive tabs " +
                 "(better for discoverability when the image is a full button background).")]
        [Range(0f, 1f)]
        [SerializeField] private float inactiveAlpha = 1f;

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
            {
                float a = active ? activeAlpha : inactiveAlpha;
                underlineImage.color = new Color(
                    underlineColour.r,
                    underlineColour.g,
                    underlineColour.b,
                    a);
            }
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