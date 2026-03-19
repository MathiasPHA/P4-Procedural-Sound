using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using InventorySystem.Data;
using InventorySystem.Crafting;

namespace InventorySystem.UI
{
    /// <summary>
    /// Displays the full detail view for the currently selected recipe.
    /// Shows the result item's icon, name, description, item stats,
    /// required ingredients with have/need counts, and a Craft button.
    ///
    /// Stats are pulled from ItemData.displayStats (designer-configured)
    /// plus auto-generated entries for durability when applicable.
    ///
    /// BUILD THE PANEL (top to bottom):
    ///   1. Result icon (Image, ~80x80)
    ///   2. Result name (TMP, bold)
    ///   3. Description (TMP, wrapping)
    ///   4. StatsContainer (empty GameObject with Vertical Layout Group)
    ///   5. IngredientContainer (empty GameObject with Vertical Layout Group)
    ///   6. Craft button
    /// </summary>
    public class RecipeDetailUI : MonoBehaviour
    {
        [Header("Result Display")]
        [SerializeField] private Image resultIcon;
        [SerializeField] private TMPro.TextMeshProUGUI resultName;
        [SerializeField] private TMPro.TextMeshProUGUI resultDescription;

        [Header("Stats")]
        [SerializeField] private Transform statsContainer;
        [SerializeField] private GameObject statRowPrefab;

        [Header("Ingredients")]
        [SerializeField] private Transform ingredientContainer;
        [SerializeField] private GameObject ingredientRowPrefab;

        [Header("Craft Button")]
        [SerializeField] private Button craftButton;
        [SerializeField] private TMPro.TextMeshProUGUI craftButtonText;

        [Header("Colours")]
        [SerializeField] private Color craftableButtonColour = new Color(0.35f, 0.55f, 0.35f);
        [SerializeField] private Color uncraftableButtonColour = new Color(0.3f, 0.3f, 0.3f);

        /// <summary>
        /// Fired when the player clicks the Craft button.
        /// CraftingUIManager handles the actual crafting logic.
        /// </summary>
        public event Action OnCraftClicked;

        private readonly List<IngredientRowUI> _ingredientRows = new();
        private readonly List<StatRowUI> _statRows = new();
        private bool _canCraft;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (craftButton != null)
                craftButton.onClick.AddListener(HandleCraftClick);

            Hide();
        }

        private void OnDestroy()
        {
            if (craftButton != null)
                craftButton.onClick.RemoveListener(HandleCraftClick);
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Populate the detail pane with a recipe's information.
        /// </summary>
        public void Show(Recipe recipe, Inventory inventory, ICraftingStation station)
        {
            if (recipe == null || recipe.result == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);

            // --- Result info ---
            if (resultIcon != null)
            {
                resultIcon.sprite = recipe.result.icon;
                resultIcon.enabled = recipe.result.icon != null;
            }

            if (resultName != null)
            {
                resultName.text = recipe.result.displayName;
                resultName.color = GetCategoryColour(recipe.result.category);
            }

            if (resultDescription != null)
            {
                resultDescription.text = recipe.result.description;
                resultDescription.gameObject.SetActive(
                    !string.IsNullOrEmpty(recipe.result.description));
            }

            // --- Stats ---
            RebuildStatRows(recipe.result);

            // --- Ingredients ---
            RebuildIngredientRows(recipe, inventory);

            // --- Craft button ---
            _canCraft = station != null && station.CanCraft(recipe, inventory);
            UpdateCraftButton();
        }

        /// <summary>
        /// Refresh ingredient counts and craft button state without
        /// rebuilding the entire panel. Called when inventory changes.
        /// </summary>
        public void Refresh(Recipe recipe, Inventory inventory, ICraftingStation station)
        {
            if (recipe == null || !gameObject.activeSelf) return;

            for (int i = 0; i < _ingredientRows.Count && i < recipe.ingredients.Length; i++)
            {
                var req = recipe.ingredients[i];
                int have = inventory.GetItemCount(req.item.id);
                _ingredientRows[i].UpdateCount(have, req.amount);
            }

            _canCraft = station != null && station.CanCraft(recipe, inventory);
            UpdateCraftButton();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // =====================================================================
        // Stats
        // =====================================================================

        private void RebuildStatRows(ItemData item)
        {
            foreach (var row in _statRows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }
            _statRows.Clear();

            if (statsContainer == null || statRowPrefab == null) return;

            // --- Auto-generated stats ---

            // Durability (only for items with instance state)
            if (item.hasInstanceState && item.maxDurability > 0)
            {
                SpawnStatRow("Durability", item.maxDurability.ToString());
            }

            // --- Designer-configured stats from ItemData.displayStats ---
            if (item.displayStats != null)
            {
                foreach (var stat in item.displayStats)
                {
                    if (string.IsNullOrEmpty(stat.label)) continue;

                    var go = Instantiate(statRowPrefab, statsContainer);
                    go.name = $"Stat_{stat.label}";

                    var row = go.GetComponent<StatRowUI>();
                    if (row != null)
                    {
                        row.Initialise(stat);
                        _statRows.Add(row);
                    }
                    else
                    {
                        Destroy(go);
                    }
                }
            }
        }

        private void SpawnStatRow(string label, string value,
            StatValueColour colour = StatValueColour.Neutral)
        {
            if (statsContainer == null || statRowPrefab == null) return;

            var go = Instantiate(statRowPrefab, statsContainer);
            go.name = $"Stat_{label}";

            var row = go.GetComponent<StatRowUI>();
            if (row != null)
            {
                row.Initialise(label, value, colour);
                _statRows.Add(row);
            }
            else
            {
                Destroy(go);
            }
        }

        // =====================================================================
        // Ingredient rows
        // =====================================================================

        private void RebuildIngredientRows(Recipe recipe, Inventory inventory)
        {
            foreach (var row in _ingredientRows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }
            _ingredientRows.Clear();

            if (ingredientContainer == null || ingredientRowPrefab == null) return;

            foreach (var req in recipe.ingredients)
            {
                if (req.item == null) continue;

                var go = Instantiate(ingredientRowPrefab, ingredientContainer);
                go.name = $"Ingredient_{req.item.id}";

                var row = go.GetComponent<IngredientRowUI>();
                if (row == null)
                {
                    Debug.LogWarning("[RecipeDetailUI] IngredientRowPrefab missing IngredientRowUI.");
                    Destroy(go);
                    continue;
                }

                int have = inventory.GetItemCount(req.item.id);
                row.Initialise(req.item, have, req.amount);
                _ingredientRows.Add(row);
            }
        }

        // =====================================================================
        // Craft button
        // =====================================================================

        private void UpdateCraftButton()
        {
            if (craftButton == null) return;

            craftButton.interactable = _canCraft;

            var buttonImage = craftButton.GetComponent<Image>();
            if (buttonImage != null)
                buttonImage.color = _canCraft ? craftableButtonColour : uncraftableButtonColour;

            if (craftButtonText != null)
                craftButtonText.text = "Craft";
        }

        private void HandleCraftClick()
        {
            if (!_canCraft) return;
            OnCraftClicked?.Invoke();
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private Color GetCategoryColour(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Tool       => new Color(0.6f, 0.85f, 1f),
                ItemCategory.Consumable => new Color(0.6f, 1f, 0.6f),
                ItemCategory.Material   => new Color(0.9f, 0.8f, 0.6f),
                ItemCategory.Buildable  => new Color(1f, 0.7f, 0.5f),
                _                       => Color.white
            };
        }
    }
}