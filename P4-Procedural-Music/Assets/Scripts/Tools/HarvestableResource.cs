using System;
using UnityEngine;
using InventorySystem.Data;

namespace InventorySystem.Harvesting
{
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
        [Min(1)][SerializeField] private int dropAmount = 3;

        [Tooltip("If true, items go straight into inventory instead of spawning as world drops.")]
        [SerializeField] private bool directToInventory = false;

        [Header("World Drop")]
        [Tooltip("Prefab with WorldItem, SpriteRenderer, Collider2D, Rigidbody2D. " +
                 "Not needed if directToInventory is true.")]
        [SerializeField] private GameObject worldItemPrefab;

        [Header("Audio")]
        [Tooltip("Sound played on each hit (e.g. axe chop or pickaxe strike).")]
        [SerializeField] private AudioClip hitSound;
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 0.5f;

        [Tooltip("Sound played when the resource is fully depleted (e.g. tree falling).")]
        [SerializeField] private AudioClip depleteSound;
        [Range(0f, 1f)]
        [SerializeField] private float depleteVolume = 0.5f;

        [Tooltip("Sound played when harvesting with directToInventory.")]
        [SerializeField] private AudioClip pickupSound;
        [Range(0f, 1f)]
        [SerializeField] private float pickupVolume = 0.5f;
        [Tooltip("How much the pitch varies randomly each pickup (e.g. 0.15 = ±15%).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float pitchVariation = 0.15f;

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

        public ToolType RequiredToolType => requiredToolType;
        public bool IsDepleted => _currentHealth <= 0;

        public event Action<int, int> OnHit;
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

        public bool TakeDamage(int damage, Vector2 hitDirection)
        {
            if (IsDepleted) return false;

            _currentHealth -= damage;
            _currentHealth = Mathf.Max(0, _currentHealth);

            _shakeTimer = shakeDuration;
            _originalPosition = transform.position;

            OnHit?.Invoke(_currentHealth, maxHealth);

            if (hitSound != null)
                PlaySoundWithPitch(hitSound, transform.position, hitVolume);

            if (_currentHealth <= 0)
            {
                Deplete(hitDirection);
                return true;
            }

            return false;
        }

        private void Deplete(Vector2 hitDirection)
        {
            if (dropItem != null)
            {
                if (directToInventory)
                {
                    var inventory = InventoryBootstrap.PlayerInventory;
                    if (inventory != null)
                    {
                        int overflow = inventory.AddItem(dropItem, dropAmount);
                        if (overflow > 0)
                            Debug.LogWarning($"[Harvestable] {overflow}x {dropItem.displayName} didn't fit.");
                    }

                    if (pickupSound != null)
                        PlaySoundWithPitch(pickupSound, transform.position, pickupVolume);
                }
                else if (worldItemPrefab != null)
                {
                    SpawnDrops(dropAmount, hitDirection);
                }
            }

            if (depletedPrefab != null)
            {
                var sr = GetComponent<SpriteRenderer>();
                Vector3 spawnPos = transform.position;

                if (sr != null)
                    spawnPos.y = sr.bounds.min.y;

                Instantiate(depletedPrefab, spawnPos, transform.rotation, transform.parent);
            }

            OnDepleted?.Invoke();

            if (depleteSound != null)
                PlaySoundWithPitch(depleteSound, transform.position, depleteVolume);

            Destroy(gameObject);
        }

        private void SpawnDrops(int amount, Vector2 hitDirection)
        {
            for (int i = 0; i < amount; i++)
            {
                var instance = new ItemInstance(dropItem);
                var go = Instantiate(worldItemPrefab, transform.position, Quaternion.identity);
                var worldItem = go.GetComponent<UI.WorldItem>();

                Vector2 scatterDir = -hitDirection.normalized + UnityEngine.Random.insideUnitCircle * 0.8f;
                worldItem?.Initialise(instance, 1, scatterDir);
            }
        }

        private void PlaySoundWithPitch(AudioClip clip, Vector3 position, float volume = -1f)
        {
            if (volume < 0f) volume = pickupVolume;

            var go = new GameObject("PickupSound");
            go.transform.position = position;

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            source.spatialBlend = 0f;
            source.Play();

            Destroy(go, clip.length / Mathf.Max(source.pitch, 0.1f));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1f,
                $"HP: {(Application.isPlaying ? _currentHealth : maxHealth)}/{maxHealth}");
        }
#endif
    }
}