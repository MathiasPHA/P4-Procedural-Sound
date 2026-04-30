using System;
using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;
using InventorySystem.Input;
using InventorySystem.Crafting;
using InventorySystem.Building;

namespace InventorySystem.UI
{
    /// <summary>
    /// Defines a tab in the crafting panel. Each tab maps to a crafting station.
    /// </summary>
    [Serializable]
    public struct CraftingTab
    {
        [Tooltip("Text shown on the tab button.")]
        public string label;

        [Tooltip("The crafting station this tab activates.")]
        public BaseCraftingStation station;
    }

    /// <summary>
    /// Independent UI orchestrator for the crafting panel.
    /// Opens and closes in sync with the inventory (listens to the same
    /// toggle event) but manages its own panel hierarchy, recipe list,
    /// detail pane, and tab bar.
    ///
    /// The tab bar lets the player switch between stations (e.g. Crafting,
    /// Building) without needing a specific tool equipped. Each tab maps
    /// to a BaseCraftingStation that filters recipes by station type.
    ///
    /// Tab visibility:
    ///   When the active station is one of the tabs, the tab bar is shown
    ///   and the player can switch freely. When a world-only station is
    ///   active (cooking station, workbench, etc. — anything not in the
    ///   tabs list), the tab bar is hidden so the player can't accidentally
    ///   switch off the world station's recipe set.
    ///
    /// Communicates with the inventory purely through the Inventory API
    /// and the crafting station abstraction — no direct coupling to
    /// InventoryUIManager.
    ///
    /// Setup:
    ///   1. Create a CraftingPanel under your UI Canvas (sibling to InventoryPanel)
    ///   2. Add a TabBar (HorizontalLayoutGroup) above the recipe list
    ///   3. Add RecipeList (ScrollView) on the left, DetailPane on the right
    ///   4. Assign references in the inspector
    ///   5. Populate the Tabs list with label + station pairs
    ///   6. Call Initialise() from InventoryBootstrap after inventory creation
    /// </summary>
    public class CraftingUIManager : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InventoryInputProvider inputProvider;

        [Header("Panel")]
        [Tooltip("Root GameObject of the entire crafting panel. Toggled on/off with inventory.")]
        [SerializeField] private GameObject craftingPanel;

        [Header("Tab Bar")]
        [Tooltip("Parent transform for spawned tab buttons. Should have a HorizontalLayoutGroup.")]
        [SerializeField] private Transform tabBarContainer;

        [Tooltip("Prefab for a single tab button. Must have a CraftingTabButton component.")]
        [SerializeField] private GameObject tabButtonPrefab;

        [Tooltip("Tabs available in the crafting panel. First tab is the default.\n" +
                 "Each entry maps a label to a BaseCraftingStation.")]
        [SerializeField] private List<CraftingTab> tabs = new();

        [Header("Station Header")]
        [SerializeField] private TMPro.TextMeshProUGUI stationNameText;

        [Header("Recipe List")]
        [SerializeField] private Transform recipeListContent;
        [SerializeField] private GameObject recipeEntryPrefab;

        [Header("Detail Pane")]
        [SerializeField] private RecipeDetailUI recipeDetail;

        [Header("Default Station (Fallback)")]
        [Tooltip("Used when the tabs list is empty. Otherwise the first tab's station is the default.")]
        [SerializeField] private BaseCraftingStation handCraftingStation;

        [Header("Audio")]
        [SerializeField] private AudioClip craftSound;
        [Range(0f, 1f)]
        [SerializeField] private float craftVolume = 0.5f;
        [Range(0f, 0.5f)]
        [SerializeField] private float craftPitchVariation = 0.1f;
        [Tooltip("Minimum time (in seconds) between playing the craft sound")]
        [SerializeField] private float craftSoundCooldown = 0.15f;
        private float lastCraftSoundTime;

        private AudioSource _audioSource;

        // --- Runtime state ---
        private Inventory _inventory;
        private ICraftingStation _activeStation;
        private bool _isOpen;
        private bool _initialized;

        // Tab state. _activeTabIndex == -1 means the active station isn't
        // represented in the tabs list (e.g. a world-only station like the
        // cooking pot). In that case the tab bar is hidden.
        private int _activeTabIndex;
        private readonly List<CraftingTabButton> _tabButtons = new();

        // Placement re-open: when the player clicks Build on a Buildable recipe,
        // the panel closes for placement. When placement ends we re-open on the
        // same tab so the player can continue building.
        private bool _closedForPlacement;

        private readonly List<RecipeEntryUI> _recipeEntries = new();
        private Recipe _selectedRecipe;

        /// <summary>Whether the crafting panel is currently visible.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>The currently active crafting station.</summary>
        public ICraftingStation ActiveStation => _activeStation;

