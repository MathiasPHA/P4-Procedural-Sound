using System.Collections;
using UnityEngine;

namespace InventorySystem.Building
{
    /// <summary>
    /// Renders a world-space health bar above a placed structure.
    ///
    /// Mirrors the MobHealthBar approach:
    ///   - Sized relative to the structure's SpriteRenderer bounds (no fixed pixel sizes).
    ///   - Runtime-generated texture — zero extra assets required.
    ///   - Hidden at full HP; appears on first hit, auto-hides after <see cref="hideDelay"/> seconds.
    ///
    /// PREFAB SETUP:
    ///   1. Add this component to your structure prefab root (same object as PlacedStructure).
    ///   2. That's it. Everything is created at runtime.
    ///   3. Optionally tweak the serialized fields below in the Inspector.
    ///
    /// The bar sits above the sprite, centred on the structure's X position.
    /// </summary>
    [RequireComponent(typeof(PlacedStructure))]
    public class StructureHealthBar : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Size (relative to sprite)")]
        [Tooltip("Bar width as a fraction of the sprite's world width. 1.0 = same width as sprite.")]
        [Range(0.2f, 2f)]
        [SerializeField] private float widthRatio  = 0.8f;

        [Tooltip("Bar height as a fraction of the sprite's world height.")]
        [Range(0.01f, 0.2f)]
        [SerializeField] private float heightRatio = 0.04f;

        [Tooltip("Extra world-space units above the sprite top edge.")]
        [SerializeField] private float yOffset = 0.15f;

        [Header("Colours")]
        [SerializeField] private Color fullColour     = new Color(0.15f, 0.85f, 0.15f); // green
        [SerializeField] private Color lowColour      = new Color(0.85f, 0.15f, 0.15f); // red
        [SerializeField] private Color backgroundColour = new Color(0.1f, 0.1f, 0.1f, 0.75f);
        [SerializeField] private Color borderColour   = new Color(0f, 0f, 0f, 0.9f);

        [Header("Behaviour")]
        [Tooltip("Seconds after the last hit before the bar fades out.")]
        [SerializeField] private float hideDelay = 2.5f;

        [Tooltip("Fade-out duration in seconds.")]
        [SerializeField] private float fadeDuration = 0.4f;

        [Tooltip("Sorting order of the bar sprite (should be above the structure sprite).")]
        [SerializeField] private int sortingOrder = 10;

        [Tooltip("Sorting layer name for the bar. Leave empty to inherit from the structure's SpriteRenderer.")]
        [SerializeField] private string sortingLayerName = "";

        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────

        private PlacedStructure _structure;
        private SpriteRenderer  _barRenderer;
        private Texture2D       _barTexture;

        private const int TexW = 64;
        private const int TexH = 8;

