using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using InventorySystem.Data;

namespace InventorySystem.UI
{
    /// <summary>
    /// Visual representation of a single inventory slot.
    /// Handles drag/drop, hover tooltips, and Valheim-style right-click use.
    /// Hotbar slots display a keybind number label (1-8).
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
        [SerializeField] private Color highlightColour = new Color(0.35f, 0.35f, 0.35f, 0.9f);
        [SerializeField] private Color hotbarSelectedColour = new Color(0.9f, 0.75f, 0.3f, 1f);
        [SerializeField] private Color equippedColour = new Color(0.3f, 0.8f, 0.4f, 1f);

        [Header("Layout")]
        [Tooltip("Pixel padding between slot edge and icon")]
        [SerializeField] private float iconPadding = 6f;

        /// <summary>Index into the Inventory.Slots array this UI element represents.</summary>
        public int SlotIndex { get; private set; }

        /// <summary>Whether this slot is part of the hotbar row.</summary>
        public bool IsHotbarSlot { get; private set; }

        private InventoryUIManager _manager;
        private bool _isHighlighted;
        private Color _originalBackgroundColour;

        // Dynamically created keybind label for hotbar slots
        private TMPro.TextMeshProUGUI _keybindLabel;

        // =====================================================================
        // Initialisation
        // =====================================================================

        public void Initialise(int slotIndex, InventoryUIManager manager, bool isHotbar = false)
        {
            SlotIndex = slotIndex;
            _manager = manager;
            IsHotbarSlot = isHotbar;

            if (slotBackground != null)
                _originalBackgroundColour = slotBackground.color;

            if (highlightBorder != null)
                highlightBorder.enabled = false;

            // Force icon to stretch-fill with padding
            if (iconImage != null)
            {
                var iconRect = iconImage.rectTransform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(iconPadding, iconPadding);
                iconRect.offsetMax = new Vector2(-iconPadding, -iconPadding);
                iconImage.preserveAspect = true;
            }

           // Force durability bar to bottom-anchored filled bar
            if (durabilityBar != null)
            {
                durabilityBar.type = Image.Type.Filled;
                durabilityBar.fillMethod = Image.FillMethod.Horizontal;
                durabilityBar.fillOrigin = (int)Image.OriginHorizontal.Left;

                var barRect = durabilityBar.rectTransform;
                barRect.anchorMin        = new Vector2(0f, 0f);
                barRect.anchorMax        = new Vector2(1f, 0f);
                barRect.pivot            = new Vector2(0.5f, 0f);
                barRect.anchoredPosition = new Vector2(0f, 12f);
                barRect.sizeDelta        = new Vector2(-8f, 8f);
            }

            DisableChildRaycastTargets();

            // Anchor quantity text to bottom-right
            if (quantityText != null)
            {
                var textRect = quantityText.rectTransform;
                textRect.anchorMin = new Vector2(1f, 0f);
                textRect.anchorMax = new Vector2(1f, 0f);
                textRect.pivot = new Vector2(1f, 0f);
                textRect.anchoredPosition = new Vector2(-9f, 5f);
                textRect.sizeDelta = new Vector2(40f, 20f);
                quantityText.fontSize = 14;
                quantityText.alignment = TMPro.TextAlignmentOptions.BottomRight;
            }

            // Create keybind number label for hotbar slots
            if (isHotbar)
            {
                CreateKeybindLabel(slotIndex + 1); // 1-based display
            }

            Refresh();
        }

        /// <summary>
        /// Creates a small number label (1-8) in the top-left corner of hotbar slots.
        /// Built entirely in code so you don't need to add it to the prefab.
        /// </summary>
        private void CreateKeybindLabel(int number)
        {
            var labelGO = new GameObject($"KeybindLabel_{number}", typeof(RectTransform));
            labelGO.transform.SetParent(transform, false);
            labelGO.layer = gameObject.layer;

            var rect = labelGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f); // top-left
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(3f, -2f);
            rect.sizeDelta = new Vector2(16f, 16f);

