using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Attach to any prefab spawned by the ObjectSpawner.
    /// 
    /// Automatically detects HarvestableResource and hooks into OnDepleted:
    /// - Harvestable objects (trees, rocks) → marked DEPLETED, stump spawns on reload
    /// - Pickup objects (twigs, mushrooms) → call Remove(), gone forever
    /// 
    /// For pickups, call Remove() from your interaction script.
    /// For harvestables, just add this component — it auto-hooks via OnDepleted.
    /// </summary>
    public class SpawnedObjectTracker : MonoBehaviour
    {
        private ChunkManager _chunkManager;
        private int _spawnId;
        private bool _handled;

        private void Start()
        {
            _chunkManager = FindFirstObjectByType<ChunkManager>();
            _spawnId = ChunkManager.GetSpawnIdFromObject(gameObject);

            // Auto-hook into HarvestableResource if present
            var harvestable = GetComponent<InventorySystem.Harvesting.HarvestableResource>();
            if (harvestable != null)
            {
                harvestable.OnDepleted += OnHarvestableDepleted;
            }
        }

        /// <summary>
        /// Called automatically when HarvestableResource is depleted.
        /// Marks the object as DEPLETED so the stump/remnant spawns on reload.
        /// HarvestableResource handles destroying the GO and spawning the stump itself.
        /// </summary>
        private void OnHarvestableDepleted()
        {
            if (_handled) return;
            _handled = true;

            if (_chunkManager == null || _spawnId == 0) return;
            _chunkManager.DepleteSpawnedObject(transform.position, _spawnId);
        }

        /// <summary>
        /// Call this from your interaction scripts for objects that get fully removed
        /// (picked up twigs, mushrooms, etc.) — they will never respawn.
        /// </summary>
        public void Remove()
        {
            if (_handled) return;
            _handled = true;

            if (_chunkManager != null && _spawnId != 0)
            {
                _chunkManager.RemoveSpawnedObject(transform.position, _spawnId);
            }

            Destroy(gameObject);
        }
    }
}