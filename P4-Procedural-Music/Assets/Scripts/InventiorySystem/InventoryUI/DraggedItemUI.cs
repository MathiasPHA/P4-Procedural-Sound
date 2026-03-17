using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.UI
{
    /// <summary>
    /// Visual ghost that follows the cursor while dragging an item.
    /// There should only be one of these in the scene, managed by InventoryUIManager.
    /// Lives on a Canvas with highest sort order so it renders above everything.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DraggedItemUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMPro.TextMeshProUGUI quantityText;
        [SerializeField] private float iconPadding = 4f;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Canvas _rootCanvas;

        public bool IsDragging { get; private set; }

        /// <summary>The slot index the drag originated from.</summary>
        public int SourceSlotIndex { get; private set; } = -1;

        /// <summary>Whether only half the stack was picked up (right-click split).</summary>
        public bool IsSplitDrag { get; private set; }

        /// <summary>Quantity being dragged (may differ from source slot if split).</summary>
        public int DragQuantity { get; private set; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

            // Ensure raycasts pass through the ghost to hit drop targets beneath
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // Force icon to stretch-fill with padding
            if (iconImage != null)
            {
                var iconRect = iconImage.rectTransform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(iconPadding, iconPadding);
                iconRect.offsetMax = new Vector2(-iconPadding, -iconPadding);
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            // Anchor quantity text to bottom-right corner
            if (quantityText != null)
            {
                var textRect = quantityText.rectTransform;
                textRect.anchorMin = new Vector2(1f, 0f);
                textRect.anchorMax = new Vector2(1f, 0f);
                textRect.pivot = new Vector2(1f, 0f);
                textRect.anchoredPosition = new Vector2(-2f, 2f);
                textRect.sizeDelta = new Vector2(40f, 20f);
                quantityText.fontSize = 14;
                quantityText.alignment = TMPro.TextAlignmentOptions.BottomRight;
                quantityText.raycastTarget = false;
            }

            Hide();
        }

        /// <summary>
        /// Begin showing the drag ghost with the given item visual.
        /// </summary>
        public void Show(Sprite icon, int quantity, int sourceSlotIndex, bool isSplit)
        {
            IsDragging = true;
            SourceSlotIndex = sourceSlotIndex;
            IsSplitDrag = isSplit;
            DragQuantity = quantity;

            iconImage.sprite = icon;
            iconImage.enabled = true;

            if (quantityText != null)
            {
                quantityText.text = quantity > 1 ? quantity.ToString() : "";
                quantityText.enabled = quantity > 1;
            }

            _canvasGroup.alpha = 0.8f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            IsDragging = false;
            SourceSlotIndex = -1;
            IsSplitDrag = false;
            DragQuantity = 0;

            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Call every frame during drag to follow the pointer.
        /// Pass the screen-space pointer position.
        /// </summary>
        public void FollowPointer(Vector2 screenPosition)
        {
            if (_rootCanvas == null) return;

            if (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _rectTransform.position = screenPosition;
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rootCanvas.transform as RectTransform,
                    screenPosition,
                    _rootCanvas.worldCamera,
                    out var localPoint
                );
                _rectTransform.position = _rootCanvas.transform.TransformPoint(localPoint);
            }
        }
    }
}