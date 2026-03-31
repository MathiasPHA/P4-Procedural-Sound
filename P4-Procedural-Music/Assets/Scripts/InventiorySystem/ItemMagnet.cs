using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem
{
    /// <summary>
    /// Attach to the Player. Pulls nearby WorldItems toward the player
    /// and collects them once they're close enough.
    ///
    /// Uses a trigger CircleCollider2D as the detection radius.
    /// Items inside the radius accelerate toward the player each frame.
    /// When they reach the pickup threshold, they're added to inventory.
    ///
    /// SETUP:
    ///   1. Attach this script to the Player GameObject
    ///   2. A trigger CircleCollider2D is created automatically (or uses an existing one)
    ///   3. Tag the Player as "Player" (for WorldItem's own logic if needed)
    ///   4. Tweak magnet radius, pull speed, and collect distance in the inspector
    ///
    /// NOTES:
    ///   - Items that don't fit in inventory are released (stop being pulled)
    ///   - Respects WorldItem.CanPickUp (the short delay after spawning)
    ///   - Pull uses MoveTowards with acceleration so it feels snappy, not floaty
    /// </summary>
    public class ItemMagnet : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Radius within which items start being pulled toward the player.")]
        [SerializeField] private float magnetRadius = 3f;

        [Header("Pull Behaviour")]
        [Tooltip("Initial pull speed in units/sec. Ramps up as items get closer.")]
        [SerializeField] private float pullSpeed = 5f;

        [Tooltip("Multiplier applied as items get closer (speed = pullSpeed * (1 + acceleration * closeness)).")]
        [SerializeField] private float acceleration = 3f;

        [Header("Collection")]
        [Tooltip("Distance at which items are actually picked up into inventory.")]
        [SerializeField] private float collectDistance = 0.4f;

        [Header("Pickup Sound")]
        [Tooltip("Sound played when an item is collected.")]
        [SerializeField] private AudioClip pickupClip;

        [Tooltip("Base volume for the pickup sound.")]
        [Range(0f, 1f)]
        [SerializeField] private float pickupVolume = 0.5f;

        [Tooltip("How much the pitch varies randomly each pickup (e.g. 0.15 = ±15%).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float pitchVariation = 0.15f;

        // Items currently being pulled
        private readonly List<UI.WorldItem> _pulledItems = new();

        // Items that failed to fit — stop pulling until they leave and re-enter
        private readonly HashSet<UI.WorldItem> _rejected = new();

        private CircleCollider2D _triggerCollider;
        private AudioSource _audioSource;

        private void Start()
        {
            // Find or create the magnet trigger collider
            _triggerCollider = GetMagnetCollider();

            if (_triggerCollider == null)
            {
                _triggerCollider = gameObject.AddComponent<CircleCollider2D>();
                _triggerCollider.isTrigger = true;
            }

            _triggerCollider.radius = magnetRadius;

            // Find or create an AudioSource for pickup sounds
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D sound
        }

        /// <summary>
        /// Finds an existing trigger CircleCollider2D that isn't the player's
        /// physics collider, or returns null so we create one.
        /// </summary>
        private CircleCollider2D GetMagnetCollider()
        {
            var colliders = GetComponents<CircleCollider2D>();
            foreach (var col in colliders)
            {
                if (col.isTrigger) return col;
            }
            return null;
        }

        private void Update()
        {
            if (InventoryBootstrap.PlayerInventory == null) return;

            // --- Overlap scan: catch items that spawned inside the radius ---
            // OnTriggerEnter2D doesn't fire for items already overlapping,
            // so we do a quick physics query each frame to find stragglers.
            var hits = Physics2D.OverlapCircleAll(transform.position, magnetRadius);
            foreach (var hit in hits)
            {
                var wi = hit.GetComponent<UI.WorldItem>();
                if (wi == null) continue;
                if (_rejected.Contains(wi)) continue;
                if (!_pulledItems.Contains(wi))
                    _pulledItems.Add(wi);
            }

            // Iterate backwards so we can remove while iterating
            for (int i = _pulledItems.Count - 1; i >= 0; i--)
            {
                var worldItem = _pulledItems[i];

                // Item was destroyed or picked up by something else
                if (worldItem == null)
                {
                    _pulledItems.RemoveAt(i);
                    continue;
                }

                // Respect the spawn delay
                if (!worldItem.CanPickUp) continue;

                Vector2 playerPos = transform.position;
                Vector2 itemPos = worldItem.transform.position;
                float distance = Vector2.Distance(playerPos, itemPos);

                // Close enough to collect
                if (distance <= collectDistance)
                {
                    if (worldItem.TryPickUp(InventoryBootstrap.PlayerInventory))
                    {
                        // TryPickUp destroys the GameObject, which triggers
                        // OnTriggerExit2D and removes it from _pulledItems.
                        // Don't RemoveAt here — the list has already been modified.
                        PlayPickupSound();
                        continue;
                    }
                    else
                    {
                        // Inventory full — stop pulling this item
                        _pulledItems.RemoveAt(i);
                        _rejected.Add(worldItem);
                    }
                    continue;
                }

                // Pull toward player with acceleration based on closeness
                float closeness = 1f - Mathf.Clamp01(distance / magnetRadius);
                float speed = pullSpeed * (1f + acceleration * closeness);

                // Kill the item's own physics velocity so our pull takes over
                var rb = worldItem.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.gravityScale = 0f;
                }

                worldItem.transform.position = Vector2.MoveTowards(
                    itemPos, playerPos, speed * Time.deltaTime);
            }
        }

        // =====================================================================
        // Trigger detection
        // =====================================================================

        private void OnTriggerEnter2D(Collider2D other)
        {
            var worldItem = other.GetComponent<UI.WorldItem>();
            if (worldItem == null) return;

            // Don't re-pull items that were already rejected (inventory full)
            if (_rejected.Contains(worldItem)) return;

            if (!_pulledItems.Contains(worldItem))
            {
                _pulledItems.Add(worldItem);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var worldItem = other.GetComponent<UI.WorldItem>();
            if (worldItem == null) return;

            _pulledItems.Remove(worldItem);

            // If a rejected item leaves the radius, allow it to be pulled again
            // next time it enters (player may have freed up inventory space)
            _rejected.Remove(worldItem);
        }

        // =====================================================================
        // Audio
        // =====================================================================

        private void PlayPickupSound()
        {
            if (pickupClip == null || _audioSource == null) return;

            _audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            _audioSource.PlayOneShot(pickupClip, pickupVolume);
        }

        // =====================================================================
        // Runtime tuning
        // =====================================================================

        /// <summary>
        /// Update the magnet radius at runtime (e.g. from an upgrade or buff).
        /// </summary>
        public void SetRadius(float radius)
        {
            magnetRadius = radius;
            if (_triggerCollider != null)
                _triggerCollider.radius = magnetRadius;
        }

        private void OnValidate()
        {
            // Keep inspector changes in sync during play mode
            if (_triggerCollider != null)
                _triggerCollider.radius = magnetRadius;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Magnet radius
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, magnetRadius);

            // Collect distance
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, collectDistance);
        }
#endif
    }
}