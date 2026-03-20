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
        [SerializeField] private float pickupDelay = 0.3f;
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobSpeed = 2f;

        public ItemInstance Instance { get; private set; }
        public int Quantity { get; private set; }

        private SpriteRenderer _spriteRenderer;
        private float _spawnTime;
        private Vector3 _basePosition;

        public void Initialise(ItemInstance instance, int quantity)
        {
            Instance = instance;
            Quantity = quantity;

            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer.sprite = instance.Data.icon;

            _spawnTime = Time.time;

            // Small random impulse so items spread out when dropped
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0.5f;
                rb.linearDamping = 3f;
                var randomDir = Random.insideUnitCircle * 1.5f;
                rb.AddForce(randomDir, ForceMode2D.Impulse);
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
