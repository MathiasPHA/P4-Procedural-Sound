using UnityEngine;

namespace InventorySystem.Building
{
    /// <summary>
    /// Marker component on placed world structures.
    /// Used by PlacementSystem for overlap detection (structures must be
    /// on a layer included in the obstacle mask).
    ///
    /// Add gameplay data here later: health, decay timer, comfort
    /// contribution, snapping points, etc.
    ///
    /// PREFAB SETUP:
    ///   1. Create your structure prefab (e.g. Campfire)
    ///   2. Add SpriteRenderer with the structure's sprite
    ///   3. Add a Collider2D matching the structure's footprint
    ///   4. Attach this component
    ///   5. Set the GameObject's layer to "Structures" (or whatever
    ///      your PlacementSystem's obstacle mask uses)
    ///   6. For comfort sources, also add ComfortInfluenceSource
    /// </summary>
    public class PlacedStructure : MonoBehaviour
    {
        [Tooltip("Optional reference back to the placeable data that spawned this.")]
        public PlaceableData sourceData;

        // Future fields:
        // public float health;
        // public float decayRate;
        // public bool isLit;  // for campfires
    }
}