            _keybindLabel = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
            _keybindLabel.text = number.ToString();
            _keybindLabel.fontSize = 11;
            _keybindLabel.fontStyle = TMPro.FontStyles.Bold;
            _keybindLabel.color = new Color(1f, 1f, 1f, 0.7f);
            _keybindLabel.alignment = TMPro.TextAlignmentOptions.TopLeft;
            _keybindLabel.raycastTarget = false;
            _keybindLabel.enableWordWrapping = false;
            _keybindLabel.overflowMode = TMPro.TextOverflowModes.Overflow;
        }

        private void DisableChildRaycastTargets()
        {
            if (iconImage != null) iconImage.raycastTarget = false;
            if (quantityText != null) quantityText.raycastTarget = false;
            if (durabilityBar != null) durabilityBar.raycastTarget = false;
            if (highlightBorder != null) highlightBorder.raycastTarget = false;
        }

        // =====================================================================
        // Visual updates
        // =====================================================================

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

            // Equipped indicator
            UpdateEquippedVisual();
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

            UpdateEquippedVisual();
        }

        /// <summary>
        /// Updates the highlight border based on equipped and hotbar selection state.
        /// Active hotbar slot always gets a border — equipped colour if equipped,
        /// selection colour otherwise.
        /// </summary>
        public void UpdateEquippedVisual()
        {
            if (highlightBorder == null) return;

            bool isEquipped = _manager.IsSlotEquipped(SlotIndex);
            bool isActiveHotbar = IsHotbarSlot && _manager.IsSlotActiveHotbar(SlotIndex);

            if (isEquipped)
            {
                highlightBorder.enabled = true;
                highlightBorder.color = equippedColour;
            }
            else if (isActiveHotbar)
            {
                highlightBorder.enabled = true;
                highlightBorder.color = hotbarSelectedColour;
            }
            else
            {
                highlightBorder.enabled = false;
            }
        }

        /// <summary>
        /// Called when the hotbar selection changes. Delegates to the
        /// unified highlight logic in UpdateEquippedVisual.
        /// </summary>
        public void SetHotbarSelected(bool selected)
        {
            UpdateEquippedVisual();
        }

        // =====================================================================
        // Drag and drop
        // =====================================================================

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_manager.IsSplitSliderOpen) return; // Block drag while slider is open

            var slot = _manager.GetSlotData(SlotIndex);
            if (slot == null || slot.IsEmpty) return;

            _manager.BeginDrag(SlotIndex, false);

            if (iconImage != null)
                iconImage.color = new Color(1f, 1f, 1f, 0.3f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_manager.IsSplitSliderOpen) return;
            _manager.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _manager.EndDrag(droppedOnSlot: false, targetSlotIndex: -1);
            Refresh();
        }

        public void OnDrop(PointerEventData eventData)
        {
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
                _manager.ShowTooltip(slot.Instance, slot.Quantity, transform as RectTransform);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHighlighted = false;

            if (slotBackground != null)
                slotBackground.color = _originalBackgroundColour;

            _manager.HideTooltip();
        }

        // =====================================================================
        // Click
        // Left-click on hotbar slot = Stardew-style select (even if empty)
        // Ctrl + left-click = open precise split slider
        // Right-click = Valheim-style use (equip tool / consume food)
        // Shift + right-click = split stack in half
        // =====================================================================

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;

            // If the split slider is open, close it on any click elsewhere
            if (_manager.IsSplitSliderOpen)
            {
                _manager.CloseSplitSlider();
                return;
            }

            // --- Left-click on hotbar slot = select it (works even if empty) ---
            if (eventData.button == PointerEventData.InputButton.Left && IsHotbarSlot)
            {
                if (!_manager.IsCtrlHeld)
                {
                    _manager.SelectHotbarSlot(SlotIndex);
                    return;
                }
            }

            var slot = _manager.GetSlotData(SlotIndex);
            if (slot == null || slot.IsEmpty) return;

            // --- Left click ---
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Ctrl + left-click = open precise split slider
                if (_manager.IsCtrlHeld && slot.Quantity > 1)
                {
                    _manager.OpenSplitSlider(SlotIndex, transform as RectTransform);
                }
                return;
            }

            // --- Right click ---
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Shift + right-click = split stack in half
                if (_manager.IsModifierHeld)
                {
                    if (slot.Quantity > 1)
                    {
                        _manager.SplitStack(SlotIndex);
                    }
                    return;
                }

                // Plain right-click = use item (equip / consume)
                _manager.UseSlot(SlotIndex);
            }
        }
    }
}