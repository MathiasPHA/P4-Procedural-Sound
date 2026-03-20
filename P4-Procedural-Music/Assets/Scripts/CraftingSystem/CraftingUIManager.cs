using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;
using InventorySystem.Input;
using InventorySystem.Crafting;

namespace InventorySystem.UI
{
    /// <summary>
    /// Independent UI orchestrator for the crafting panel.
    /// Opens and closes in sync with the inventory (listens to the same
    /// toggle event) but manages its own panel hierarchy, recipe list,
    /// and detail pane.
    ///
    /// Communicates with the inventory purely through the Inventory API
    /// and the crafting station abstraction — no direct coupling to
    /// InventoryUIManager.
    ///
    /// Setup:
    ///   1. Create a CraftingPanel under your UI Canvas (sibling to InventoryPanel)
    ///   2. Add RecipeList (ScrollView) on the left, DetailPane on the right
    ///   3. Assign references in the inspector
    ///   4. Call Initialise() from InventoryBootstrap after inventory creation
    /// </summary>
    public class CraftingUIManager : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InventoryInputProvider inputProvider;

        [Header("Panel")]
        [Tooltip("Root GameObject of the entire crafting panel. Toggled on/off with inventory.")]
        [SerializeField] private GameObject craftingPanel;

        [Header("Station Header")]
        [SerializeField] private TMPro.TextMeshProUGUI stationNameText;

        [Header("Recipe List")]
        [SerializeField] private Transform recipeListContent;
        [SerializeField] private GameObject recipeEntryPrefab;

        [Header("Detail Pane")]
        [SerializeField] private RecipeDetailUI recipeDetail;

        [Header("Default Station")]
        [Tooltip("Hand-crafting station used when the player isn't at a crafting station. " +
                 "Attach a HandCraftingStation component to a persistent GameObject and assign it here.")]
        [SerializeField] private BaseCraftingStation handCraftingStation;

        // --- Runtime state ---
        private Inventory _inventory;
        private ICraftingStation _activeStation;
        private bool _isOpen;
        private bool _initialized;

        private readonly List<RecipeEntryUI> _recipeEntries = new();
        private Recipe _selectedRecipe;

        // =====================================================================
        // Initialisation (called by InventoryBootstrap.Start)
        // =====================================================================

        public void Initialise(Inventory inventory)
        {
            _inventory = inventory;

            if (!ValidateReferences()) return;

            // Default to hand-crafting
            _activeStation = handCraftingStation;

            // Wire events
            if (inputProvider != null)
                inputProvider.OnToggleInventory += OnToggleInventory;

            _inventory.OnInventoryChanged += OnInventoryChanged;

            // Detail pane setup
            if (recipeDetail != null)
                recipeDetail.OnCraftClicked += HandleCraft;

            craftingPanel.SetActive(false);
            _isOpen = false;
            _initialized = true;

            Debug.Log("[CraftingUIManager] Initialized successfully.");
        }

        private void OnDestroy()
        {
            if (inputProvider != null)
                inputProvider.OnToggleInventory -= OnToggleInventory;

            if (_inventory != null)
                _inventory.OnInventoryChanged -= OnInventoryChanged;

            if (recipeDetail != null)
                recipeDetail.OnCraftClicked -= HandleCraft;
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (inputProvider == null)
            {
                Debug.LogError("[CraftingUIManager] InputProvider is not assigned!");
                valid = false;
            }

            if (craftingPanel == null)
            {
                Debug.LogError("[CraftingUIManager] CraftingPanel is not assigned!");
                valid = false;
            }

            if (recipeListContent == null)
            {
                Debug.LogError("[CraftingUIManager] RecipeListContent is not assigned!");
                valid = false;
            }

            if (recipeEntryPrefab == null)
            {
                Debug.LogError("[CraftingUIManager] RecipeEntryPrefab is not assigned!");
                valid = false;
            }

            if (recipeDetail == null)
                Debug.LogWarning("[CraftingUIManager] RecipeDetail not assigned — detail pane disabled.");

            if (handCraftingStation == null)
                Debug.LogWarning("[CraftingUIManager] HandCraftingStation not assigned — " +
                                 "crafting will only work near stations.");

            return valid;
        }

        // =====================================================================
        // Public API — called by CraftingStationInteraction
        // =====================================================================

        /// <summary>
        /// Set the active crafting station. Called when the player interacts
        /// with a station in the world. Pass null to revert to hand-crafting.
        /// </summary>
        public void SetActiveStation(ICraftingStation station)
        {
            _activeStation = station ?? (ICraftingStation)handCraftingStation;

            if (_isOpen)
            {
                RebuildRecipeList();
                ClearSelection();
                UpdateStationHeader();
            }
        }