        // =====================================================================
        // Initialisation (called by InventoryBootstrap.Start)
        // =====================================================================

        public void Initialise(Inventory inventory)
        {
            _inventory = inventory;

            if (!ValidateReferences()) return;

            // Default to first tab's station, or handCraftingStation as fallback
            _activeStation = tabs.Count > 0 && tabs[0].station != null
                ? tabs[0].station
                : (ICraftingStation)handCraftingStation;

            _activeTabIndex = 0;

            // Wire events
            if (inputProvider != null)
                inputProvider.OnToggleInventory += OnToggleInventory;

            _inventory.OnInventoryChanged += OnInventoryChanged;

            // Detail pane setup
            if (recipeDetail != null)
                recipeDetail.OnCraftClicked += HandleCraft;

            // Spawn tab buttons
            SpawnTabButtons();

            craftingPanel.SetActive(false);
            _isOpen = false;
            _initialized = true;

            // Subscribe to PlacementSystem events for re-open after placement
            if (PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.OnPlacementCancelled += OnPlacementEnded;
                PlacementSystem.Instance.OnPlacementFinished += OnPlacementEnded;
            }

            // Audio setup
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

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

            if (PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.OnPlacementCancelled -= OnPlacementEnded;
                PlacementSystem.Instance.OnPlacementFinished -= OnPlacementEnded;
            }
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

            if (tabs.Count == 0)
                Debug.LogWarning("[CraftingUIManager] No tabs defined — using handCraftingStation as fallback.");

            if (tabs.Count > 0 && (tabBarContainer == null || tabButtonPrefab == null))
                Debug.LogWarning("[CraftingUIManager] Tabs defined but TabBarContainer or TabButtonPrefab " +
                                 "not assigned — tab bar won't render.");

            return valid;
        }

        // =====================================================================
        // Tab Bar
        // =====================================================================

        private void SpawnTabButtons()
        {
            // Block opening the inventory while fishing — the player is locked
            // into the minigame and shouldn't be able to swap gear / craft mid-cast.
            if (FishingSystem.FishingManager.Instance != null &&
                FishingSystem.FishingManager.Instance.IsFishing)
                return;

            if (tabBarContainer == null || tabButtonPrefab == null || tabs.Count == 0)
                return;

            for (int i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                if (tab.station == null) continue;

                var go = Instantiate(tabButtonPrefab, tabBarContainer);
                go.name = $"Tab_{tab.label}";

                var tabButton = go.GetComponent<CraftingTabButton>();
                if (tabButton == null)
                {
                    Debug.LogWarning($"[CraftingUIManager] TabButtonPrefab missing CraftingTabButton component.");
                    Destroy(go);
                    continue;
                }

                tabButton.Initialise(i, tab.label, OnTabClicked);
                _tabButtons.Add(tabButton);
            }

            // Highlight the default tab
            UpdateTabHighlights();
        }

        private void OnTabClicked(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= tabs.Count) return;
            if (tabIndex == _activeTabIndex) return;

