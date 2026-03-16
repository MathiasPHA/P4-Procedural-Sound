using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InventorySystem.Data;

namespace InventorySystem.UI
{
    /// <summary>
    /// Visual representation of a single inventory slot.
    /// Handles drag/drop via Unity's EventSystem interfaces and
    /// hover for tooltips. Communicates with InventoryUIManager for
    /// all actual data operations.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class InventorySlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Slot Visuals")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMPro.TextMeshProUGUI quantityText;
        [SerializeField] private Image durabilityBar;
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image highlightBorder;

        [Header("Colours")]
        [SerializeField] private Color normalColour = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color highlightColour = new Color(0.35f, 0.35f, 0.35f, 0.9f);
        [SerializeField] private Color hotbarSelectedColour = new Color(0.9f, 0.75f, 0.3f, 1f);

        /// <summary>Index into the Inventory.Slots array this UI element represents.</summary>
        public int SlotIndex { get; private set; }

        /// <summary>Whether this slot is part of the hotbar row.</summary>
        public bool IsHotbarSlot { get; private set; }

        private InventoryUIManager _manager;
        private bool _isHighlighted;

        // =====================================================================
        // Initialisation
        // =====================================================================

        public void Initialise(int slotIndex, InventoryUIManager manager, bool isHotbar = false)
        {
            SlotIndex = slotIndex;
            _manager = manager;
            IsHotbarSlot = isHotbar;

            if (highlightBorder != null)
                highlightBorder.enabled = false;

            Refresh();
        }

        // =====================================================================
        // Visual updates
        // =====================================================================

        /// <summary>
        /// Refresh visuals from the current inventory data.
        /// Called by InventoryUIManager when the slot's data changes.
        /// </summary>
        public void Refresh()
        {
            var slot = _manager.GetSlotData(SlotIndex);

            if (slot == null || slot.IsEmpty)
            {
                ShowEmpty();
                return;
            }

            // Icon
            iconImage.sprite = slot.ItemData.icon;
            iconImage.color = Color.white;
            iconImage.enabled = true;

            // Quantity (only show for stacks > 1)
            if (quantityText != null)
            {
                bool showQuantity = slot.Quantity > 1;
                quantityText.text = showQuantity ? slot.Quantity.ToString() : "";
                quantityText.enabled = showQuantity;
            }

            // Durability bar
            if (durabilityBar != null)
            {
                bool showDurability = slot.Instance.Data.hasInstanceState
                    && slot.Instance.DurabilityNormalized < 1f;
                durabilityBar.enabled = showDurability;

                if (showDurability)
                {
                    durabilityBar.fillAmount = slot.Instance.DurabilityNormalized;
                    durabilityBar.color = Color.Lerp(Color.red, Color.green,
                        slot.Instance.DurabilityNormalized);
                }
            }
        }

        private void ShowEmpty()
        {
            iconImage.sprite = null;
            iconImage.color = Color.clear;
            iconImage.enabled = false;

            if (quantityText != null)
            {
                quantityText.text = "";
                quantityText.enabled = false;
            }

            if (durabilityBar != null)
                durabilityBar.enabled = false;
        }

        /// <summary>
        /// Show/hide the hotbar selection highlight.
        /// </summary>
        public void SetHotbarSelected(bool selected)
        {
            if (highlightBorder != null)
            {
                highlightBorder.enabled = selected;
                highlightBorder.color = hotbarSelectedColour;
            }
        }

        // =====================================================================
        // Drag and drop
        // =====================================================================

        public void OnBeginDrag(PointerEventData eventData)
        {
            var slot = _manager.GetSlotData(SlotIndex);
            if (slot == null || slot.IsEmpty) return;

            // Right-click drag = split stack
            bool isSplit = eventData.button == PointerEventData.InputButton.Right;

            _manager.BeginDrag(SlotIndex, isSplit);

            // Dim the source slot while dragging
            if (iconImage != null)
                iconImage.color = new Color(1f, 1f, 1f, 0.3f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _manager.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // If we didn't drop on a valid target, the manager handles
            // either dropping to world or cancelling
            _manager.EndDrag(droppedOnSlot: false, targetSlotIndex: -1);

            // Restore icon alpha
            Refresh();
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Something was dropped on us
            _manager.EndDrag(droppedOnSlot: true, targetSlotIndex: SlotIndex);
        }

        // =====================================================================
        // Hover — tooltip
        // =====================================================================

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHighlighted = true;

            if (slotBackground != null)
                slotBackground.color = highlightColour;

            var slot = _manager.GetSlotData(SlotIndex);
            if (slot != null && !slot.IsEmpty)
            {
                _manager.ShowTooltip(slot.Instance, slot.Quantity, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHighlighted = false;

            if (slotBackground != null)
                slotBackground.color = normalColour;

            _manager.HideTooltip();
        }

        // =====================================================================
        // Click — right-click to split into empty slot
        // =====================================================================

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !eventData.dragging)
            {
                var slot = _manager.GetSlotData(SlotIndex);
                if (slot != null && !slot.IsEmpty && slot.Quantity > 1)
                {
                    _manager.SplitStack(SlotIndex);
                }
            }
        }
    }
}
