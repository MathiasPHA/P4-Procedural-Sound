namespace InventorySystem.Data
{
    /// <summary>
    /// Types of tools. Matched against HarvestableResource.requiredToolType
    /// to determine which tool can harvest which resource.
    /// </summary>
    public enum ToolType
    {
        None,
        Axe,
        Pickaxe,
        Shovel,
        Hoe,
        Hammer,
        Spear
    }
}
