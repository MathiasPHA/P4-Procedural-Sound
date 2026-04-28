using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem.Building
{
    /// <summary>
    /// Links an inventory item (category: Buildable) to the prefab that
    /// gets spawned in the world when placed, plus placement metadata.
    ///
    /// CREATE ONE PER BUILDABLE:
    ///   Right-click → Create → Inventory → Placeable Data
    ///   Assign the matching ItemData and the world prefab.
    ///   Set grid size (1x1 for campfire, 2x1 for workbench, etc.)
    /// </summary>
    [CreateAssetMenu(fileName = "New Placeable", menuName = "Inventory/Placeable Data")]
    public class PlaceableData : ScriptableObject
    {
        [Tooltip("The inventory item this placeable corresponds to.")]
        public ItemData item;

        [Tooltip("The prefab spawned in the world when placed. " +
                 "Should have a SpriteRenderer and a Collider2D.")]
        public GameObject prefab;

        [Tooltip("Size in grid cells. A campfire is 1x1, a long workbench might be 2x1.")]
        public Vector2Int gridCells = Vector2Int.one;

        [Header("Durability")]
        [Tooltip("Total damage this structure can absorb before being demolished. " +
                 "Only tools with structureDamage > 0 (e.g. Hammer) can deal damage. " +
                 "Default 30 = 6 hammer hits at structureDamage=5.")]
        [Min(1)]
        public int maxHealth = 30;
    }
}