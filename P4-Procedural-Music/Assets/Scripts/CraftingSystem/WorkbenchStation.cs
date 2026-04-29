using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Crafting station for the workbench — a tier-1 build hub that unlocks
    /// more advanced recipes than hand-crafting (tools, basic structures,
    /// processed materials, etc.).
    ///
    /// Activated when the player is in range of a placed workbench — either
    /// via proximity (CraftingStationInteraction) or click-to-interact
    /// (WorldStationInteractable + OpenCraftingUIAction). Recipes assigned
    /// to this station are auto-discovered by default — the workbench is a
    /// visible, deliberate piece of progression, not something the player
    /// needs to figure out.
    ///
    /// SETUP (prefab):
    ///   1. Create a GameObject with:
    ///        - SpriteRenderer (workbench sprite)
    ///        - Collider2D (solid footprint, on obstacle layer)
    ///        - Second trigger Collider2D (interaction radius)
    ///   2. Attach this script
    ///   3. Set Station Type to "Workbench"
    ///   4. Drag all workbench Recipe assets into the Recipe Database list
    ///   5. Attach PlacedStructure (references the PlaceableData asset)
    ///   6. Attach WorldStationInteractable + OpenCraftingUIAction
    ///      (or CraftingStationInteraction for proximity-only activation)
    ///   7. (Optional) Attach ComfortInfluenceSource for a small comfort
    ///      radius — workbenches feel like home base
    ///   8. Save as prefab, assign to PlaceableData.prefab
    ///
    /// HOW TO BUILD ONE IN-GAME:
    ///   - Create a Workbench ItemData (Buildable = true) and a matching
    ///     PlaceableData pointing at this prefab
    ///   - Create a Recipe with stationType = BuildHammer and result =
    ///     the Workbench ItemData
    ///   - Add the recipe to BuildHammerStation's Recipe Database
    ///   - Run Right-click → Auto-Populate Placeables on PlacementSystem
    ///
    /// RECIPE CONVENTION (workbench-crafted recipes):
    ///   - stationType = Workbench
    ///   - ingredients  = processed materials (planks, rope, leather, etc.)
    ///   - result       = tools, weapons, advanced structures, or refined materials
    /// </summary>
    public class WorkbenchStation : BaseCraftingStation
    {
        [Header("Workbench")]
        [Tooltip("If true, all workbench recipes are visible immediately " +
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
            Debug.Log($"[Workbench] Crafted {recipe.resultAmount}x {recipe.result.displayName}");
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
