using UnityEngine;
using InventorySystem;
using InventorySystem.Data;
using ProceduralTerrain;

namespace InteractionSystem
{
    /// <summary>
    /// Interactable for world items that get picked up into inventory.
    /// Sticks, mushrooms, flowers — anything the player walks to and grabs.
    ///
    /// On interact: faces the player, adds to inventory, destroys the object,
    /// and plays a pickup animation. The animation event on the last frame
    /// calls OnHarvestAnimationComplete() to return to idle.
    ///
    /// SETUP:
    ///   1. Attach to the pickup prefab
    ///   2. Set actionVerb to "Pick Up" in Inspector
    ///   3. Assign the ItemData and amount
    ///   4. Ensure the GameObject has a Collider2D on the "Interactable" layer
    ///   5. If spawned by ObjectSpawner, also has SpawnedObjectTracker
    ///   6. Add pickup animation clips to the Animator (PlayerPickUpLeft, PlayerPickUpRight)
    ///   7. Add an animation event on the last frame calling OnHarvestAnimationComplete()
    /// </summary>
    public class PickupInteractable : Interactable
    {
        [Header("Pickup")]
        [SerializeField] private ItemData itemData;
        [Min(1)][SerializeField] private int amount = 1;

        [Header("Audio")]
        [SerializeField] private AudioClip pickupSound;
        [Range(0f, 1f)][SerializeField] private float volume = 0.5f;
        [Range(0f, 0.5f)][SerializeField] private float pitchVariation = 0.15f;

        private bool _pickedUp;

        public override bool CanInteract() => !_pickedUp && itemData != null;

        public override void Interact(PlayerStateManager player)
        {
            if (_pickedUp) return;
            _pickedUp = true;

            // Face left or right toward the item
            float xDiff = transform.position.x - player.transform.position.x;
            player.playerDir = xDiff < 0 ? "Left" : "Right";

            // Add to inventory
            var inventory = InventoryBootstrap.PlayerInventory;
            if (inventory != null)
            {
                int overflow = inventory.AddItem(itemData, amount);
                if (overflow > 0)
                    Debug.LogWarning($"[PickupInteractable] {overflow}x {itemData.displayName} didn't fit.");
            }

            // Audio
            if (pickupSound != null)
            {
                var go = new GameObject("PickupSound");
                go.transform.position = transform.position;
                var source = go.AddComponent<AudioSource>();
                source.clip = pickupSound;
                source.volume = volume;
                source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                source.spatialBlend = 0f;
                source.Play();
                Destroy(go, pickupSound.length / Mathf.Max(source.pitch, 0.1f));
            }

            // Play pickup animation (animation event calls OnHarvestAnimationComplete)
            player.animationQue = "PickUp";
            player.StartHarvest();

            // Remove the world object
            var tracker = GetComponent<SpawnedObjectTracker>();
            if (tracker != null)
                tracker.Remove();
            else
                Destroy(gameObject);
        }
    }
}