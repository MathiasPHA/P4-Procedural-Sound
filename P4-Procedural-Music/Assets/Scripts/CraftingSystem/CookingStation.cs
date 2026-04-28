using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Crafting station for cooking food over a fire.
    /// Activated when the player is in range of a placed cooking station
    /// (via CraftingStationInteraction). Recipes assigned to this station
    /// are auto-discovered — cooking is a visible, deliberate activity,
    /// not something the player needs to figure out.
    ///
    /// SETUP (prefab):
    ///   1. Create a GameObject with:
    ///        - SpriteRenderer
    ///        - Collider2D (solid footprint, on obstacle layer)
    ///        - Second trigger Collider2D (interaction radius)
    ///   2. Attach this script
    ///   3. Set Station Type to "CookingStation"
    ///   4. Drag all cooking Recipe assets into the Recipe Database list
    ///   5. Attach PlacedStructure (references the PlaceableData asset)
    ///   6. Attach CraftingStationInteraction and wire the station + UI manager refs
    ///   7. (Optional) Attach ComfortInfluenceSource if the station should warm
    ///      the player like a campfire
    ///   8. Save as prefab, assign to PlaceableData.prefab
    ///
    /// RECIPE CONVENTION:
    ///   - stationType = CookingStation
    ///   - ingredients  = raw food (raw meat, fish, berries) + optional fuel
    ///   - result       = cooked food ItemData (Consumable with higher hungerRestore)
    /// </summary>
    public class CookingStation : BaseCraftingStation
    {
        [Header("Cooking Station")]
        [Tooltip("If true, all cooking recipes are visible immediately " +
                 "without the player needing to hold ingredients first.")]
        [SerializeField] private bool discoverAllOnStart = true;

        protected override void Awake()
        {
            base.Awake();

            if (discoverAllOnStart)
                ForceDiscoverAll();
        }

        protected override void OnCraftSuccess(Recipe recipe, Inventory inventory)
        {
            Debug.Log($"[CookingStation] Cooked {recipe.resultAmount}x {recipe.result.displayName}");
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
