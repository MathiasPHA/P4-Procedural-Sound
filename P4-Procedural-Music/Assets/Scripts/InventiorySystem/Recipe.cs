using System;
using UnityEngine;

namespace InventorySystem.Crafting
{
    public enum CraftingStationType
    {
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

        [Tooltip("Which station type can craft this recipe")]
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
