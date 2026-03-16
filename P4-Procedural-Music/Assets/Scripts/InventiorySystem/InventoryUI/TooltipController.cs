using UnityEngine;
using UnityEngine.UI;
using InventorySystem.Data;

namespace InventorySystem.UI
{
    /// <summary>
    /// Single tooltip instance used across the entire inventory UI.
    /// Displays item name, category, description, lore, and durability.
    /// Positions itself just above the hovered slot.
    /// </summary>
    public class TooltipController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TMPro.TextMeshProUGUI nameText;
        [SerializeField] private TMPro.TextMeshProUGUI categoryText;
        [SerializeField] private TMPro.TextMeshProUGUI descriptionText;
        [SerializeField] private TMPro.TextMeshProUGUI loreText;
        [SerializeField] private TMPro.TextMeshProUGUI durabilityText;
        [SerializeField] private TMPro.TextMeshProUGUI quantityText;

        [Header("Settings")]
        [Tooltip("Vertical gap in pixels between the slot top edge and the tooltip bottom edge")]
        [SerializeField] private float gapAboveSlot = 8f;
        [SerializeField] private float screenPadding = 12f;

        private Canvas _rootCanvas;
        private RectTransform _canvasRect;
        private bool _isVisible;

        private void Awake()
        {
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_rootCanvas != null)
                _canvasRect = _rootCanvas.transform as RectTransform;

            Hide();
        }

        /// <summary>
        /// Show the tooltip anchored above the given slot RectTransform.
        /// </summary>
        public void Show(ItemInstance instance, int quantity, RectTransform slotRect)
        {
            if (instance == null || tooltipPanel == null || slotRect == null) return;

            var data = instance.Data;

            // Name
            if (nameText != null)
            {
                nameText.text = data.displayName;
                nameText.color = GetCategoryColour(data.category);
            }

            // Category
            if (categoryText != null)
            {
                categoryText.text = data.category.ToString();
            }

            // Description
            if (descriptionText != null)
            {
                descriptionText.text = data.description;
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(data.description));
            }

            // Lore note (the cute little flavour text)
            if (loreText != null)
            {
                loreText.text = !string.IsNullOrEmpty(data.loreNote)
                    ? $"\"{data.loreNote}\""
                    : "";
                loreText.gameObject.SetActive(!string.IsNullOrEmpty(data.loreNote));
            }

            // Durability
            if (durabilityText != null)
            {
                if (data.hasInstanceState)
                {
                    durabilityText.text = $"Durability: {instance.CurrentDurability}/{data.maxDurability}";
                    durabilityText.gameObject.SetActive(true);
                }
                else
                {
                    durabilityText.gameObject.SetActive(false);
                }
            }

            // Quantity
            if (quantityText != null)
            {
                if (quantity > 1)
                {
                    quantityText.text = $"Quantity: {quantity}";
                    quantityText.gameObject.SetActive(true);
                }
                else
                {
                    quantityText.gameObject.SetActive(false);
                }
            }

            // Force layout rebuild so size is correct before positioning
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);

            tooltipPanel.gameObject.SetActive(true);
            _isVisible = true;

            PositionAboveSlot(slotRect);
        }

        public void Hide()
        {
            _isVisible = false;
            if (tooltipPanel != null)
                tooltipPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Positions the tooltip centred horizontally above the slot,
        /// with the tooltip's bottom edge sitting just above the slot's top edge.
        /// Clamps to screen edges so it never goes off-screen.
        /// </summary>
        private void PositionAboveSlot(RectTransform slotRect)
        {
            if (_canvasRect == null) return;

            // Get the slot's world-space corners: [0]=bottom-left, [1]=top-left, [2]=top-right, [3]=bottom-right
            Vector3[] slotCorners = new Vector3[4];
            slotRect.GetWorldCorners(slotCorners);

            // Slot centre X and top Y in screen space
            Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _rootCanvas.worldCamera;

            Vector2 slotTopLeft = RectTransformUtility.WorldToScreenPoint(cam, slotCorners[1]);
            Vector2 slotTopRight = RectTransformUtility.WorldToScreenPoint(cam, slotCorners[2]);

            float slotCentreScreenX = (slotTopLeft.x + slotTopRight.x) * 0.5f;
            float slotTopScreenY = slotTopLeft.y;

            // Convert that screen point to canvas local space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                new Vector2(slotCentreScreenX, slotTopScreenY),
                cam,
                out var anchoredPos
            );

            // Offset upward: tooltip pivot is (0.5, 0) so its bottom edge is at the anchor point
            // Add the gap so it floats above the slot
            anchoredPos.y += gapAboveSlot;

            // Clamp to screen bounds
            var tooltipSize = tooltipPanel.sizeDelta;
            var canvasSize = _canvasRect.sizeDelta;

            float halfTooltipW = tooltipSize.x * 0.5f;
            float minX = -canvasSize.x * 0.5f + halfTooltipW + screenPadding;
            float maxX = canvasSize.x * 0.5f - halfTooltipW - screenPadding;
            float maxY = canvasSize.y * 0.5f - tooltipSize.y - screenPadding;

            anchoredPos.x = Mathf.Clamp(anchoredPos.x, minX, maxX);
            anchoredPos.y = Mathf.Clamp(anchoredPos.y, -canvasSize.y * 0.5f + screenPadding, maxY);

            tooltipPanel.anchoredPosition = anchoredPos;
        }

        private Color GetCategoryColour(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Tool => new Color(0.6f, 0.85f, 1f),
                ItemCategory.Consumable => new Color(0.6f, 1f, 0.6f),
                ItemCategory.Material => new Color(0.9f, 0.8f, 0.6f),
                ItemCategory.Buildable => new Color(1f, 0.7f, 0.5f),
                _ => Color.white
            };
        }
    }
}