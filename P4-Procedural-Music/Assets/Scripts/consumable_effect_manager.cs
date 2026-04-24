using InventorySystem.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace InventorySystem
{
    /// <summary>
    /// Manages and applies consumable effects to the player.
    /// Attach this to your Player GameObject.
    /// Hooks into HappinessSystem, HungerSystem, and PlayerStateManager.
    /// 
    /// Automatically subscribes to Inventory.OnItemConsumed to apply
    /// custom effects when items are consumed.
    /// </summary>
    public class ConsumableEffectManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer playerSprite;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Light2D playerLight;

        [Header("System References (Auto-found if not assigned)")]
        [SerializeField] private HappinessSystem happinessSystem;
        [SerializeField] private HungerSystem hungerSystem;
        [SerializeField] private PlayerStateManager playerStateManager;

        [Header("Debug")]
        [SerializeField] private bool logEffects = true;

        // Track original values
        private Vector3 originalScale;
        private float originalMoveSpeed;

        // Active tint tracking - survives animation sprite changes
        private Color activeTint = Color.white;
        private bool tintActive = false;
        private MaterialPropertyBlock propBlock;

        // Control inversion flag
        [HideInInspector] public bool invertControls = false;

        void Start()
        {
            // Auto-find components if not assigned
            if (playerSprite == null)
                playerSprite = GetComponentInChildren<SpriteRenderer>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (happinessSystem == null)
                happinessSystem = HappinessSystem.Instance ?? FindObjectOfType<HappinessSystem>();

            if (hungerSystem == null)
                hungerSystem = HungerSystem.Instance ?? FindObjectOfType<HungerSystem>();

            if (playerStateManager == null)
                playerStateManager = GetComponent<PlayerStateManager>();

            if (playerLight == null)
                playerLight = GetComponentInChildren<Light2D>();

            originalScale = transform.localScale;

            if (playerStateManager != null)
                originalMoveSpeed = playerStateManager.moveSpeed;

            // Initialize MaterialPropertyBlock for tinting
            propBlock = new MaterialPropertyBlock();

            // Subscribe to inventory's OnItemConsumed event
            SubscribeToInventory();
        }

        void LateUpdate()
        {
            // Continuously re-apply the tint using MaterialPropertyBlock
            // This survives sprite/animation changes because it runs after the Animator
            if (tintActive && playerSprite != null)
            {
                playerSprite.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", activeTint);
                playerSprite.SetPropertyBlock(propBlock);
            }
        }

        void OnDestroy()
        {
            UnsubscribeFromInventory();
        }

        private void SubscribeToInventory()
        {
            var inventory = InventoryBootstrap.PlayerInventory;
            if (inventory != null)
            {
                inventory.OnItemConsumed += OnItemConsumed;
                if (logEffects)
                    Debug.Log("[ConsumableEffectManager] Subscribed to OnItemConsumed");
            }
            else
            {
                Debug.LogWarning("[ConsumableEffectManager] No PlayerInventory found - " +
                                 "custom effects won't trigger on consumption!");
            }
        }

        private void UnsubscribeFromInventory()
        {
            var inventory = InventoryBootstrap.PlayerInventory;
            if (inventory != null)
            {
                inventory.OnItemConsumed -= OnItemConsumed;
            }
        }

        /// <summary>
        /// Called automatically when player consumes an item from inventory.
        /// </summary>
        private void OnItemConsumed(ItemInstance consumed)
        {
            if (consumed == null || consumed.Data == null) return;

            if (consumed.Data.HasCustomEffects)
            {
                if (logEffects)
                    Debug.Log($"[ConsumableEffectManager] Applying {consumed.Data.customEffects.Count} " +
                              $"custom effect(s) from {consumed.Data.displayName}");

                ApplyItemEffects(consumed.Data);
            }
        }

        /// <summary>
        /// Apply all effects from a consumed item (can also be called manually)
        /// </summary>
        public void ApplyItemEffects(ItemData item)
        {
            if (item == null || !item.HasCustomEffects)
                return;

            foreach (var effect in item.customEffects)
            {
                ApplyEffect(effect);
            }
        }

        /// <summary>
        /// Apply a single consumable effect
        /// </summary>
        public void ApplyEffect(ConsumableEffect effect)
        {
            if (effect.effectSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(effect.effectSound, effect.effectSoundVolume);
            }

            if (!string.IsNullOrEmpty(effect.effectMessage))
            {
                Debug.Log($"[Effect] {effect.effectMessage}");
            }

            switch (effect.effectType)
            {
                case EffectType.RestoreHunger:
                    ApplyInstantHunger(effect.magnitude);
                    break;

                case EffectType.RestoreHappiness:
                    ApplyInstantHappiness(effect.magnitude);
                    break;

                case EffectType.RestoreHealth:
                    ApplyInstantHealth(effect.magnitude);
                    break;

                case EffectType.DamageHealth:
                    ApplyInstantDamage(effect.magnitude);
                    break;

                case EffectType.Teleport:
                    ApplyTeleport(effect.magnitude);
                    break;

                case EffectType.ColorTint:
                    StartCoroutine(ApplyColorTintEffect(effect));
                    break;

                case EffectType.Poison:
                    StartCoroutine(ApplyPoisonEffect(effect));
                    break;

                case EffectType.SpeedBoost:
                    StartCoroutine(ApplySpeedEffect(effect));
                    break;

                case EffectType.Slow:
                    StartCoroutine(ApplySlowEffect(effect));
                    break;

                case EffectType.Glow:
                    StartCoroutine(ApplyGlowEffect(effect));
                    break;

                case EffectType.Invincibility:
                    StartCoroutine(ApplyInvincibilityEffect(effect));
                    break;

                case EffectType.Shrink:
                    StartCoroutine(ApplyScaleEffect(effect, true));
                    break;

                case EffectType.Grow:
                    StartCoroutine(ApplyScaleEffect(effect, false));
                    break;

                case EffectType.Invisibility:
                    StartCoroutine(ApplyInvisibilityEffect(effect));
                    break;

                case EffectType.Blindness:
                    StartCoroutine(ApplyBlindnessEffect(effect));
                    break;

                case EffectType.Confusion:
                    StartCoroutine(ApplyConfusionEffect(effect));
                    break;
            }
        }

        #region Instant Effects

        private void ApplyInstantHunger(float amount)
        {
            if (hungerSystem != null)
            {
                hungerSystem.RestoreHunger(amount);
                if (logEffects) Debug.Log($"Restored {amount * 100}% hunger");
            }
            else
            {
                Debug.LogWarning("HungerSystem not found!");
            }
        }

        private void ApplyInstantHappiness(float amount)
        {
            if (happinessSystem != null)
            {
                happinessSystem.AdjustHappiness(amount);
                if (logEffects) Debug.Log($"Adjusted happiness by {amount}");
            }
            else
            {
                Debug.LogWarning("HappinessSystem not found!");
            }
        }

        private void ApplyInstantHealth(float amount)
        {
            Debug.Log($"[TODO] Heal {amount} health");
        }

        private void ApplyInstantDamage(float amount)
        {
            if (happinessSystem != null)
            {
                happinessSystem.AdjustHappiness(-amount);
                if (logEffects) Debug.Log($"Took {amount} damage (reduced happiness)");
            }
            else
            {
                Debug.Log($"[TODO] Take {amount} damage");
            }
        }

        private void ApplyTeleport(float radius)
        {
            Vector2 randomOffset = Random.insideUnitCircle * radius;
            transform.position += new Vector3(randomOffset.x, randomOffset.y, 0);
            if (logEffects) Debug.Log("Teleported!");
        }

        #endregion

        #region Duration Effects

        private IEnumerator ApplyColorTintEffect(ConsumableEffect effect)
        {
            if (playerSprite == null) yield break;

            // Activate the tint - LateUpdate will re-apply it every frame
            activeTint = effect.tintColor;
            tintActive = true;

            yield return new WaitForSeconds(effect.duration);

            // Clear tint
            tintActive = false;
            if (playerSprite != null)
            {
                playerSprite.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", Color.white);
                playerSprite.SetPropertyBlock(propBlock);
            }
        }

        private IEnumerator ApplyPoisonEffect(ConsumableEffect effect)
        {
            float elapsed = 0f;
            float tickInterval = 1f;

            // Apply greenish tint
            activeTint = new Color(0.5f, 1f, 0.5f);
            tintActive = true;

            while (elapsed < effect.duration)
            {
                if (happinessSystem != null)
                {
                    happinessSystem.AdjustHappiness(-effect.magnitude);
                }

                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
            }

            // Clear tint
            tintActive = false;
            if (playerSprite != null)
            {
                playerSprite.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", Color.white);
                playerSprite.SetPropertyBlock(propBlock);
            }
        }

        private IEnumerator ApplySpeedEffect(ConsumableEffect effect)
        {
            if (playerStateManager == null) yield break;

            float originalSpeed = playerStateManager.moveSpeed;
            playerStateManager.moveSpeed = originalSpeed * effect.magnitude;

            yield return new WaitForSeconds(effect.duration);

            playerStateManager.moveSpeed = originalSpeed;
        }

        private IEnumerator ApplySlowEffect(ConsumableEffect effect)
        {
            if (playerStateManager == null) yield break;

            float originalSpeed = playerStateManager.moveSpeed;
            playerStateManager.moveSpeed = originalSpeed * effect.magnitude;

            // Apply tint if specified
            if (effect.tintColor != Color.white)
            {
                activeTint = effect.tintColor;
                tintActive = true;
            }

            yield return new WaitForSeconds(effect.duration);

            playerStateManager.moveSpeed = originalSpeed;

            tintActive = false;
            if (playerSprite != null)
            {
                playerSprite.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", Color.white);
                playerSprite.SetPropertyBlock(propBlock);
            }
        }

        private IEnumerator ApplyGlowEffect(ConsumableEffect effect)
        {
            if (playerLight == null)
            {
                Debug.LogWarning("No Light2D component found for glow effect!");
                yield break;
            }

            float originalIntensity = playerLight.intensity;
            float originalOuterRadius = playerLight.pointLightOuterRadius;
            Color originalColor = playerLight.color;
            bool wasEnabled = playerLight.enabled;

            playerLight.pointLightOuterRadius = effect.magnitude;
            playerLight.intensity = 1.5f;
            playerLight.color = effect.tintColor;
            playerLight.enabled = true;

            yield return new WaitForSeconds(effect.duration);

            playerLight.pointLightOuterRadius = originalOuterRadius;
            playerLight.intensity = originalIntensity;
            playerLight.color = originalColor;
            playerLight.enabled = wasEnabled;
        }

        private IEnumerator ApplyInvincibilityEffect(ConsumableEffect effect)
        {
            float flashInterval = 0.1f;
            float elapsed = 0f;

            while (elapsed < effect.duration)
            {
                activeTint = effect.tintColor;
                tintActive = true;
                yield return new WaitForSeconds(flashInterval);
                tintActive = false;
                if (playerSprite != null)
                {
                    playerSprite.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_Color", Color.white);
                    playerSprite.SetPropertyBlock(propBlock);
                }
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval * 2;
            }

            tintActive = false;
        }

        private IEnumerator ApplyScaleEffect(ConsumableEffect effect, bool shrink)
        {
            Vector3 targetScale = originalScale * effect.magnitude;
            transform.localScale = targetScale;

            yield return new WaitForSeconds(effect.duration);

            transform.localScale = originalScale;
        }

        private IEnumerator ApplyInvisibilityEffect(ConsumableEffect effect)
        {
            if (playerSprite == null) yield break;

            activeTint = new Color(1f, 1f, 1f, 0.2f);
            tintActive = true;

            yield return new WaitForSeconds(effect.duration);

            tintActive = false;
            if (playerSprite != null)
            {
                playerSprite.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", Color.white);
                playerSprite.SetPropertyBlock(propBlock);
            }
        }

        private IEnumerator ApplyBlindnessEffect(ConsumableEffect effect)
        {
            Debug.Log("[TODO] Blindness effect - darken screen");
            yield return new WaitForSeconds(effect.duration);
            Debug.Log("Vision restored");
        }

        private IEnumerator ApplyConfusionEffect(ConsumableEffect effect)
        {
            invertControls = true;
            yield return new WaitForSeconds(effect.duration);
            invertControls = false;
        }

        #endregion
    }
}