using UnityEngine;

namespace MobSystem
{
    /// <summary>
    /// Visual feedback when a mob takes damage: white flash + position shake.
    /// Mirrors the HarvestableResource shake pattern and adds a sprite colour flash.
    ///
    /// Automatically hooks into MobController.OnDamaged — no manual wiring needed.
    ///
    /// SETUP:
    ///   1. Add to the mob prefab (same GameObject as SpriteRenderer)
    ///   2. Tune shake/flash values in the inspector
    ///   3. That's it — it self-wires via MobController.OnDamaged
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MobHitFeedback : MonoBehaviour
    {
        [Header("Shake")]
        [Tooltip("How far the sprite shakes on hit (world units).")]
        [SerializeField] private float shakeIntensity = 0.08f;

        [Tooltip("How long the shake lasts (seconds).")]
        [SerializeField] private float shakeDuration = 0.15f;

        [Header("Flash")]
        [Tooltip("Colour the sprite flashes to on hit. White = full brightness flash.")]
        [SerializeField] private Color flashColor = Color.white;

        [Tooltip("How long the flash lasts (seconds).")]
        [SerializeField] private float flashDuration = 0.1f;

        // Runtime
        private SpriteRenderer _sr;
        private MobController _mob;
        private Color _originalColor;
        private Vector3 _originalPosition;
        private float _shakeTimer;
        private float _flashTimer;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _originalColor = _sr.color;
        }

        private void Start()
        {
            _mob = GetComponent<MobController>();
            if (_mob == null)
                _mob = GetComponentInParent<MobController>();

            if (_mob != null)
                _mob.OnDamaged += OnMobDamaged;
        }

        private void OnDestroy()
        {
            if (_mob != null)
                _mob.OnDamaged -= OnMobDamaged;
        }

        private void Update()
        {
            // ── Shake ──
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                float t = _shakeTimer / shakeDuration;
                Vector2 offset = Random.insideUnitCircle * shakeIntensity * t;
                transform.position = _originalPosition + (Vector3)offset;

                if (_shakeTimer <= 0f)
                    transform.position = _originalPosition;
            }

            // ── Flash ──
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float t = _flashTimer / flashDuration;
                _sr.color = Color.Lerp(_originalColor, flashColor, t);

                if (_flashTimer <= 0f)
                    _sr.color = _originalColor;
            }
        }

        private void OnMobDamaged(int currentHealth, int maxHealth, Vector3 attackerPosition)
        {
            TriggerFeedback();
        }

        /// <summary>
        /// Trigger the flash + shake manually (e.g. from other damage sources).
        /// </summary>
        public void TriggerFeedback()
        {
            // Start shake
            _originalPosition = transform.position;
            _shakeTimer = shakeDuration;

            // Start flash
            _sr.color = flashColor;
            _flashTimer = flashDuration;
        }
    }
}
