using UnityEngine;

namespace MobSystem
{
    /// <summary>
    /// Small health bar that appears above a mob when it takes damage,
    /// then fades out after a timeout. Fully self-contained — creates its
    /// own sprites at runtime from a 1x1 white texture.
    ///
    /// Auto-scales to the mob's sprite size. A tiny rabbit and a huge boss
    /// both get a proportionally-sized bar with zero inspector tweaking.
    ///
    /// SETUP:
    ///   1. Add to the mob prefab (same GameObject as SpriteRenderer)
    ///   2. That's it — auto-wires via MobController.OnDamaged, auto-scales via SpriteRenderer.bounds
    /// </summary>
    public class MobHealthBar : MonoBehaviour
    {
        [Header("Bar Size (relative to sprite)")]
        [Tooltip("Bar width as a fraction of the sprite's width. " +
                 "0.8 = 80% of sprite width. Works on any sprite size.")]
        [Range(0.3f, 1.2f)]
        [SerializeField] private float widthRatio = 0.8f;

        [Tooltip("Bar height as a fraction of the sprite's width. " +
                 "Tied to width (not height) so the bar keeps consistent proportions.")]
        [Range(0.01f, 0.1f)]
        [SerializeField] private float heightRatio = 0.035f;

        [Header("Position")]
        [Tooltip("Extra padding above the sprite's top edge (world units).")]
        [SerializeField] private float verticalPadding = 0.15f;

        [Header("Timing")]
        [Tooltip("Seconds the bar stays visible after the last hit.")]
        [SerializeField] private float showDuration = 3f;

        [Tooltip("How fast the bar fades out (seconds).")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Colours")]
        [SerializeField] private Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        [SerializeField] private Color healthyColor = new Color(0.3f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color hurtColor = new Color(0.9f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color criticalColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        [Header("Sorting")]
        [Tooltip("Sorting order for the bar (should be above mob sprites).")]
        [SerializeField] private int sortingOrder = 100;

        // ───────────────────────── Runtime ─────────────────────────

        private MobController _mob;
        private SpriteRenderer _bgRenderer;
        private SpriteRenderer _fillRenderer;
        private Transform _barRoot;
        private float _showTimer;
        private float _fadeTimer;
        private float _displayedRatio = 1f;
        private float _targetRatio = 1f;
        private bool _isVisible;
        private float _barYPosition;
        private float _barWidth;
        private float _barHeight;

        // Shared across all instances
        private static Sprite _sharedPixelSprite;

        private void Start()
        {
            _mob = GetComponent<MobController>();
            if (_mob == null)
                _mob = GetComponentInParent<MobController>();

            if (_mob == null)
            {
                Debug.LogWarning($"[MobHealthBar] No MobController found on {gameObject.name}!");
                enabled = false;
                return;
            }

            // Calculate actual bar dimensions from sprite bounds
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float spriteWidth = sr.bounds.size.x;
                float spriteHeight = sr.bounds.size.y;
                _barWidth = spriteWidth * widthRatio;
                _barHeight = spriteWidth * heightRatio;
                _barYPosition = (spriteHeight * 0.5f) + verticalPadding;
            }
            else
            {
                // Fallback for mobs without a SpriteRenderer on the root
                _barWidth = 1f;
                _barHeight = 0.08f;
                _barYPosition = 0.5f + verticalPadding;
            }

            CreateBar();

            _mob.OnDamaged += OnMobDamaged;

            // Start hidden
            SetBarVisible(false);
        }

        private void OnDestroy()
        {
            if (_mob != null)
                _mob.OnDamaged -= OnMobDamaged;
        }

        private void LateUpdate()
        {
            if (_barRoot == null) return;

            // Keep bar above mob, not rotated, not flipped
            _barRoot.position = transform.position + Vector3.up * _barYPosition;
            _barRoot.rotation = Quaternion.identity;

            // Smooth the fill bar toward target
            _displayedRatio = Mathf.MoveTowards(_displayedRatio, _targetRatio, Time.deltaTime * 3f);
            UpdateFill();

            // Visibility timer
            if (_isVisible)
            {
                _showTimer -= Time.deltaTime;

                if (_showTimer <= 0f)
                {
                    _fadeTimer -= Time.deltaTime;
                    float fadeAlpha = Mathf.Clamp01(_fadeTimer / fadeDuration);

                    SetBarAlpha(fadeAlpha);

                    if (_fadeTimer <= 0f)
                        SetBarVisible(false);
                }
            }
        }

        // ───────────────────────── Bar Creation ─────────────────────────

        private void CreateBar()
        {
            EnsurePixelSprite();

            // Root container
            _barRoot = new GameObject("HealthBar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = Vector3.up * _barYPosition;

            // Background
            var bgObj = new GameObject("HealthBar_BG");
            bgObj.transform.SetParent(_barRoot, false);
            _bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            _bgRenderer.sprite = _sharedPixelSprite;
            _bgRenderer.color = backgroundColor;
            _bgRenderer.sortingOrder = sortingOrder;
            bgObj.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);

            // Fill
            var fillObj = new GameObject("HealthBar_Fill");
            fillObj.transform.SetParent(_barRoot, false);
            _fillRenderer = fillObj.AddComponent<SpriteRenderer>();
            _fillRenderer.sprite = _sharedPixelSprite;
            _fillRenderer.color = healthyColor;
            _fillRenderer.sortingOrder = sortingOrder + 1;
            fillObj.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);
        }

        private static void EnsurePixelSprite()
        {
            if (_sharedPixelSprite != null) return;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            _sharedPixelSprite = Sprite.Create(
                tex,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f
            );
        }

        // ───────────────────────── Updates ─────────────────────────

        private void UpdateFill()
        {
            if (_fillRenderer == null) return;

            // Scale X to represent health ratio
            float fillWidth = _barWidth * _displayedRatio;
            _fillRenderer.transform.localScale = new Vector3(fillWidth, _barHeight, 1f);

            // Offset so the bar shrinks from right to left
            float offset = (fillWidth - _barWidth) * 0.5f;
            _fillRenderer.transform.localPosition = new Vector3(offset, 0f, 0f);

            // Colour based on health ratio
            Color fillColor;
            if (_displayedRatio > 0.5f)
                fillColor = Color.Lerp(hurtColor, healthyColor, (_displayedRatio - 0.5f) * 2f);
            else
                fillColor = Color.Lerp(criticalColor, hurtColor, _displayedRatio * 2f);

            fillColor.a = _fillRenderer.color.a;
            _fillRenderer.color = fillColor;
        }

        private void SetBarVisible(bool visible)
        {
            _isVisible = visible;

            if (_bgRenderer != null) _bgRenderer.enabled = visible;
            if (_fillRenderer != null) _fillRenderer.enabled = visible;
        }

        private void SetBarAlpha(float alpha)
        {
            if (_bgRenderer != null)
            {
                var c = _bgRenderer.color;
                c.a = backgroundColor.a * alpha;
                _bgRenderer.color = c;
            }

            if (_fillRenderer != null)
            {
                var c = _fillRenderer.color;
                c.a = alpha;
                _fillRenderer.color = c;
            }
        }

        // ───────────────────────── Event Handler ─────────────────────────

        private void OnMobDamaged(int currentHealth, int maxHealth, Vector3 attackerPosition)
        {
            _targetRatio = Mathf.Clamp01((float)currentHealth / maxHealth);

            SetBarVisible(true);
            SetBarAlpha(1f);
            _showTimer = showDuration;
            _fadeTimer = fadeDuration;
        }
    }
}