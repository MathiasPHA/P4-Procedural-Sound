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
        private Vector2Int _chunkCoord;
        private bool _handled;
        private bool _initialized;

        public void Init(int spawnId, Vector2Int chunkCoord)
        {
            _spawnId = spawnId;
            _chunkCoord = chunkCoord;
            _initialized = true;
        }

        private void Start()
        {
            _chunkManager = ChunkManager.Instance;
            if (_chunkManager == null)
                _chunkManager = FindFirstObjectByType<ChunkManager>();

            if (!_initialized)
                Debug.LogWarning($"[SpawnedObjectTracker] {gameObject.name} — Init() was never called! SpawnId will be 0.");

            // Auto-hook into HarvestableResource if present
            var harvestable = GetComponent<InventorySystem.Harvesting.HarvestableResource>();
            if (harvestable != null)
            {
                harvestable.OnDepleted += OnHarvestableDepleted;
                Debug.Log($"[SpawnedObjectTracker] Hooked OnDepleted on {gameObject.name} (spawnId={_spawnId}, chunk={_chunkCoord})");
            }
        }

        private void OnHarvestableDepleted()
        {
            if (_handled) return;
            _handled = true;

            if (_chunkManager == null)
            {
                Debug.LogWarning($"[SpawnedObjectTracker] {gameObject.name} depleted but ChunkManager is null — not tracked!");
                return;
            }

            if (_spawnId == 0)
            {
                Debug.LogWarning($"[SpawnedObjectTracker] {gameObject.name} depleted but spawnId is 0 — not tracked!");
                return;
            }

            Debug.Log($"[SpawnedObjectTracker] Depleting {gameObject.name} spawnId={_spawnId} chunk={_chunkCoord}");
            _chunkManager.DepleteSpawnedObject(_chunkCoord, _spawnId);
        }

        /// <summary>
        /// Call this from your interaction scripts for objects that get fully removed
        /// (picked up twigs, mushrooms, etc.) — they will never respawn.
        /// </summary>
        public void Remove()
        {
            if (_handled) return;
            _handled = true;

            if (_chunkManager == null)
            {
                Debug.LogWarning($"[SpawnedObjectTracker] {gameObject.name} removed but ChunkManager is null — not tracked!");
            }
            else if (_spawnId == 0)
            {
                Debug.LogWarning($"[SpawnedObjectTracker] {gameObject.name} removed but spawnId is 0 — not tracked!");
            }
            else
            {
                Debug.Log($"[SpawnedObjectTracker] Removing {gameObject.name} spawnId={_spawnId} chunk={_chunkCoord}");
                _chunkManager.RemoveSpawnedObject(_chunkCoord, _spawnId);
            }

            Destroy(gameObject);
        }
    }
}