using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem.Audio
{
    /// <summary>
    /// Listens for item consumption events and plays the associated sound.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ConsumableAudioListener : MonoBehaviour
    {
        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            // Subscribe to the global inventory's consume event
            if (InventoryBootstrap.PlayerInventory != null)
            {
                InventoryBootstrap.PlayerInventory.OnItemConsumed += HandleItemConsumed;
            }
            else
            {
                Debug.LogWarning("[ConsumableAudioListener] PlayerInventory is null. Ensure InventoryBootstrap runs first.");
            }
        }

        private void OnDestroy()
        {
            // Always unsubscribe to prevent memory leaks
            if (InventoryBootstrap.PlayerInventory != null)
            {
                InventoryBootstrap.PlayerInventory.OnItemConsumed -= HandleItemConsumed;
            }
        }

        private void HandleItemConsumed(ItemInstance consumedItem)
        {
            if (consumedItem == null || consumedItem.Data == null) return;

            AudioClip soundToPlay = consumedItem.Data.consumeSound;
            
            if (soundToPlay != null)
            {
                // PlayOneShot allows multiple sounds to overlap without cutting each other off
                _audioSource.PlayOneShot(soundToPlay);
            }
        }
    }
}