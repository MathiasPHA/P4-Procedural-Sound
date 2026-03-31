using UnityEngine;

namespace InventorySystem.Building
{
    /// <summary>
    /// Marker component on placed world structures.
    /// Used by PlacementSystem for overlap detection (structures must be
    /// on a layer included in the obstacle mask).
    ///
    /// Automatically tracked by PlacedStructureManager for save/load persistence.
    /// Structures loaded from save data have wasSaveLoaded = true to prevent
    /// double-registration.
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
        [Tooltip("Reference back to the placeable data that spawned this. Required for saving.")]
        public PlaceableData sourceData;

        [HideInInspector]
        [Tooltip("Set to true when this structure was spawned by the save system, " +
                 "so it doesn't register itself again.")]
        public bool wasSaveLoaded = false;

        // Future fields:
        // public float health;
        // public float decayRate;
        // public bool isLit;  // for campfires

        /// <summary>
        /// Call this to demolish/remove the structure.
        /// Unregisters from save system and destroys the GameObject.
        /// </summary>
        public void Demolish()
        {
            var manager = ProceduralTerrain.PlacedStructureManager.Instance;
            if (manager != null)
                manager.UnregisterStructure(this);

            Destroy(gameObject);
        }
    }
}