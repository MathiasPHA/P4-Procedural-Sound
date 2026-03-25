using System;
using UnityEngine;
using UnityEngine.UI;
using InventorySystem.Crafting;

namespace InventorySystem.UI
{
    /// <summary>
    /// Visual representation of a single recipe in the crafting panel's
    /// recipe list. Shows the result item's icon and name.
    ///
    /// Craftable recipes are fully opaque; uncraftable ones are dimmed.
    /// The currently selected recipe gets a highlight border.
    ///
    /// BUILD THE PREFAB:
    ///   1. Create a horizontal layout GameObject (~200x40)
    ///   2. Add child: Image for icon (40x40, preserve aspect)
    ///   3. Add child: TMP text for name (flexible width)
    ///   4. Add a background Image on the root for selection highlight
    ///   5. Add a Button component on the root (Navigation: None)
    ///   6. Attach this script and wire references
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RecipeEntryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMPro.TextMeshProUGUI nameText;
        [SerializeField] private Image backgroundImage;

        [Header("Colours")]
        [SerializeField] private Color normalColour = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color selectedColour = new Color(0.35f, 0.3f, 0.2f, 0.9f);
        [SerializeField] private Color craftableTextColour = new Color(1f, 0.95f, 0.85f);
        [SerializeField] private Color uncraftableTextColour = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private float uncraftableIconAlpha = 0.4f;

        /// <summary>The recipe this entry represents.</summary>
        public Recipe Recipe { get; private set; }

        private Action<Recipe> _onClick;
        private bool _isCraftable;
        private bool _isSelected;
        private Button _button;

        // =====================================================================
        // Initialisation
        // =====================================================================

        /// <summary>
        /// Set up this entry with a recipe and a click callback.
        /// Called by CraftingUIManager when building the recipe list.
        /// </summary>
        public void Initialise(Recipe recipe, bool canCraft, Action<Recipe> onClick)
        {
            Recipe = recipe;
            _onClick = onClick;
            _isCraftable = canCraft;

            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClicked);

            // Populate visuals
            if (iconImage != null && recipe.result != null)
            {
                iconImage.sprite = recipe.result.icon;
                iconImage.enabled = recipe.result.icon != null;
            }

            if (nameText != null)
                nameText.text = recipe.result != null ? recipe.result.displayName : recipe.recipeId;

            UpdateVisuals();
        }

        // =====================================================================
        // Public state setters
        // =====================================================================

        /// <summary>
        /// Update whether the player currently has materials to craft this recipe.
        /// </summary>
        public void SetCraftable(bool canCraft)
        {
            if (_isCraftable == canCraft) return;
            _isCraftable = canCraft;
            UpdateVisuals();
        }

        /// <summary>
        /// Show or hide the selection highlight.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            UpdateVisuals();
        }

        // =====================================================================
        // Visuals
        // =====================================================================

        private void UpdateVisuals()
        {
            // Background
            if (backgroundImage != null)
                backgroundImage.color = _isSelected ? selectedColour : normalColour;

            // Text colour
            if (nameText != null)
                nameText.color = _isCraftable ? craftableTextColour : uncraftableTextColour;

            // Icon opacity
            if (iconImage != null)
            {
                var c = iconImage.color;
                c.a = _isCraftable ? 1f : uncraftableIconAlpha;
                iconImage.color = c;
            }
        }

        // =====================================================================
        // Click
        // =====================================================================

        private void OnClicked()
        {
            _onClick?.Invoke(Recipe);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }
    }
}
