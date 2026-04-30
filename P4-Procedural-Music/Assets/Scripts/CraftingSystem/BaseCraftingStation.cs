using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;
using InventorySystem.Building;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Abstract base for crafting stations.
    /// Handles the shared logic: recipe loading, material checks,
    /// recipe discovery, and ingredient consumption.
    ///
    /// Authoring model:
    ///   The station's Recipe Database in the Inspector is the single source
    ///   of truth for what this station can craft. A recipe can be assigned
    ///   to multiple stations — drag it into each station's database. The
    ///   recipe's own stationType field is now an authoring tag (the recipe's
    ///   "primary home") and is NOT used to filter at runtime.
    ///
    /// If a recipe's result is a Buildable item, crafting enters placement
    /// mode instead of adding the item to inventory. This applies to ALL
    /// stations — HandCraft, BuildHammer, Workbench, etc. — so structures
    /// like campfires can be built from hand-crafting without needing a hammer.
    ///
    /// Concrete stations (Workbench, CookingStation, BuildHammer)
    /// inherit from this and override only what's unique.
    /// </summary>
    public abstract class BaseCraftingStation : MonoBehaviour, ICraftingStation
    {
        [Header("Station Configuration")]
        [SerializeField] private CraftingStationType stationType;

        [Tooltip("Recipes this station can craft. Authoritative — a recipe " +
                 "shows up here if and only if it appears in this list. " +
                 "The same recipe can be assigned to multiple stations.")]
        [SerializeField] private List<Recipe> recipeDatabase;

        public CraftingStationType StationType => stationType;

        // All recipes this station offers (resolved from the database in Awake)
        private List<Recipe> _stationRecipes = new();

        // Recipes the player has discovered (persisted per-save via IDs)
        private HashSet<string> _discoveredIds = new();
        private List<Recipe> _discoveredRecipes = new();

        public IReadOnlyList<Recipe> AllRecipes => _stationRecipes;
        public IReadOnlyList<Recipe> DiscoveredRecipes => _discoveredRecipes;

        protected virtual void Awake()
        {
            // The database itself is the source of truth — no type filtering.
            // Just copy non-null entries so the runtime list is safe to iterate.
            _stationRecipes = new List<Recipe>();

            if (recipeDatabase == null || recipeDatabase.Count == 0)
            {
                Debug.LogWarning(
                    $"[{name}] Crafting station has no recipes assigned " +
                    $"(StationType={stationType}). " +
                    "Drag Recipe assets into the Recipe Database in the Inspector.");
                return;
            }

            int nullCount = 0;
            foreach (var recipe in recipeDatabase)
            {
                if (recipe == null)
                {
                    nullCount++;
                    continue;
                }
                _stationRecipes.Add(recipe);
            }

            if (nullCount > 0)
            {
                Debug.LogWarning(
                    $"[{name}] Recipe Database contains {nullCount} null " +
                    "entry(ies). Clean up the list in the Inspector.");
            }
        }

        // =====================================================================
        // Discovery
        // =====================================================================

        /// <summary>
        /// Call this whenever the inventory changes (subscribe to OnInventoryChanged).
        /// Checks all undiscovered recipes and marks them as discovered
        /// if the player currently holds all required materials.
        /// </summary>
        public void RefreshDiscovery(Inventory inventory)
        {
            bool changed = false;

            foreach (var recipe in _stationRecipes)
            {
                if (_discoveredIds.Contains(recipe.recipeId)) continue;

                if (CanCraft(recipe, inventory))
                {
                    _discoveredIds.Add(recipe.recipeId);
                    changed = true;
                }
            }

            if (changed)
            {
                RebuildDiscoveredList();
            }
        }

        private void RebuildDiscoveredList()
        {
            _discoveredRecipes.Clear();
            foreach (var recipe in _stationRecipes)
            {
                if (_discoveredIds.Contains(recipe.recipeId))
                {
                    _discoveredRecipes.Add(recipe);
                }
            }
        }

        // =====================================================================
        // Crafting
        // =====================================================================

        public bool CanCraft(Recipe recipe, Inventory inventory)
        {
            if (recipe == null || inventory == null) return false;

            foreach (var req in recipe.ingredients)
            {
                if (!inventory.HasItem(req.item.id, req.amount))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Craft a recipe. If the result is a Buildable item, enters placement
        /// mode via PlacementSystem (ingredients consumed on actual placement).
        /// Otherwise, consumes ingredients immediately and adds result to inventory.
        /// </summary>
        public virtual bool Craft(Recipe recipe, Inventory inventory)
        {
            if (!CanCraft(recipe, inventory)) return false;

            // ── Buildable results → placement mode (no immediate consumption) ──
            if (recipe.result.category == ItemCategory.Buildable)
            {
                return CraftBuildable(recipe, inventory);
            }

            // ── Normal crafting: consume ingredients, produce result ──
            foreach (var req in recipe.ingredients)
            {
                inventory.RemoveItem(req.item.id, req.amount);
            }

            int overflow = inventory.AddItem(recipe.result, recipe.resultAmount);

            if (overflow > 0)
            {
                Debug.LogWarning(
                    $"[Crafting] {overflow}x {recipe.result.displayName} could not fit. " +
                    "Consider spawning as world item.");
                OnCraftOverflow(recipe, inventory, overflow);
            }

            OnCraftSuccess(recipe, inventory);
            return true;
        }

        /// <summary>
        /// Routes a Buildable recipe to PlacementSystem for ghost placement.
        /// Ingredients are NOT consumed here — PlacementSystem consumes them
        /// when the player actually clicks to place the structure.
        /// </summary>
        private bool CraftBuildable(Recipe recipe, Inventory inventory)
        {
            var placement = PlacementSystem.Instance;
            if (placement == null)
            {
                Debug.LogError("[BaseCraftingStation] PlacementSystem.Instance is null — " +
                               "cannot enter placement mode.");
                return false;
            }

            bool started = placement.BeginPlacementFromRecipe(recipe);

            if (started)
            {
                Debug.Log($"[Crafting] Entering placement mode for {recipe.result.displayName}");
                OnCraftSuccess(recipe, inventory);
            }

            return started;
        }

        /// <summary>
        /// Hook for subclasses to react to a successful craft
        /// (e.g. play animation, trigger sound, start cook timer).
        /// </summary>
        protected virtual void OnCraftSuccess(Recipe recipe, Inventory inventory) { }

        /// <summary>
        /// Hook for subclasses to handle items that didn't fit in inventory
        /// (e.g. spawn as world drop near the station).
        /// </summary>
        protected virtual void OnCraftOverflow(Recipe recipe, Inventory inventory, int overflow) { }

        // =====================================================================
        // Save / Load for discovery state
        // =====================================================================

        public List<string> GetDiscoveredRecipeIds() => new(_discoveredIds);

        public void LoadDiscoveredRecipeIds(List<string> ids)
        {
            _discoveredIds = new HashSet<string>(ids);
            RebuildDiscoveredList();
        }
    }
}