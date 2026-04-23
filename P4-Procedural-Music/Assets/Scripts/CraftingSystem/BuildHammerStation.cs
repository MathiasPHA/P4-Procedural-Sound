using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Crafting station for the Build Hammer.
    /// All recipes assigned to this station should have Buildable results,
    /// which the base class automatically routes to PlacementSystem.
    ///
    /// This station simply provides a filtered recipe list and auto-discovery.
    /// The actual placement logic lives in BaseCraftingStation.CraftBuildable()
    /// and PlacementSystem.BeginPlacementFromRecipe().
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
    ///   - result       = the Buildable ItemData (used for icon/name display
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

        protected override void OnCraftSuccess(Recipe recipe, Inventory inventory)
        {
            Debug.Log($"[BuildHammer] Placed {recipe.result.displayName}");
        }

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