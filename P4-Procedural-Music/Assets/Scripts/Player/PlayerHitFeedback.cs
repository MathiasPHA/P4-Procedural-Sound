using UnityEngine;

namespace MobSystem
{
    /// <summary>
    /// Visual feedback when the player takes damage: red sprite tint.
    /// PlayerHealth calls TriggerHit() when a mob attack lands.
    ///
    /// SETUP:
    ///   1. Add to the Player GameObject (same object as PlayerHealth)
    ///   2. Assign the player's SpriteRenderer (auto-finds in children if empty)
    ///   3. Tune colour and duration in the inspector
    /// </summary>
    public class PlayerHitFeedback : MonoBehaviour
    {
        public static PlayerHitFeedback Instance { get; private set; }

        [Header("Flash")]
        [Tooltip("Colour the sprite flashes to on hit.")]
        [SerializeField] private Color hitColor = new Color(1f, 0.3f, 0.3f, 1f);

        [Tooltip("How long the tint lasts (seconds).")]
        [SerializeField] private float flashDuration = 0.2f;

        [Header("References")]
        [Tooltip("The player's SpriteRenderer. Auto-finds in children if left empty.")]
        [SerializeField] private SpriteRenderer playerSprite;

        // Runtime
        private Color _originalColor;
        private float _flashTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (playerSprite == null)
                playerSprite = GetComponentInChildren<SpriteRenderer>();

            if (playerSprite == null)
            {
                Debug.LogWarning("[PlayerHitFeedback] No SpriteRenderer found on player!");
                enabled = false;
                return;
            }

            _originalColor = playerSprite.color;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float t = _flashTimer / flashDuration;
                playerSprite.color = Color.Lerp(_originalColor, hitColor, t);

                if (_flashTimer <= 0f)
                    playerSprite.color = _originalColor;
            }
        }

        /// <summary>
        /// Trigger the red flash. Called by PlayerHealth.TakeDamage().
        /// </summary>
        public void TriggerHit()
        {
            if (playerSprite == null) return;

            playerSprite.color = hitColor;
            _flashTimer = flashDuration;
        }
    }
}