            SwitchToTab(tabIndex);
        }

        private void SwitchToTab(int tabIndex)
        {
            _activeTabIndex = tabIndex;
            var tab = tabs[tabIndex];

            _activeStation = tab.station;

            // Refresh discovery for the new station
            if (_activeStation is BaseCraftingStation baseStation)
                baseStation.RefreshDiscovery(_inventory);

            RebuildRecipeList();
            ClearSelection();
            UpdateTabHighlights();
            UpdateTabBarVisibility();
            UpdateStationHeader();

            // Auto-select first recipe
            if (_recipeEntries.Count > 0)
                SelectRecipe(_recipeEntries[0].Recipe);
        }

        private void UpdateTabHighlights()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                _tabButtons[i].SetActive(i == _activeTabIndex);
            }
        }

        /// <summary>
        /// Show the tab bar only when the active station is one of the tabs.
        /// World-only stations (cooking, workbench, etc.) hide the bar so
        /// the player can't switch away from the station's recipe set.
        /// </summary>
        private void UpdateTabBarVisibility()
        {
            if (tabBarContainer == null) return;

            bool show = _activeTabIndex >= 0 && _activeTabIndex < tabs.Count;
            tabBarContainer.gameObject.SetActive(show);
        }

        /// <summary>
        /// Returns the tab index whose station matches the given station,
        /// or -1 if no tab matches (i.e. the station is a world-only station).
        /// </summary>
        private int FindTabIndexForStation(ICraftingStation station)
        {
            if (station == null) return -1;
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].station == (BaseCraftingStation)station)
                    return i;
            }
            return -1;
        }

        // =====================================================================
        // Public API — called by CraftingStationInteraction
        // =====================================================================

        /// <summary>
        /// Set the active crafting station. Called when the player interacts
        /// with a world station (workbench, cooking pot, etc.).
        /// Pass null to revert to the default tab's station.
        /// </summary>
        public void SetActiveStation(ICraftingStation station)
        {
            if (station == null)
            {
                // Revert to default tab
                _activeStation = tabs.Count > 0 && tabs[0].station != null
                    ? tabs[0].station
                    : (ICraftingStation)handCraftingStation;
                _activeTabIndex = 0;
            }
            else
            {
                _activeStation = station;

                // -1 means the active station isn't a tab — world-only.
                _activeTabIndex = FindTabIndexForStation(station);
            }

            if (_isOpen)
            {
                RebuildRecipeList();
                ClearSelection();
                UpdateTabHighlights();
                UpdateTabBarVisibility();
                UpdateStationHeader();
            }
        }

        /// <summary>
        /// Revert to the default tab's station.
        /// Called when the player leaves a world station's interaction range.
        /// </summary>
        public void ClearStation()
        {
            SetActiveStation(null);
        }

        /// <summary>
        /// Set the active station AND open the crafting panel.
        /// Used by click-to-interact handlers (e.g. CookingStationInteractable)
        /// that need to pop the panel open in response to a world click, rather
        /// than waiting for the inventory toggle key.
        /// If the panel is already open, this just re-targets to the new station.
        /// </summary>
        public void OpenWithStation(ICraftingStation station)
        {
            SetActiveStation(station);

            if (!_isOpen)
                OpenPanel();
        }

        // =====================================================================
        // PlacementSystem callbacks — re-open after placement
        // =====================================================================

        private void OnPlacementEnded()
        {
            // Re-open the crafting panel on the same tab the player was on
            // when they clicked Build. This lets them continue placing structures.
            if (_closedForPlacement)
            {
                _closedForPlacement = false;
                OpenPanel();
            }
        }

        // =====================================================================
        // Toggle (synced with inventory)
        // =====================================================================

        private void OnToggleInventory()
        {
            if (_isOpen)
                ClosePanel();
            else
                OpenPanel();
        }

        private void OpenPanel()
        {
            _isOpen = true;
            craftingPanel.SetActive(true);

            // Refresh discovery before showing
            if (_activeStation is BaseCraftingStation baseStation)
                baseStation.RefreshDiscovery(_inventory);

            RebuildRecipeList();
            UpdateTabHighlights();
            UpdateTabBarVisibility();
            UpdateStationHeader();

            // Auto-select first recipe if nothing is selected
            if (_selectedRecipe == null && _recipeEntries.Count > 0)
                SelectRecipe(_recipeEntries[0].Recipe);
        }

        /// <summary>
        /// Close the crafting panel. Public so external systems (pause menu,
        /// scene transitions, etc.) can dismiss the panel cleanly.
        /// </summary>
        public void ClosePanel()
        {
            _isOpen = false;
            craftingPanel.SetActive(false);
            ClearSelection();
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

            // Tab-backed station: use the tab label
            if (_activeTabIndex >= 0 && _activeTabIndex < tabs.Count)
            {
                stationNameText.text = tabs[_activeTabIndex].label.ToUpper();
                return;
            }

            // World-only station (cooking, workbench, ...): use station type
            if (_activeStation != null)
            {
                stationNameText.text = GetStationDisplayName(_activeStation.StationType);
                return;
            }

            stationNameText.text = "CRAFTING";
        }

        /// <summary>
        /// Pretty-prints a CraftingStationType for the panel header when no tab
        /// label is available. Adjust here if you rename a station type.
        /// </summary>
        private static string GetStationDisplayName(CraftingStationType type)
        {
            return type switch
            {
                CraftingStationType.HandCraft       => "CRAFTING",
                CraftingStationType.Workbench       => "WORKBENCH",
                CraftingStationType.CookingStation  => "COOKING",
                CraftingStationType.BuildHammer     => "BUILDING",
                _                                   => type.ToString().ToUpper()
            };
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

                // Play craft sound
                if (craftSound != null && _audioSource != null && Time.time >= lastCraftSoundTime + craftSoundCooldown)
                {
                    lastCraftSoundTime = Time.time;
                   _audioSource.pitch = 1f + UnityEngine.Random.Range(-craftPitchVariation, craftPitchVariation);
                    _audioSource.PlayOneShot(craftSound, craftVolume);
                }

                // If the result was a Buildable, close the panel for placement.
                // PlacementSystem is now in ghost mode. Mark _closedForPlacement
                // so we re-open on the same tab when placement ends.
                if (_selectedRecipe.result.category == ItemCategory.Buildable)
                {
                    _closedForPlacement = true;
                    ClosePanel();
                    return;
                }

                // Normal crafting — refresh everything
                OnInventoryChanged();
            }
        }
    }
}