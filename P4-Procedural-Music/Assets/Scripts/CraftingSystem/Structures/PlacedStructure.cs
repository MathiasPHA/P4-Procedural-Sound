using System;
using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;

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
    /// HP / DAMAGE:
    ///   The structure has its own HP, sourced from PlaceableData.maxHealth.
    ///   Only tools with ToolData.structureDamage > 0 (e.g. Hammer) can damage it.
    ///   When HP hits 0, OnDestroyed fires and Demolish() runs (unregisters from
    ///   save, spawns drops, and destroys the GameObject).
    ///
    /// DROPS:
    ///   Configure the Drops list in the Inspector to spawn world items when
    ///   the structure is demolished. Each entry pairs an ItemData with an
    ///   amount. Drops scatter away from the hit direction like harvestable
    ///   resources do. Leave the list empty for structures that shouldn't
    ///   drop anything (e.g. cosmetic decor).
    ///
    /// PREFAB SETUP:
    ///   1. Create your structure prefab (e.g. Campfire)
    ///   2. Add SpriteRenderer with the structure's sprite
    ///   3. Add a Collider2D matching the structure's footprint
    ///   4. Attach this component
    ///   5. Set the GameObject's layer to "Structures" (or whatever
    ///      your PlacementSystem's obstacle mask uses)
    ///   6. For comfort sources, also add ComfortInfluenceSource
    ///   7. (Optional) Configure the Drops list and assign worldItemPrefab
    ///      if you want demolition to drop materials
    /// </summary>
    public class PlacedStructure : MonoBehaviour
    {
        [Tooltip("Reference back to the placeable data that spawned this. Required for saving and HP sourcing.")]
        public PlaceableData sourceData;

        [HideInInspector]
        [Tooltip("Set to true when this structure was spawned by the save system, " +
                 "so it doesn't register itself again.")]
        public bool wasSaveLoaded = false;

        [Header("Hit Feedback")]
        [Tooltip("How much to shake on hit. Set to 0 to disable.")]
        [SerializeField] private float shakeIntensity = 0.08f;
        [SerializeField] private float shakeDuration = 0.12f;

        [Tooltip("Sound played each time this structure is hit by a tool.")]
        [SerializeField] private AudioClip hitSound;
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 0.5f;

        [Tooltip("Sound played when this structure is fully demolished.")]
        [SerializeField] private AudioClip demolishSound;
        [Range(0f, 1f)]
        [SerializeField] private float demolishVolume = 0.6f;

        [Header("Drops on Demolish")]
        [Tooltip("Items dropped when this structure is fully demolished. " +
                 "Leave empty for structures that shouldn't drop anything.")]
        [SerializeField] private List<StructureDrop> drops = new();

        [Tooltip("Prefab spawned for each dropped item — same WorldItem prefab used " +
                 "by HarvestableResource and InventoryUIManager. Required if Drops is non-empty.")]
        [SerializeField] private GameObject worldItemPrefab;

        [Tooltip("If true, drops go straight into the player's inventory instead of " +
                 "spawning as world items. Useful for small structures.")]
        [SerializeField] private bool directToInventory = false;

        // Runtime
        private bool _initialized = false;
        private int _maxHealth;
        private int _currentHealth;
        private Vector3 _originalPosition;
        private float _shakeTimer;

        // Cached at the moment of demolition so Demolish() can spawn drops
        // even though TakeDamage is the only caller that has hit-direction info.
        private Vector2 _lastHitDirection = Vector2.up;

        /// <summary>Current HP. Reads as 0 before initialization runs.</summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>Max HP for this structure (from PlaceableData.maxHealth, fallback 30).</summary>
        public int MaxHealth => _maxHealth;

        /// <summary>True once HP has reached 0 (Demolish has been or is being called).</summary>
        public bool IsDestroyed => _initialized && _currentHealth <= 0;

        /// <summary>Fired on each successful hit. (currentHealth, maxHealth)</summary>
        public event Action<int, int> OnHit;

        /// <summary>Fired once when HP reaches 0, just before Demolish() runs.</summary>
        public event Action OnDestroyed;

        // ──────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            _originalPosition = transform.position;
            // Don't read sourceData.maxHealth here — sourceData is assigned
            // by PlacementSystem / PlacedStructureManager AFTER Instantiate,
            // so it's null during Awake. Init runs on Start (live placement)
            // or RestoreHealth (save load), whichever comes first.
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                float t = _shakeTimer / shakeDuration;
                Vector2 offset = UnityEngine.Random.insideUnitCircle * shakeIntensity * t;
                transform.position = _originalPosition + (Vector3)offset;

                if (_shakeTimer <= 0f)
                    transform.position = _originalPosition;
            }
        }

        /// <summary>
        /// Idempotent. Pulls maxHealth from sourceData (fallback 30) and seeds
        /// currentHealth to max on first call. Subsequent calls are no-ops.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;
            _maxHealth = (sourceData != null && sourceData.maxHealth > 0) ? sourceData.maxHealth : 30;
            if (_currentHealth <= 0) _currentHealth = _maxHealth;
            _initialized = true;
        }

        // ──────────────────────────────────────────────────────────────
        // Damage
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Apply damage from a tool hit. Returns true if the structure was demolished.
        /// </summary>
        public bool TakeDamage(int damage, Vector2 hitDirection)
        {
            EnsureInitialized();
            if (IsDestroyed || damage <= 0) return false;

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            _lastHitDirection = hitDirection;

            // Shake feedback
            if (shakeIntensity > 0f)
            {
                _shakeTimer = shakeDuration;
                _originalPosition = transform.position;
            }

            // Hit sound (detached so it survives Destroy on demolish)
            if (hitSound != null)
                PlaySoundDetached(hitSound, transform.position, hitVolume);

            OnHit?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0)
            {
                OnDestroyed?.Invoke();
                if (demolishSound != null)
                    PlaySoundDetached(demolishSound, transform.position, demolishVolume);
                Demolish();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Restore HP from save data. Pass 0 (or negative) to load at full HP.
        /// Called by PlacedStructureManager during chunk load after sourceData is assigned.
        /// </summary>
        public void RestoreHealth(int hp)
        {
            // Manager calls this after sourceData is set — safe to read maxHealth here.
            _maxHealth = (sourceData != null && sourceData.maxHealth > 0) ? sourceData.maxHealth : 30;

            if (hp <= 0)
                _currentHealth = _maxHealth;
            else
                _currentHealth = Mathf.Clamp(hp, 1, _maxHealth);

            _initialized = true;
        }

        // ──────────────────────────────────────────────────────────────
        // Demolish
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Call this to demolish/remove the structure.
        /// Spawns configured drops, unregisters from save system, and destroys
        /// the GameObject.
        /// </summary>
        public void Demolish()
        {
            SpawnDrops(_lastHitDirection);

            var manager = ProceduralTerrain.PlacedStructureManager.Instance;
            if (manager != null)
                manager.UnregisterStructure(this);

            Destroy(gameObject);
        }

        // ──────────────────────────────────────────────────────────────
        // Drops
        // ──────────────────────────────────────────────────────────────

        private void SpawnDrops(Vector2 hitDirection)
        {
            if (drops == null || drops.Count == 0) return;

            foreach (var drop in drops)
            {
                if (drop == null || drop.item == null || drop.amount <= 0) continue;

                if (directToInventory)
                {
                    var inventory = InventoryBootstrap.PlayerInventory;
                    if (inventory != null)
                    {
                        int overflow = inventory.AddItem(drop.item, drop.amount);
                        if (overflow > 0)
                            Debug.LogWarning($"[PlacedStructure] {overflow}x {drop.item.displayName} didn't fit in inventory.");
                    }
                }
                else if (worldItemPrefab != null)
                {
                    SpawnWorldDrops(drop.item, drop.amount, hitDirection);
                }
                else
                {
                    Debug.LogWarning($"[PlacedStructure] {name} has drops configured but no worldItemPrefab assigned. " +
                                     "Drops won't spawn.");
                }
            }
        }

        private void SpawnWorldDrops(ItemData item, int amount, Vector2 hitDirection)
        {
            for (int i = 0; i < amount; i++)
            {
                var instance = new ItemInstance(item);
                var go = Instantiate(worldItemPrefab, transform.position, Quaternion.identity);
                var worldItem = go.GetComponent<UI.WorldItem>();

                // Items scatter away from the hit direction with some randomness —
                // mirrors HarvestableResource.SpawnDrops so demolish loot looks
                // and feels the same as resource loot.
                Vector2 scatterDir = -hitDirection.normalized + UnityEngine.Random.insideUnitCircle * 0.8f;
                worldItem?.Initialise(instance, 1, scatterDir);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────

        private static void PlaySoundDetached(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return;
            var go = new GameObject("StructureHitSound");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(go, clip.length + 0.1f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            int displayCur = Application.isPlaying ? _currentHealth
                                                   : (sourceData != null ? sourceData.maxHealth : 0);
            int displayMax = Application.isPlaying ? _maxHealth
                                                   : (sourceData != null ? sourceData.maxHealth : 0);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.8f,
                $"HP: {displayCur}/{displayMax}");
        }
#endif
    }

    /// <summary>
    /// One drop entry on a PlacedStructure. Pairs an ItemData with a quantity.
    /// Multiple entries can be configured per structure (e.g. a workbench
    /// dropping both wood and rope).
    /// </summary>
    [Serializable]
    public class StructureDrop
    {
        [Tooltip("The item that drops when this structure is demolished.")]
        public ItemData item;

        [Tooltip("How many of this item drop.")]
        [Min(1)] public int amount = 1;
    }
}