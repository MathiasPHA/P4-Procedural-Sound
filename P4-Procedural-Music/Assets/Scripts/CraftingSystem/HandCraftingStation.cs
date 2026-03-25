using UnityEngine;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Default crafting station representing hand-crafting.
    /// Always available — the player doesn't need to be near anything.
    ///
    /// Assign a CraftingStationType to this (e.g. Workbench for now)
    /// and populate the recipe database with all recipes in the game.
    /// The base class filters to only those matching this station type.
    ///
    /// SETUP:
    ///   1. Create an empty GameObject in the scene (e.g. "HandCraftingStation")
    ///   2. Attach this script
    ///   3. Set Station Type to "HandCraft"
    ///   4. Drag all Recipe assets into the Recipe Database list
    ///   5. Assign this to CraftingUIManager's handCraftingStation field
    ///
    /// DISCOVERY:
    ///   Unlike station-locked recipes, hand-crafting recipes are auto-discovered
    ///   on Awake so the player sees them immediately without needing to
    ///   gather materials first. Override this behaviour by commenting out
    ///   the ForceDiscoverAll() call if you want material-gated discovery.
    /// </summary>
    public class HandCraftingStation : BaseCraftingStation
    {
        [Header("Hand Crafting")]
        [Tooltip("If true, all recipes for this station type are immediately " +
                 "visible in the list without the player needing to hold the materials first.")]
        [SerializeField] private bool discoverAllOnStart = true;

        protected override void Awake()
        {
            base.Awake();

            if (discoverAllOnStart)
            {
                ForceDiscoverAll();
            }
        }

        /// <summary>
        /// Marks every recipe assigned to this station as discovered
        /// so they all appear in the recipe list from the start.
        /// </summary>
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

        /// <summary>
        /// Hand-crafting has no special success behaviour.
        /// Override in subclasses for animations, sounds, etc.
        /// </summary>
        protected override void OnCraftSuccess(Recipe recipe, Data.Inventory inventory)
        {
            Debug.Log($"[HandCrafting] Crafted {recipe.resultAmount}x {recipe.result.displayName}");
        }
    }
}
