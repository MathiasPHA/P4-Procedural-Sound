using UnityEngine;
using InventorySystem.Data;
using InventorySystem.Building;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Crafting station for the Build Hammer.
    /// Instead of consuming ingredients and producing an item,
    /// Craft() hands the recipe to PlacementSystem for ghost placement.
    /// Ingredients are consumed only when the player actually places the structure.
    ///
    /// SETUP:
    ///   1. Create a persistent GameObject (e.g. "BuildHammerStation")
    ///   2. Attach this script
    ///   3. Set Station Type to "BuildHammer"
    ///   4. Drag all structure Recipe assets into the Recipe Database list
    ///      (each recipe's result must be a Buildable ItemData with a matching
    ///       PlaceableData in PlacementSystem's placeables list)
    ///   5. Assign to BuildHammerEquipHandler's buildHammerStation field
    ///
    /// RECIPE CONVENTION:
    ///   - stationType = BuildHammer
    ///   - ingredients  = raw materials (wood, stone, etc.)
    ///   - result       = the Buildable ItemData (used only for icon/name display
    ///                     and to look up PlaceableData — never added to inventory)
    /// </summary>
    public class BuildHammerStation : BaseCraftingStation
    {
        [Header("Build Hammer")]
        [Tooltip("If true, all build recipes are visible immediately " +
                 "without the player needing to hold materials first.")]
        [SerializeField] private bool discoverAllOnStart = true;

        protected override void Awake()
        {
            base.Awake();

            if (discoverAllOnStart)
                ForceDiscoverAll();
        }

        /// <summary>
        /// Override: don't consume ingredients or produce an item.
        /// Instead, tell PlacementSystem to begin recipe-driven placement.
        /// Ingredients are consumed at actual placement time.
        /// </summary>
        public override bool Craft(Recipe recipe, Inventory inventory)
        {
            if (recipe == null || recipe.result == null) return false;

            // Verify the player can afford it (visual check — placement will re-check)
            if (!CanCraft(recipe, inventory)) return false;

            // Hand off to PlacementSystem
            var placement = PlacementSystem.Instance;
            if (placement == null)
            {
                Debug.LogError("[BuildHammerStation] PlacementSystem.Instance is null!");
                return false;
            }

            bool started = placement.BeginPlacementFromRecipe(recipe);

            if (started)
                OnCraftSuccess(recipe, inventory);

            return started;
        }

        protected override void OnCraftSuccess(Recipe recipe, Inventory inventory)
        {
            Debug.Log($"[BuildHammer] Entering placement mode for {recipe.result.displayName}");
        }

        // -----------------------------------------------------------------

        private void ForceDiscoverAll()
        {
            var ids = new System.Collections.Generic.List<string>();

            foreach (var recipe in AllRecipes)
            {
                if (recipe != null)
                    ids.Add(recipe.recipeId);
            }

            LoadDiscoveredRecipeIds(ids);
        }
    }
}
