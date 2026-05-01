using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using InventorySystem.Data;
using System;

namespace InventorySystem.UI
{
    /// <summary>
    /// Lightweight slot view used inside ChestUIPanel.
    /// Displays an item icon and quantity; fires a callback on click.
    ///
    /// This is intentionally simpler than InventorySlotUI — no drag/drop,
    /// no tooltip, no hotbar label. Just display + click-to-transfer.
    ///
    /// SETUP:
    ///   The slot prefab needs:
    ///     - An Image component on the root (the slot background)
    ///     - A child Image named "Icon"
    ///     - A child TextMeshProUGUI named "Quantity"
    ///
    ///   ChestUIPanel adds this component at runtime if the prefab doesn't
    ///   already have it, so you can reuse your existing inventory slot prefab.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ChestSlotUI : MonoBehaviour, IPointerClickHandler
    {
        // =====================================================================
        // Inspector (auto-discovered from child hierarchy if not assigned)
        // =====================================================================

        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI quantityText;

        [Header("Colours")]
        [SerializeField] private Color normalColour    = new Color(0.18f, 0.18f, 0.18f, 0.85f);
        [SerializeField] private Color hoverColour     = new Color(0.30f, 0.30f, 0.30f, 0.90f);
        [SerializeField] private Color emptyIconColour = new Color(1f, 1f, 1f, 0f);

        // =====================================================================
        // Runtime state
        // =====================================================================

        private int      _slotIndex;
        private Inventory _inventory;
        private Action   _onClick;
        private Image    _background;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            _background = GetComponent<Image>();
            if (_background != null)
                _background.color = normalColour;

            // Auto-discover child references if not set in Inspector
            if (iconImage == null)
            {
                var iconTransform = transform.Find("Icon");
                if (iconTransform != null)
                    iconImage = iconTransform.GetComponent<Image>();
            }

            if (quantityText == null)
            {
                var qTransform = transform.Find("Quantity");
                if (qTransform != null)
                    quantityText = qTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        // =====================================================================
        // Initialise — called by ChestUIPanel
        // =====================================================================

        public void Initialise(int slotIndex, Inventory inventory, Action onClick)
        {
            _slotIndex  = slotIndex;
            _inventory  = inventory;
            _onClick    = onClick;
        }

        // =====================================================================
        // Refresh display
        // =====================================================================

        public void Refresh(InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                SetEmpty();
                return;
            }

            if (iconImage != null)
            {
                iconImage.sprite  = slot.ItemData.icon;
                iconImage.color   = Color.white;
                iconImage.enabled = true;
            }

            if (quantityText != null)
            {
                // Only show quantity text if there's more than one item
                quantityText.text    = slot.Quantity > 1 ? slot.Quantity.ToString() : "";
                quantityText.enabled = true;
            }
        }

        private void SetEmpty()
        {
            if (iconImage != null)
            {
                iconImage.sprite  = null;
                iconImage.color   = emptyIconColour;
            }

            if (quantityText != null)
                quantityText.text = "";
        }

        // =====================================================================
        // Hover highlight
        // =====================================================================

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _)
        {
            if (_background != null)
                _background.color = hoverColour;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData _)
        {
            if (_background != null)
                _background.color = normalColour;
        }

        // =====================================================================
        // Click
        // =====================================================================

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _onClick?.Invoke();
        }
    }
}