        /// <summary>
        /// Revert to the default hand-crafting station.
        /// Called when the player leaves a station's interaction range.
        /// </summary>
        public void ClearStation()
        {
            SetActiveStation(null);
        }

        /// <summary>
        /// The currently active crafting station (never null if handCraftingStation is assigned).
        /// </summary>
        public ICraftingStation ActiveStation => _activeStation;

        // =====================================================================
        // Toggle (synced with inventory)
        // =====================================================================

        private void OnToggleInventory()
        {
            _isOpen = !_isOpen;
            craftingPanel.SetActive(_isOpen);

            if (_isOpen)
            {
                // Refresh discovery before showing
                if (_activeStation is BaseCraftingStation baseStation)
                    baseStation.RefreshDiscovery(_inventory);

                RebuildRecipeList();
                UpdateStationHeader();

                // Auto-select first recipe if nothing is selected
                if (_selectedRecipe == null && _recipeEntries.Count > 0)
                    SelectRecipe(_recipeEntries[0].Recipe);
            }
            else
            {
                ClearSelection();
            }
        }

        // =====================================================================
        // Inventory change handler
        // =====================================================================

        private void OnInventoryChanged()
        {
            if (!_isOpen) return;

            // Refresh discovery — player may have picked up new materials
            if (_activeStation is BaseCraftingStation baseStation)
                baseStation.RefreshDiscovery(_inventory);

            // Refresh craftability indicators on all visible entries
            foreach (var entry in _recipeEntries)
            {
                bool canCraft = _activeStation.CanCraft(entry.Recipe, _inventory);
                entry.SetCraftable(canCraft);
            }

            // Refresh the detail pane ingredient counts
            if (_selectedRecipe != null && recipeDetail != null)
                recipeDetail.Refresh(_selectedRecipe, _inventory, _activeStation);
        }

        // =====================================================================
        // Recipe list management
        // =====================================================================

        private void RebuildRecipeList()
        {
            // Clear existing entries
            foreach (var entry in _recipeEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            _recipeEntries.Clear();

            if (_activeStation == null) return;

            // Spawn an entry for each discovered recipe
            var recipes = _activeStation.DiscoveredRecipes;

            for (int i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null) continue;

                var go = Instantiate(recipeEntryPrefab, recipeListContent);
                go.name = $"Recipe_{recipe.recipeId}";

                var entry = go.GetComponent<RecipeEntryUI>();
                if (entry == null)
                {
                    Debug.LogWarning($"[CraftingUIManager] RecipeEntryPrefab missing RecipeEntryUI component.");
                    Destroy(go);
                    continue;
                }

                bool canCraft = _activeStation.CanCraft(recipe, _inventory);
                entry.Initialise(recipe, canCraft, OnRecipeEntryClicked);
                _recipeEntries.Add(entry);
            }
        }

        private void UpdateStationHeader()
        {
            if (stationNameText == null) return;

            string name = _activeStation switch
            {
                BaseCraftingStation station => station.StationType switch
                {
                    CraftingStationType.HandCraft => "Crafting",
                    _ => station.StationType.ToString()
                },
                _ => "Crafting"
            };

            stationNameText.text = name.ToUpper();
        }

        // =====================================================================
        // Selection
        // =====================================================================

        private void OnRecipeEntryClicked(Recipe recipe)
        {
            SelectRecipe(recipe);
        }

        private void SelectRecipe(Recipe recipe)
        {
            _selectedRecipe = recipe;

            // Update visual selection on list entries
            foreach (var entry in _recipeEntries)
                entry.SetSelected(entry.Recipe == recipe);

            // Show detail
            if (recipeDetail != null && recipe != null)
                recipeDetail.Show(recipe, _inventory, _activeStation);
        }

        private void ClearSelection()
        {
            _selectedRecipe = null;

            foreach (var entry in _recipeEntries)
                entry.SetSelected(false);

            if (recipeDetail != null)
                recipeDetail.Hide();
        }

        // =====================================================================
        // Crafting
        // =====================================================================

        private void HandleCraft()
        {
            if (_selectedRecipe == null || _activeStation == null) return;

            bool success = _activeStation.Craft(_selectedRecipe, _inventory);

            if (success)
            {
                Debug.Log($"[Crafting] Crafted {_selectedRecipe.resultAmount}x " +
                          $"{_selectedRecipe.result.displayName}");

                // Refresh everything — inventory events will also fire,
                // but we refresh immediately for snappy feedback
                OnInventoryChanged();
            }
        }
    }
}
