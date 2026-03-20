using System;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.UI
{
    /// <summary>
    /// Popup panel for precise stack splitting.
    /// Shows a slider, quantity display, and confirm/cancel buttons.
    /// 
    /// BUILD THIS AS A PREFAB IN UNITY:
    /// 1. Create a Panel (dark background, ~220x130)
    /// 2. Add child: TMP text for title ("Split stack")
    /// 3. Add child: TMP text for quantity display (large, gold coloured)
    /// 4. Add child: Unity Slider (whole numbers, horizontal)
    /// 5. Add child row with two Buttons: "Take" (green) and "Cancel" (red)
    /// 6. Attach this script to the panel root
    /// 7. Wire all serialized references in the inspector
    /// 8. Save as prefab, assign to InventoryUIManager's splitSlider field
    /// 
    /// The panel starts inactive. InventoryUIManager calls Open/Close.
    /// </summary>
    public class StackSplitSliderUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMPro.TextMeshProUGUI titleLabel;
        [SerializeField] private TMPro.TextMeshProUGUI quantityLabel;
        [SerializeField] private Slider slider;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        /// <summary>Fires when the player confirms a split. (slotIndex, amount)</summary>
        public event Action<int, int> OnConfirm;

        /// <summary>Fires when the player cancels.</summary>
        public event Action OnCancel;

        // State
        private int _sourceSlotIndex;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            // Wire button callbacks
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);
            if (slider != null)
                slider.onValueChanged.AddListener(OnSliderChanged);

            Close();
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Open the slider for the given slot. Positions near the slot.
        /// </summary>
        public void Open(int slotIndex, int maxQuantity, RectTransform slotRect)
        {
            _sourceSlotIndex = slotIndex;

            // Configure slider range
            slider.wholeNumbers = true;
            slider.minValue = 1;
            slider.maxValue = maxQuantity;
            slider.value = Mathf.CeilToInt(maxQuantity / 2f);

            // Update labels
            if (titleLabel != null)
                titleLabel.text = $"Split stack ({maxQuantity})";

            UpdateQuantityLabel(slider.value);

            // Position above the slot
            PositionNearSlot(slotRect);

            panel.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
        }

        // =====================================================================
        // Positioning
        // =====================================================================

        private void PositionNearSlot(RectTransform slotRect)
        {
            Vector3 slotPos = slotRect.position;
            float slotHalfH = slotRect.rect.height * slotRect.lossyScale.y * 0.5f;
            float panelH = panel.rect.height * panel.lossyScale.y;
            float panelHalfW = panel.rect.width * panel.lossyScale.x * 0.5f;

            // Default: above the slot
            float x = slotPos.x;
            float y = slotPos.y + slotHalfH + panelH * 0.5f + 8f;

            // If it overflows the top of the screen, flip below
            if (y + panelH * 0.5f > Screen.height - 12f)
            {
                y = slotPos.y - slotHalfH - panelH * 0.5f - 8f;
            }

            // Clamp horizontally
            x = Mathf.Clamp(x, panelHalfW + 12f, Screen.width - panelHalfW - 12f);

            panel.position = new Vector3(x, y, slotPos.z);
        }

        // =====================================================================
        // Callbacks
        // =====================================================================

        private void OnSliderChanged(float value)
        {
            UpdateQuantityLabel(value);
        }

        private void UpdateQuantityLabel(float value)
        {
            if (quantityLabel != null)
                quantityLabel.text = Mathf.RoundToInt(value).ToString();
        }

        private void OnConfirmClicked()
        {
            int amount = Mathf.RoundToInt(slider.value);
            Close();
            OnConfirm?.Invoke(_sourceSlotIndex, amount);
        }

        private void OnCancelClicked()
        {
            Close();
            OnCancel?.Invoke();
        }
    }
}