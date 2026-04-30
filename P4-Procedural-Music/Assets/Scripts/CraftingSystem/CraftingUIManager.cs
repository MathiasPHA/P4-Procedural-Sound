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
    /// Manages its own panel hierarchy, recipe list, detail pane, and tab bar.
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
    /// IMPORTANT: This manager no longer subscribes to OnToggleInventory itself.
    /// Open/close is driven exclusively by UICoordinator via ForceOpen/ForceClose
    /// so both panels always move together.
    ///
    /// Setup:
    ///   1. Create a CraftingPanel under your UI Canvas (sibling to InventoryPanel)
    ///   2. Add a TabBar (HorizontalLayoutGroup) above the recipe list
    ///   3. Add RecipeList (ScrollView) on the left, DetailPane on the right
    ///   4. Assign references in the inspector
    ///   5. Populate the Tabs list with label + station pairs
    ///   6. Call Initialise() from InventoryBootstrap after inventory creation
    ///   7. Call UICoordinator.Initialise() last
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
        // the panel closes for placement. When placement ends UICoordinator
        // re-opens both panels via ReopenAfterPlacement().
        private bool _closedForPlacement;

        // Reference to coordinator for placement re-open callback.
        private UICoordinator _coordinator;

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

            // NOTE: OnToggleInventory is intentionally NOT subscribed here.
            // UICoordinator owns that subscription and drives open/close via
            // ForceOpen() / ForceClose() so both panels always move together.

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

            // Cache coordinator for placement re-open
            _coordinator = FindAnyObjectByType<UICoordinator>();

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

            if (_activeStation is BaseCraftingStation baseStation)
                baseStation.RefreshDiscovery(_inventory);

            RebuildRecipeList();
            ClearSelection();
            UpdateTabHighlights();
            UpdateTabBarVisibility();
            UpdateStationHeader();

            if (_recipeEntries.Count > 0)
                SelectRecipe(_recipeEntries[0].Recipe);
        }

        private void UpdateTabHighlights()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].SetActive(i == _activeTabIndex);
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
        // Public API — called by UICoordinator
        // =====================================================================

        /// <summary>
        /// Set the active crafting station without opening or closing the panel.
        /// Pass null to revert to the default tab's station.
        /// Called by UICoordinator.OpenWithStation() before ForceOpen().
        /// </summary>
        public void SetActiveStation(ICraftingStation station)
        {
            if (station == null)
            {
                _activeStation = tabs.Count > 0 && tabs[0].station != null
                    ? tabs[0].station
                    : (ICraftingStation)handCraftingStation;
                _activeTabIndex = 0;
            }
            else
            {
                _activeStation = station;
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
        /// Unconditionally open the crafting panel.
        /// Called by UICoordinator only — do not call directly from other systems.
        /// </summary>
        public void ForceOpen()
        {
            if (_isOpen) return;

            _isOpen = true;
            craftingPanel.SetActive(true);

            if (_activeStation is BaseCraftingStation baseStation)
                baseStation.RefreshDiscovery(_inventory);

            RebuildRecipeList();
            UpdateTabHighlights();
            UpdateTabBarVisibility();
            UpdateStationHeader();

            if (_selectedRecipe == null && _recipeEntries.Count > 0)
                SelectRecipe(_recipeEntries[0].Recipe);
        }

        /// <summary>
        /// Unconditionally close the crafting panel.
        /// Called by UICoordinator only — do not call directly from other systems.
        /// </summary>
        public void ForceClose()
        {
            if (!_isOpen) return;

            _isOpen = false;
            craftingPanel.SetActive(false);
            ClearSelection();
        }

        // =====================================================================
        // PlacementSystem callbacks — re-open after placement
        // =====================================================================

        private void OnPlacementEnded()
        {
            if (!_closedForPlacement) return;

            _closedForPlacement = false;

            // Re-open both panels through the coordinator so they stay in sync.
            if (_coordinator != null)
                _coordinator.ReopenAfterPlacement();
            else
            {
                // Fallback: coordinator not found — open crafting panel alone.
                Debug.LogWarning("[CraftingUIManager] UICoordinator not found — re-opening crafting panel alone.");
                ForceOpen();
            }
        }

        // =====================================================================
        // Inventory change handler
        // =====================================================================

        private void OnInventoryChanged()
        {
            if (!_isOpen) return;

            if (_activeStation is BaseCraftingStation baseStation)
                baseStation.RefreshDiscovery(_inventory);

            foreach (var entry in _recipeEntries)
            {
                bool canCraft = _activeStation.CanCraft(entry.Recipe, _inventory);
                entry.SetCraftable(canCraft);
            }

            if (_selectedRecipe != null && recipeDetail != null)
                recipeDetail.Refresh(_selectedRecipe, _inventory, _activeStation);
        }

        // =====================================================================
        // Recipe list management
        // =====================================================================

        private void RebuildRecipeList()
        {
            foreach (var entry in _recipeEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            _recipeEntries.Clear();

            if (_activeStation == null) return;

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

            if (_activeTabIndex >= 0 && _activeTabIndex < tabs.Count)
            {
                stationNameText.text = tabs[_activeTabIndex].label.ToUpper();
                return;
            }

            if (_activeStation != null)
            {
                stationNameText.text = GetStationDisplayName(_activeStation.StationType);
                return;
            }

            stationNameText.text = "CRAFTING";
        }

        private static string GetStationDisplayName(CraftingStationType type)
        {
            return type switch
            {
                CraftingStationType.HandCraft      => "CRAFTING",
                CraftingStationType.Workbench      => "WORKBENCH",
                CraftingStationType.CookingStation => "COOKING",
                CraftingStationType.BuildHammer    => "BUILDING",
                _                                  => type.ToString().ToUpper()
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

            foreach (var entry in _recipeEntries)
                entry.SetSelected(entry.Recipe == recipe);

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

                if (craftSound != null && _audioSource != null &&
                    Time.time >= lastCraftSoundTime + craftSoundCooldown)
                {
                    lastCraftSoundTime = Time.time;
                    _audioSource.pitch = 1f + UnityEngine.Random.Range(-craftPitchVariation, craftPitchVariation);
                    _audioSource.PlayOneShot(craftSound, craftVolume);
                }

                if (_selectedRecipe.result.category == ItemCategory.Buildable)
                {
                    // Mark for re-open, then close both panels via coordinator.
                    _closedForPlacement = true;
                    if (_coordinator != null)
                        _coordinator.ClosePanels();
                    else
                        ForceClose(); // fallback
                    return;
                }

                OnInventoryChanged();
            }
        }
    }
}