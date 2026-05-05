using UnityEngine;
using InventorySystem.Data;
using ProceduralTerrain;

namespace InteractionSystem
{
    /// <summary>
    /// Interactable that spawns world item drops when harvested (e.g. berry bushes).
    /// After harvesting, optionally swaps to a depleted sprite or prefab.
    ///
    /// SETUP:
    ///   1. Attach to the bush prefab
    ///   2. Set actionVerb to "Harvest" or "Pick"
    ///   3. Assign dropItem, dropAmount, and worldItemPrefab
    ///   4. Optionally assign a depleted sprite or prefab
    /// </summary>
    public class BushInteractable : Interactable
    {
        [Header("Drops")]
        [SerializeField] private ItemData dropItem;
        [Min(1)][SerializeField] private int dropAmount = 3;
        [SerializeField] private GameObject worldItemPrefab;

        [Header("After Harvest")]
        [Tooltip("If set, swaps the sprite instead of destroying the object.")]
        [SerializeField] private Sprite depletedSprite;
        [Tooltip("If set, spawns this prefab and destroys the original.")]
        [SerializeField] private GameObject depletedPrefab;
        [Tooltip("If true, the bush is destroyed after harvest (when no depleted visual is set).")]
        [SerializeField] private bool destroyOnHarvest = false;

        [Header("Audio")]
        [SerializeField] private AudioClip harvestSound;
        [Range(0f, 1f)][SerializeField] private float volume = 0.5f;
        [Range(0f, 0.5f)][SerializeField] private float pitchVariation = 0.15f;

        private bool _harvested;

        public override bool CanInteract() => !_harvested;

        public override void Interact(PlayerStateManager player)
        {
            if (_harvested) return;
            _harvested = true;

            // Face left or right toward the bush
            float xDiff = transform.position.x - player.transform.position.x;
            player.playerDir = xDiff < 0 ? "Left" : "Right";

            // Play pickup animation
            player.animationQue = "PickUp";
            player.StartHarvest();

            // Spawn world drops
            if (dropItem != null && worldItemPrefab != null)
            {
                Vector2 scatterDir = (transform.position - player.transform.position).normalized;
                for (int i = 0; i < dropAmount; i++)
                {
                    var instance = new ItemInstance(dropItem);
                    var go = Instantiate(worldItemPrefab, transform.position, Quaternion.identity);
                    var worldItem = go.GetComponent<InventorySystem.UI.WorldItem>();
                    Vector2 dir = scatterDir + Random.insideUnitCircle * 0.8f;
                    worldItem?.Initialise(instance, 1, dir);
                }
            }

            // Audio
            if (harvestSound != null)
            {
                var go = new GameObject("BushHarvestSound");
                go.transform.position = transform.position;
                var source = go.AddComponent<AudioSource>();
                source.clip = harvestSound;
                source.volume = volume;
                source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                source.spatialBlend = 0f;
                source.Play();
                Destroy(go, harvestSound.length / Mathf.Max(source.pitch, 0.1f));
            }

            // Handle depleted visuals
            if (depletedSprite != null)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = depletedSprite;
            }
            else if (depletedPrefab != null)
            {
                Instantiate(depletedPrefab, transform.position, transform.rotation, transform.parent);
                NotifyChunkAndDestroy();
            }
            else if (destroyOnHarvest)
            {
                NotifyChunkAndDestroy();
            }
        }

        private void NotifyChunkAndDestroy()
        {
            var tracker = GetComponent<SpawnedObjectTracker>();
            if (tracker != null)
                tracker.Remove();
            else
                Destroy(gameObject);
        }
    }
}