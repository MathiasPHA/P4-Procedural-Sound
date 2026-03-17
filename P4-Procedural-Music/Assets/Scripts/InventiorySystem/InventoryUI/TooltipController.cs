using UnityEngine;
using UnityEngine.UI;
using InventorySystem.Data;

namespace InventorySystem.UI
{
    /// <summary>
    /// Single tooltip instance used across the entire inventory UI.
    /// Displays item name, category, description, lore, and durability.
    /// Positions itself near the hovered slot using a simple offset.
    /// Flips direction automatically when it would go off-screen.
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

        [Header("Offset")]
        [Tooltip("Offset from the slot in local canvas units. " +
                 "Positive Y = above the slot. Tweak in inspector to taste.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 40f);

        [Tooltip("Minimum distance from screen edges before the tooltip flips direction")]
        [SerializeField] private float screenPadding = 12f;

        private bool _isVisible;

        private void Awake()
        {
            Hide();
        }

        /// <summary>
        /// Show the tooltip near the given slot.
        /// </summary>
        public void Show(ItemInstance instance, int quantity, RectTransform slotRect)
        {
            if (instance == null || tooltipPanel == null || slotRect == null) return;

            PopulateContent(instance, quantity);

            // Rebuild layout so sizeDelta is accurate before positioning
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);

            tooltipPanel.gameObject.SetActive(true);
            _isVisible = true;

            PositionNearSlot(slotRect);
        }

        public void Hide()
        {
            _isVisible = false;
            if (tooltipPanel != null)
                tooltipPanel.gameObject.SetActive(false);
        }

        // =================================================================
        // Positioning
        // =================================================================

        private void PositionNearSlot(RectTransform slotRect)
        {
            // Both the slot and the tooltip live on the same Screen Space
            // Overlay canvas, so we can work directly in world/screen space.

            Vector3 slotPos = slotRect.position;
            float slotHalfH = slotRect.rect.height * slotRect.lossyScale.y * 0.5f;

            float tooltipH = tooltipPanel.rect.height * tooltipPanel.lossyScale.y;
            float tooltipHalfW = tooltipPanel.rect.width * tooltipPanel.lossyScale.x * 0.5f;

            // Scale the offset by canvas scale so the inspector values
            // behave consistently regardless of CanvasScaler settings
            float scaledOffsetX = offset.x * tooltipPanel.lossyScale.x;
            float scaledOffsetY = offset.y * tooltipPanel.lossyScale.y;

            // Default position: centred above the slot
            float x = slotPos.x + scaledOffsetX;
            float y = slotPos.y + slotHalfH + scaledOffsetY;

            // If the tooltip would overflow the top of the screen, flip below
            if (y + tooltipH > Screen.height - screenPadding)
            {
                y = slotPos.y - slotHalfH - scaledOffsetY - tooltipH;
            }

            // If it would go below the screen bottom, push it back up
            if (y < screenPadding)
            {
                y = screenPadding;
            }

            // Clamp horizontally
            x = Mathf.Clamp(
                x,
                tooltipHalfW + screenPadding,
                Screen.width - tooltipHalfW - screenPadding
            );

            tooltipPanel.position = new Vector3(x, y, slotPos.z);
        }

        // =================================================================
        // Content
        // =================================================================

        private void PopulateContent(ItemInstance instance, int quantity)
        {
            var data = instance.Data;

            if (nameText != null)
            {
                nameText.text = data.displayName;
                nameText.color = GetCategoryColour(data.category);
            }

            if (categoryText != null)
                categoryText.text = data.category.ToString();

            if (descriptionText != null)
            {
                descriptionText.text = data.description;
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(data.description));
            }

            if (loreText != null)
            {
                bool hasLore = !string.IsNullOrEmpty(data.loreNote);
                loreText.text = hasLore ? $"\"{data.loreNote}\"" : "";
                loreText.gameObject.SetActive(hasLore);
            }

            if (durabilityText != null)
            {
                bool show = data.hasInstanceState;
                durabilityText.text = show
                    ? $"Durability: {instance.CurrentDurability}/{data.maxDurability}"
                    : "";
                durabilityText.gameObject.SetActive(show);
            }

            if (quantityText != null)
            {
                bool show = quantity > 1;
                quantityText.text = show ? $"Quantity: {quantity}" : "";
                quantityText.gameObject.SetActive(show);
            }
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