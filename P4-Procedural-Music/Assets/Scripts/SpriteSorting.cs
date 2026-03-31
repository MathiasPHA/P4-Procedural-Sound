using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sorts a sprite (or a SortingGroup) by the bottom of its world-space bounds along Y so that lower
/// (visually closer to the bottom of the screen) renders in front. Works best when all world-dynamic
/// objects share a common sorting layer.
/// Optionally unifies the sorting layer at runtime so different categories (e.g., Items, Characters)
/// still inter-sort by Y.
/// </summary>
[DisallowMultipleComponent]
public class YSortable : MonoBehaviour
{
    public enum YReferenceMode
    {
        BoundsBottom,   // Use SpriteRenderer(s) world bounds min.y
        ColliderBottom, // Use assigned Collider2D bounds min.y
        CustomTransform // Use a custom transform's world Y (e.g., a feet bone/marker)
    }

    [Header("Sorting Target")]
    [Tooltip("If present, this takes precedence for multi-sprite characters.")]
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Ordering Settings")]
    [Tooltip("Pixels (orders) per world unit along Y. Higher = finer sorting granularity.")]
    [SerializeField] private int ordersPerUnit = 100;
    [Tooltip("Static offset applied to the computed order. Useful to bias certain categories up/down.")]
    [SerializeField] private int staticOrderOffset = 0;
    [Tooltip("Small random jitter to avoid flicker when two objects share the same Y.")]
    [SerializeField] private float randomOffsetRange = 0.01f;

    [Header("Reference Point")]
    [Tooltip("How to determine the Y used for sorting. BoundsBottom works for most sprites. ColliderBottom is ideal if you have a dedicated feet collider. CustomTransform lets you point to a feet marker.")]
    [SerializeField] private YReferenceMode referenceMode = YReferenceMode.BoundsBottom;
    [Tooltip("Optional explicit collider used when reference mode is ColliderBottom.")]
    [SerializeField] private Collider2D feetCollider;
    [Tooltip("Optional explicit transform used when reference mode is CustomTransform (e.g., a Feet bone).")]
    [SerializeField] private Transform customPivot;
    [Tooltip("Additional world-space Y offset applied after computing the reference (positive pushes the pivot up).")]
    [SerializeField] private float additionalYOffset = 0f;

    [Header("Layer Unification (optional)")]
    [Tooltip("If true, move the renderer/sorting group to a common layer so items and characters can inter-sort by Y.")]
    [SerializeField] private bool unifySortingLayer = true;
    [Tooltip("Sorting Layer name used when unifying. Create e.g. 'World' in Tags & Layers → Sorting Layers.")]
    [SerializeField] private string unifiedSortingLayerName = "World";

    private float jitter;
    private SpriteRenderer[] groupRenderers;
    private Collider2D[] groupColliders;

    private void Awake()
    {
        // Auto-find components if not wired
        if (sortingGroup == null) sortingGroup = GetComponent<SortingGroup>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (sortingGroup == null && spriteRenderer == null)
        {
            // Fallback: try first child SpriteRenderer (useful when attached to root with child visuals)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        jitter = (randomOffsetRange > 0f) ? Random.Range(-randomOffsetRange, randomOffsetRange) : 0f;

        BuildRendererCache();
        BuildColliderCache();
    }

    private void OnEnable()
    {
        if (unifySortingLayer)
        {
            TryUnifySortingLayer();
        }
        // Do an immediate update so initial frame is correct
        UpdateSortingOrder();
    }

    private void LateUpdate()
    {
        UpdateSortingOrder();
    }

    private void UpdateSortingOrder()
    {
        var y = GetReferenceYWorld() + jitter + additionalYOffset;
        int order = staticOrderOffset + Mathf.RoundToInt(-y * ordersPerUnit);

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = order;
        }
    }

    private float GetReferenceYWorld()
    {
        switch (referenceMode)
        {
            case YReferenceMode.ColliderBottom:
                {
                    float y;
                    if (TryGetColliderBottom(out y))
                        return y;
                    // Fallback to bounds bottom if collider missing
                    break;
                }
            case YReferenceMode.CustomTransform:
                if (customPivot != null)
                    return customPivot.position.y;
                // Fallback to bounds bottom
                break;
        }

        // Default and fallback: bottom of sprite bounds (group-aware)
        if (sortingGroup != null)
        {
            if (groupRenderers == null || groupRenderers.Length == 0)
            {
                BuildRendererCache();
            }

            float bottom = float.PositiveInfinity;
            if (groupRenderers != null)
            {
                for (int i = 0; i < groupRenderers.Length; i++)
                {
                    var sr = groupRenderers[i];
                    if (sr == null || sr.sprite == null) continue;
                    var b = sr.bounds;
                    if (b.size.sqrMagnitude <= 0f) continue;
                    if (b.min.y < bottom) bottom = b.min.y;
                }
            }
            if (bottom < float.PositiveInfinity)
                return bottom;
        }

        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return spriteRenderer.bounds.min.y;

        return transform.position.y;
    }

    private void BuildRendererCache()
    {
        if (sortingGroup != null)
        {
            groupRenderers = sortingGroup.GetComponentsInChildren<SpriteRenderer>(true);
        }
        else
        {
            groupRenderers = null;
        }
    }

    private void BuildColliderCache()
    {
        if (sortingGroup != null)
        {
            groupColliders = sortingGroup.GetComponentsInChildren<Collider2D>(true);
        }
        else
        {
            groupColliders = null;
        }
    }

    private bool TryGetColliderBottom(out float bottomY)
    {
        // Explicit collider takes precedence
        if (feetCollider != null)
        {
            bottomY = feetCollider.bounds.min.y;
            return true;
        }

        // Try colliders on this object
        var localCol = GetComponent<Collider2D>();
        if (localCol != null)
        {
            bottomY = localCol.bounds.min.y;
            return true;
        }

        // Try children within the sorting group
        if (sortingGroup != null)
        {
            if (groupColliders == null || groupColliders.Length == 0)
                BuildColliderCache();
            float b = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < groupColliders.Length; i++)
            {
                var c = groupColliders[i];
                if (c == null) continue;
                var bounds = c.bounds;
                if (bounds.size.sqrMagnitude <= 0f) continue;
                if (bounds.min.y < b)
                {
                    b = bounds.min.y;
                    found = true;
                }
            }
            if (found)
            {
                bottomY = b;
                return true;
            }
        }

        bottomY = 0f;
        return false;
    }

    private void OnTransformChildrenChanged()
    {
        if (sortingGroup != null)
        {
            BuildRendererCache();
            BuildColliderCache();
        }
    }

    private void TryUnifySortingLayer()
    {
        if (string.IsNullOrEmpty(unifiedSortingLayerName)) return;
        int layerId = SortingLayer.NameToID(unifiedSortingLayerName);
        if (layerId == 0 && unifiedSortingLayerName != SortingLayer.layers[0].name)
        {
            // Layer likely doesn't exist; skip unification silently
            return;
        }

        if (sortingGroup != null)
        {
            sortingGroup.sortingLayerID = layerId;
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerID = layerId;
        }
    }

    // Exposed helpers
    public void SetStaticOrderOffset(int offset)
    {
        staticOrderOffset = offset;
        UpdateSortingOrder();
    }

    public void SetOrdersPerUnit(int value)
    {
        ordersPerUnit = Mathf.Max(1, value);
        UpdateSortingOrder();
    }
}

