using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem.UI
{
    /// <summary>
    /// A dropped item in the game world. Displays the item sprite,
    /// has a collider for pickup detection, and stores the item data.
    ///
    /// The player picks this up by walking into it or pressing Interact nearby.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class WorldItem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float pickupDelay = 1.5f;
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobSpeed = 2f;

        [Header("Drop Physics")]
        [SerializeField] private float dropImpulse = 3f;
        [SerializeField] private float randomSpread = 0.5f;

        public ItemInstance Instance { get; private set; }
        public int Quantity { get; private set; }

        private SpriteRenderer _spriteRenderer;
        private float _spawnTime;
        private Vector3 _basePosition;

        public void Initialise(ItemInstance instance, int quantity, Vector2 launchDirection = default)
        {
            Instance = instance;
            Quantity = quantity;

            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer.sprite = instance.Data.icon;

            _spawnTime = Time.time;

            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0.5f;
                rb.linearDamping = 3f;

                // Use provided direction or fall back to random
                Vector2 dir = launchDirection.sqrMagnitude > 0.01f
                    ? launchDirection.normalized
                    : Random.insideUnitCircle.normalized;

                // Add a bit of random spread so multiple drops fan out
                dir += Random.insideUnitCircle * randomSpread;

                rb.AddForce(dir * dropImpulse, ForceMode2D.Impulse);
            }
        }

        private void Start()
        {
            _basePosition = transform.position;
        }

        private void Update()
        {
            // Gentle floating bob after settling
            if (Time.time - _spawnTime > 1f)
            {
                var pos = transform.position;
                pos.y = _basePosition.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
                transform.position = pos;
            }
        }

        /// <summary>
        /// Whether enough time has passed since spawning to allow pickup.
        /// Prevents instantly re-picking up a dropped item.
        /// </summary>
        public bool CanPickUp => Time.time - _spawnTime > pickupDelay;

        /// <summary>
        /// Attempt to pick up this item into the given inventory.
        /// Returns true if the item was fully picked up (and should be destroyed).
        /// </summary>
        public bool TryPickUp(Inventory inventory)
        {
            if (!CanPickUp || inventory == null) return false;

            bool success = inventory.AddInstance(Instance, Quantity);

            if (success)
            {
                Destroy(gameObject);
                return true;
            }

            return false;
        }
    }
}