using System;
using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem.Harvesting
{
    /// <summary>
    /// A world object that can be harvested with the correct tool type.
    /// Trees, rocks, bushes, ore deposits — anything the player hits
    /// with a tool to extract resources.
    ///
    /// SETUP:
    ///   1. Attach to the resource GameObject (e.g. a tree)
    ///   2. Add a Collider2D (non-trigger) so OverlapCircle can detect it
    ///   3. Set the required tool type (Axe for trees, Pickaxe for rocks)
    ///   4. Configure health and drops
    ///   5. Optionally assign a stump/depleted prefab to swap in when destroyed
    ///   6. Assign the worldItemPrefab (same one used by InventoryUIManager for drops)
    /// </summary>
    public class HarvestableResource : MonoBehaviour
    {
        [Header("Tool Requirement")]
        [Tooltip("Which tool type can harvest this resource.")]
        [SerializeField] private ToolType requiredToolType;

        [Header("Health")]
        [Tooltip("Total hits (damage) this resource can take before being destroyed.")]
        [SerializeField] private int maxHealth = 5;

        [Header("Drops")]
        [Tooltip("The item dropped when this resource is harvested.")]
        [SerializeField] private ItemData dropItem;

        [Tooltip("How many items drop when the resource is destroyed.")]
        [Min(1)] [SerializeField] private int dropAmount = 3;

        [Header("World Drop")]
        [Tooltip("Prefab with WorldItem, SpriteRenderer, Collider2D, Rigidbody2D. " +
                 "Same prefab used for inventory drops.")]
        [SerializeField] private GameObject worldItemPrefab;

        [Header("Visuals")]
        [Tooltip("Optional prefab to spawn in place when destroyed (e.g. a tree stump).")]
        [SerializeField] private GameObject depletedPrefab;

        [Header("Feedback")]
        [Tooltip("How much to shake on hit. Set to 0 to disable.")]
        [SerializeField] private float shakeIntensity = 0.1f;
        [SerializeField] private float shakeDuration = 0.15f;

        // Runtime
        private int _currentHealth;
        private Vector3 _originalPosition;
        private float _shakeTimer;

        /// <summary>The tool type required to harvest this resource.</summary>
        public ToolType RequiredToolType => requiredToolType;

        /// <summary>Whether this resource has been fully depleted.</summary>
        public bool IsDepleted => _currentHealth <= 0;

        /// <summary>Fired when the resource takes a hit. (currentHealth, maxHealth)</summary>
        public event Action<int, int> OnHit;

        /// <summary>Fired when the resource is fully destroyed.</summary>
        public event Action OnDepleted;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _originalPosition = transform.position;
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
        /// Apply damage from a tool hit. Returns true if the resource was destroyed.
        /// </summary>
        public bool TakeDamage(int damage, Vector2 hitDirection)
        {
            if (IsDepleted) return false;

            _currentHealth -= damage;
            _currentHealth = Mathf.Max(0, _currentHealth);

            // Shake feedback
            _shakeTimer = shakeDuration;
            _originalPosition = transform.position;

            OnHit?.Invoke(_currentHealth, maxHealth);

            if (_currentHealth <= 0)
            {
                Deplete(hitDirection);
                return true;
            }

            return false;
        }

        private void Deplete(Vector2 hitDirection)
        {
            // Spawn drops
            if (dropItem != null && worldItemPrefab != null)
            {
                SpawnDrops(dropAmount, hitDirection);
            }

            OnDepleted?.Invoke();

            // Swap to stump or destroy
            if (depletedPrefab != null)
            {
                Instantiate(depletedPrefab, transform.position, transform.rotation);
            }

            Destroy(gameObject);
        }

        private void SpawnDrops(int amount, Vector2 hitDirection)
        {
            for (int i = 0; i < amount; i++)
            {
                var instance = new ItemInstance(dropItem);
                var go = Instantiate(worldItemPrefab, transform.position, Quaternion.identity);
                var worldItem = go.GetComponent<UI.WorldItem>();

                // Items scatter away from the hit direction with some randomness
                Vector2 scatterDir = -hitDirection.normalized + UnityEngine.Random.insideUnitCircle * 0.8f;
                worldItem?.Initialise(instance, 1, scatterDir);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Show health as a label
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1f,
                $"HP: {(Application.isPlaying ? _currentHealth : maxHealth)}/{maxHealth}");
        }
#endif
    }
}
