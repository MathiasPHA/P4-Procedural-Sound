using System;
using UnityEngine;

namespace InventorySystem.Crafting
{
    public enum CraftingStationType
    {
        HandCraft,
        Workbench,
        CookingStation,
        BuildHammer
    }

    [Serializable]
    public struct ItemRequirement
    {
        public Data.ItemData item;
        [Min(1)] public int amount;
    }

    [CreateAssetMenu(fileName = "New Recipe", menuName = "Inventory/Recipe")]
    public class Recipe : ScriptableObject
    {
        [Header("Identity")]
        public string recipeId;

        [Tooltip("Authoring tag — the recipe's 'primary' station for organisation. " +
                 "NOT enforced at runtime: a recipe is craftable wherever it's added " +
                 "to that station's Recipe Database. The same recipe can live in " +
                 "multiple station databases (e.g. a stick recipe at HandCraft AND Workbench).")]
        public CraftingStationType stationType;

        [Header("Ingredients")]
        public ItemRequirement[] ingredients;

        [Header("Result")]
        public Data.ItemData result;
        [Min(1)] public int resultAmount = 1;

        [Header("Optional")]
        [Tooltip("Craft time in seconds. 0 = instant.")]
        [Min(0)] public float craftTime;
    }
}