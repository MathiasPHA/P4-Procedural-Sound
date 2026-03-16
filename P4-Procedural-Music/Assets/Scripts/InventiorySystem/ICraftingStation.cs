using System.Collections.Generic;
using InventorySystem.Data;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// Interface for anything that can craft items.
    /// Implemented by all crafting stations and the build hammer.
    /// </summary>
    public interface ICraftingStation
    {
        CraftingStationType StationType { get; }

        /// <summary>
        /// All recipes this station knows about, regardless of whether
        /// the player currently has the materials.
        /// </summary>
        IReadOnlyList<Recipe> AllRecipes { get; }

        /// <summary>
        /// Recipes the player has discovered (i.e. has held all required
        /// materials at some point). Subset of AllRecipes.
        /// </summary>
        IReadOnlyList<Recipe> DiscoveredRecipes { get; }

        /// <summary>
        /// Whether the player currently has all materials for a recipe.
        /// </summary>
        bool CanCraft(Recipe recipe, Inventory inventory);

        /// <summary>
        /// Execute the craft: consume ingredients, produce result.
        /// Returns true if successful.
        /// </summary>
        bool Craft(Recipe recipe, Inventory inventory);
    }
}