        private float _fillAmount = 1f;   // 0–1
        private Coroutine _hideRoutine;
        private bool _visible;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _structure = GetComponent<PlacedStructure>();
        }

        private void Start()
        {
            // PlacedStructure.sourceData is assigned by PlacementSystem / PlacedStructureManager
            // before Start, so the sprite is ready to read here.
            BuildBarRenderer();
            SetVisible(false);

            _structure.OnHit       += HandleHit;
            _structure.OnDestroyed += HandleDestroyed;
        }

        private void OnDestroy()
        {
            if (_structure != null)
            {
                _structure.OnHit       -= HandleHit;
                _structure.OnDestroyed -= HandleDestroyed;
            }

            if (_barTexture != null)
                Destroy(_barTexture);

            if (_barRenderer != null)
                Destroy(_barRenderer.gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Event handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleHit(int currentHealth, int maxHealth)
        {
            _fillAmount = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            RedrawTexture();
            ShowBar();
        }

        private void HandleDestroyed()
        {
            // Structure is about to be destroyed — hide bar immediately.
            SetVisible(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Bar construction
        // ─────────────────────────────────────────────────────────────────────

        private void BuildBarRenderer()
        {
            // Create a dedicated child GameObject so the bar can be toggled
            // independently and doesn't interfere with the structure's own renderer.
            var go = new GameObject("StructureHealthBar");
            go.transform.SetParent(transform, worldPositionStays: false);

            _barRenderer = go.AddComponent<SpriteRenderer>();

            // Resolve sorting layer
            var structSr = GetComponentInChildren<SpriteRenderer>();
            string layerName = string.IsNullOrEmpty(sortingLayerName) && structSr != null
                ? structSr.sortingLayerName
                : sortingLayerName;

            if (!string.IsNullOrEmpty(layerName))
                _barRenderer.sortingLayerName = layerName;

            _barRenderer.sortingOrder = sortingOrder;

            // Position above the sprite
            PositionBar(structSr);

            // Create texture and sprite
            _barTexture = new Texture2D(TexW, TexH, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp
            };

            RedrawTexture();

            // Sprite pixel-per-unit = TexW so it displays at exactly the bar's world width
            float spriteWidth  = GetSpriteWorldWidth(structSr);
            float targetWidth  = spriteWidth * widthRatio;
            float targetHeight = GetSpriteWorldHeight(structSr) * heightRatio;

            // PPU chosen so that TexW pixels == targetWidth world units
            float ppu = TexW / targetWidth;

            var sprite = Sprite.Create(
                _barTexture,
                new Rect(0, 0, TexW, TexH),
                new Vector2(0.5f, 0.5f),
                ppu
            );

            _barRenderer.sprite = sprite;

            // Scale height independently: sprite height in world = TexH / ppu,
            // but we want targetHeight — adjust via local Y scale.
            float naturalHeight = TexH / ppu;
            float yScale        = naturalHeight > 0f ? targetHeight / naturalHeight : 1f;
            go.transform.localScale = new Vector3(1f, yScale, 1f);
        }

        private void PositionBar(SpriteRenderer structSr)
        {
            float topEdge = structSr != null
                ? structSr.bounds.max.y - transform.position.y
                : 0.5f;

            // Position is relative to parent (the structure root)
            _barRenderer.transform.localPosition = new Vector3(0f, topEdge + yOffset, 0f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Texture drawing
        // ─────────────────────────────────────────────────────────────────────

        private void RedrawTexture()
        {
            if (_barTexture == null) return;

            Color fillColour = Color.Lerp(lowColour, fullColour, _fillAmount);
            int fillPixels   = Mathf.RoundToInt(_fillAmount * (TexW - 2)); // inside border

            for (int x = 0; x < TexW; x++)
            {
                for (int y = 0; y < TexH; y++)
                {
                    Color c;

                    // 1-pixel border
                    if (x == 0 || x == TexW - 1 || y == 0 || y == TexH - 1)
                    {
                        c = borderColour;
                    }
                    // Interior
                    else
                    {
                        // x=1 is the first interior pixel
                        c = (x - 1) < fillPixels ? fillColour : backgroundColour;
                    }

                    _barTexture.SetPixel(x, y, c);
                }
            }

            _barTexture.Apply();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Visibility helpers
        // ─────────────────────────────────────────────────────────────────────

        private void ShowBar()
        {
            // Cancel any pending hide
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            SetVisible(true);

            // Start the auto-hide countdown
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private void SetVisible(bool show)
        {
            _visible = show;
            if (_barRenderer != null)
                _barRenderer.enabled = show;
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(hideDelay);

            // Fade out
            float elapsed = 0f;
            Color baseColour = _barRenderer.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                _barRenderer.color = new Color(baseColour.r, baseColour.g, baseColour.b, alpha);
                yield return null;
            }

            SetVisible(false);
            _barRenderer.color = new Color(baseColour.r, baseColour.g, baseColour.b, 1f);
            _hideRoutine = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Size helpers
        // ─────────────────────────────────────────────────────────────────────

        private static float GetSpriteWorldWidth(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return 1f;
            return sr.bounds.size.x;
        }

        private static float GetSpriteWorldHeight(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return 1f;
            return sr.bounds.size.y;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            if (_barRenderer == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_barRenderer.transform.position, _barRenderer.bounds.size);
        }
#endif
    }
}
