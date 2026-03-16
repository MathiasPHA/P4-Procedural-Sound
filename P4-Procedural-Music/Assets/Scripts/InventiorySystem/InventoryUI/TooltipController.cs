using UnityEngine;
using UnityEngine.UI;
using InventorySystem.Data;

namespace InventorySystem.UI
{
    /// <summary>
    /// Single tooltip instance used across the entire inventory UI.
    /// Displays item name, category, description, lore, and durability.
    /// Follows the cursor with a slight offset so it doesn't obscure the slot.
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
        [SerializeField] private Vector2 offset = new Vector2(16f, -16f);
        [SerializeField] private float padding = 12f;

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

        public void Show(ItemInstance instance, int quantity, Vector2 screenPosition)
        {
            if (instance == null || tooltipPanel == null) return;

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

            UpdatePosition(screenPosition);
        }

        public void Hide()
        {
            _isVisible = false;
            if (tooltipPanel != null)
                tooltipPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Update tooltip position to follow cursor. Call from Update when visible.
        /// </summary>
        public void UpdatePosition(Vector2 screenPosition)
        {
            if (!_isVisible || tooltipPanel == null || _canvasRect == null) return;

            Vector2 anchoredPos;

            if (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screenPosition, null, out anchoredPos);
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screenPosition, _rootCanvas.worldCamera, out anchoredPos);
            }

            anchoredPos += offset;

            // Clamp to screen bounds so the tooltip doesn't go off-screen
            var tooltipSize = tooltipPanel.sizeDelta;
            var canvasSize = _canvasRect.sizeDelta;

            float maxX = canvasSize.x * 0.5f - tooltipSize.x - padding;
            float minX = -canvasSize.x * 0.5f + padding;
            float maxY = canvasSize.y * 0.5f - padding;
            float minY = -canvasSize.y * 0.5f + tooltipSize.y + padding;

            anchoredPos.x = Mathf.Clamp(anchoredPos.x, minX, maxX);
            anchoredPos.y = Mathf.Clamp(anchoredPos.y, minY, maxY);

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
