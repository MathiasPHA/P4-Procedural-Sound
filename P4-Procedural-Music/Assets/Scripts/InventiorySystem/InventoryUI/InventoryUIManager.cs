using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;
using InventorySystem.Input;

namespace InventorySystem.UI
{
    /// <summary>
    /// Central UI orchestrator. Bridges between the Inventory data layer
    /// and all visual elements (slots, tooltip, drag ghost, split slider).
    ///
    /// Initialized explicitly by InventoryBootstrap.Start() — not by Awake.
    /// This guarantees InventoryInputProvider has resolved its actions first.
    ///
    /// IMPORTANT: This manager no longer subscribes to OnToggleInventory itself.
    /// Open/close is driven exclusively by UICoordinator via ForceOpen/ForceClose
    /// so both panels always move together.
    /// </summary>
    public class InventoryUIManager : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InventoryInputProvider inputProvider;

        [Header("UI References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private Transform hotbarContainer;
        [SerializeField] private DraggedItemUI draggedItem;
        [SerializeField] private TooltipController tooltip;
        [SerializeField] private StackSplitSliderUI splitSlider;

        [Header("Prefab")]
        [SerializeField] private GameObject slotPrefab;

        [Header("World Drop")]
        [SerializeField] private GameObject worldItemPrefab;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float dropDistance = 1.5f;

        [Header("Player State")]
        [SerializeField] private PlayerStateManager playerStateManager;

        // --- Runtime state ---
        private Inventory _inventory;
        private readonly List<InventorySlotUI> _allSlotUIs = new();
        private readonly List<InventorySlotUI> _hotbarSlotUIs = new();
        private bool _inventoryOpen;
        private bool _initialized;
        private bool _scrollBlocked;

        // Drag state
        private bool _isDragging;
        private int _dragSourceIndex = -1;
        private bool _dragIsSplit;
        private int _dragOriginalQuantity;
        private InventorySlot _splitHeldSlot;

        // =====================================================================
        // Initialisation (called by InventoryBootstrap.Start)
        // =====================================================================

        public void Initialise(Inventory inventory)
        {
            _inventory = inventory;

            if (!ValidateReferences())
            {
                Debug.LogError("[InventoryUIManager] Initialization failed — check missing references above.");
                return;
            }

            SpawnSlotUIs();
            SubscribeToEvents();
            RefreshAllSlots();

            inventoryPanel.SetActive(false);
            _inventoryOpen = false;
            _initialized = true;

            UpdateHotbarSelection(_inventory.ActiveHotbarIndex);
            Debug.Log("[InventoryUIManager] Initialized successfully.");
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (inputProvider == null)
            {
                Debug.LogError("[InventoryUIManager] InputProvider is not assigned!");
                valid = false;
            }
            else if (!inputProvider.IsInitialized)
            {
                Debug.LogWarning("[InventoryUIManager] InputProvider exists but hasn't initialized yet. " +
                                 "Check execution order (InventoryInputProvider should be -100).");
            }

            if (inventoryPanel == null) { Debug.LogError("[InventoryUIManager] InventoryPanel not assigned!"); valid = false; }
            if (slotContainer == null) { Debug.LogError("[InventoryUIManager] SlotContainer not assigned!"); valid = false; }
            if (hotbarContainer == null) { Debug.LogError("[InventoryUIManager] HotbarContainer not assigned!"); valid = false; }
            if (draggedItem == null) { Debug.LogError("[InventoryUIManager] DraggedItem not assigned!"); valid = false; }
            if (slotPrefab == null) { Debug.LogError("[InventoryUIManager] SlotPrefab not assigned!"); valid = false; }

            if (tooltip == null) Debug.LogWarning("[InventoryUIManager] Tooltip not assigned — hover info disabled.");
            if (splitSlider == null) Debug.LogWarning("[InventoryUIManager] SplitSlider not assigned — Ctrl+click split disabled.");
            if (playerStateManager == null) Debug.LogWarning("[InventoryUIManager] PlayerStateManager not assigned — state transitions disabled.");

            return valid;
        }

        private void SpawnSlotUIs()
        {
            _allSlotUIs.Clear();
            _hotbarSlotUIs.Clear();

            for (int i = 0; i < _inventory.SlotCount; i++)
            {
                bool isHotbar = i < Inventory.HotbarSize;
                var parent = isHotbar ? hotbarContainer : slotContainer;

                var go = Instantiate(slotPrefab, parent);
                go.name = $"Slot_{i}";

                var slotUI = go.GetComponent<InventorySlotUI>();
                slotUI.Initialise(i, this, isHotbar);

                _allSlotUIs.Add(slotUI);
                if (isHotbar) _hotbarSlotUIs.Add(slotUI);
            }
        }

        private void SubscribeToEvents()
        {
            // Inventory data events
            _inventory.OnSlotChanged += OnSlotDataChanged;
            _inventory.OnHotbarSelectionChanged += UpdateHotbarSelection;
            _inventory.OnEquippedChanged += OnEquippedChanged;
            _inventory.OnItemConsumed += OnItemConsumed;

            // NOTE: OnToggleInventory is intentionally NOT subscribed here.
            // UICoordinator owns that subscription and drives open/close via
            // ForceOpen() / ForceClose() so both panels always move together.

            // Hotbar & scroll still come directly from input
            if (inputProvider != null)
            {
                inputProvider.OnHotbarSelect += OnHotbarKeyPressed;
                inputProvider.OnScrollHotbar += OnScrollHotbar;
            }

            // Split slider events
            if (splitSlider != null)
            {
                splitSlider.OnConfirm += OnSplitSliderConfirm;
                splitSlider.OnCancel += OnSplitSliderCancel;
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnSlotChanged -= OnSlotDataChanged;
                _inventory.OnHotbarSelectionChanged -= UpdateHotbarSelection;
                _inventory.OnEquippedChanged -= OnEquippedChanged;
                _inventory.OnItemConsumed -= OnItemConsumed;
            }

            if (inputProvider != null)
            {
                inputProvider.OnHotbarSelect -= OnHotbarKeyPressed;
                inputProvider.OnScrollHotbar -= OnScrollHotbar;
            }

            if (splitSlider != null)
            {
                splitSlider.OnConfirm -= OnSplitSliderConfirm;
                splitSlider.OnCancel -= OnSplitSliderCancel;
            }
        }

        // =====================================================================
        // Public API — called by InventorySlotUI
        // =====================================================================

        public InventorySlot GetSlotData(int index)
        {
            if (_inventory == null || index < 0 || index >= _inventory.SlotCount) return null;
            return _inventory.Slots[index];
        }

        public bool IsModifierHeld => inputProvider != null && inputProvider.IsModifierHeld;
        public bool IsCtrlHeld => inputProvider != null && inputProvider.IsCtrlHeld;
        public bool IsSplitSliderOpen => splitSlider != null && splitSlider.IsOpen;

        public void OpenSplitSlider(int slotIndex, RectTransform slotRect)
        {
            if (splitSlider == null) return;
            var slot = _inventory.Slots[slotIndex];
            if (slot.IsEmpty || slot.Quantity <= 1) return;
            splitSlider.Open(slotIndex, slot.Quantity, slotRect);
        }

        public void CloseSplitSlider() => splitSlider?.Close();

        // --- Drag and drop ---

        public void BeginDrag(int sourceIndex, bool isSplit)
        {
            var slot = _inventory.Slots[sourceIndex];
            if (slot.IsEmpty) return;

            _isDragging = true;
            _dragSourceIndex = sourceIndex;
            _dragIsSplit = isSplit;

            int dragQuantity;

            if (isSplit && slot.Quantity > 1)
            {
                _dragOriginalQuantity = slot.Quantity;
                _splitHeldSlot = slot.SplitStack();
                dragQuantity = _splitHeldSlot?.Quantity ?? slot.Quantity;
                RefreshSlot(sourceIndex);
            }
            else
            {
                dragQuantity = slot.Quantity;
                _splitHeldSlot = null;
            }

            draggedItem.Show(slot.ItemData.icon, dragQuantity, sourceIndex, isSplit);
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (_isDragging) draggedItem.FollowPointer(screenPosition);
        }

        public void EndDrag(bool droppedOnSlot, int targetSlotIndex)
        {
            if (!_isDragging) return;

            if (droppedOnSlot && targetSlotIndex >= 0 && targetSlotIndex != _dragSourceIndex)
            {
                if (_dragIsSplit && _splitHeldSlot != null)
                {
                    var targetSlot = _inventory.Slots[targetSlotIndex];
                    if (targetSlot.IsEmpty)
                    {
                        targetSlot.Set(_splitHeldSlot.Instance, _splitHeldSlot.Quantity);
                        RefreshSlot(targetSlotIndex);
                    }
                    else if (targetSlot.CanAccept(_splitHeldSlot.ItemData))
                    {
                        targetSlot.AddToStack(_splitHeldSlot.Quantity);
                        RefreshSlot(targetSlotIndex);
                    }
                    else
                    {
                        _inventory.Slots[_dragSourceIndex].AddToStack(_splitHeldSlot.Quantity);
                        RefreshSlot(_dragSourceIndex);
                    }
                }
                else
                {
                    _inventory.MergeOrSwap(_dragSourceIndex, targetSlotIndex);
                }
            }
            else if (!droppedOnSlot)
            {
                if (_dragIsSplit && _splitHeldSlot != null)
                {
                    if (_splitHeldSlot.ItemData.isDroppable)
                        SpawnWorldDrop(_splitHeldSlot.Instance, _splitHeldSlot.Quantity);
                    else
                    {
                        _inventory.Slots[_dragSourceIndex].AddToStack(_splitHeldSlot.Quantity);
                        RefreshSlot(_dragSourceIndex);
                    }
                }
                else
                {
                    var sourceSlot = _inventory.Slots[_dragSourceIndex];
                    if (!sourceSlot.IsEmpty && sourceSlot.ItemData.isDroppable)
                    {
                        SpawnWorldDrop(sourceSlot.Instance, sourceSlot.Quantity);
                        _inventory.ClearSlot(_dragSourceIndex);
                    }
                }
            }
            else
            {
                if (_dragIsSplit && _splitHeldSlot != null)
                {
                    _inventory.Slots[_dragSourceIndex].AddToStack(_splitHeldSlot.Quantity);
                    RefreshSlot(_dragSourceIndex);
                }
            }

            _isDragging = false;
            _dragSourceIndex = -1;
            _splitHeldSlot = null;
            draggedItem.Hide();
            _inventory.NotifyChanged();
        }

        // --- Tooltip ---

        public void ShowTooltip(ItemInstance instance, int quantity, RectTransform slotRect)
        {
            if (!_isDragging) tooltip?.Show(instance, quantity, slotRect);
        }

        public void HideTooltip() => tooltip?.Hide();

        // --- Stack splitting ---

        public void SplitStack(int slotIndex)
        {
            int newSlotIndex = _inventory.SplitStack(slotIndex);
            if (newSlotIndex >= 0)
            {
                RefreshSlot(slotIndex);
                RefreshSlot(newSlotIndex);
            }
        }

        // --- Item use ---

        public void UseSlot(int slotIndex) => _inventory.UseSlot(slotIndex);
        public bool IsSlotEquipped(int slotIndex) => _inventory.EquippedSlotIndex == slotIndex;
        public bool IsSlotActiveHotbar(int slotIndex) => slotIndex == _inventory.ActiveHotbarIndex;

        // =====================================================================
        // Panel open / close — driven by UICoordinator
        // =====================================================================

        /// <summary>
        /// Unconditionally open the inventory panel.
        /// Called by UICoordinator only — do not call directly from other systems.
        /// </summary>
        public void ForceOpen()
        {
            if (_inventoryOpen) return;

            _inventoryOpen = true;
            inventoryPanel.SetActive(true);

            RefreshAllSlots();
            inputProvider?.DisableMovement();
            if (playerStateManager != null)
                playerStateManager.SwitchState(playerStateManager.inventoryState);
        }

        /// <summary>
        /// Unconditionally close the inventory panel.
        /// Called by UICoordinator only — do not call directly from other systems.
        /// </summary>
        public void ForceClose()
        {
            if (!_inventoryOpen) return;

            _inventoryOpen = false;
            inventoryPanel.SetActive(false);

            HideTooltip();
            CloseSplitSlider();
            inputProvider?.EnableMovement();
            if (playerStateManager != null)
                playerStateManager.SwitchState(playerStateManager.idleState);
        }

        /// <summary>Whether the inventory panel is currently visible.</summary>
        public bool IsInventoryOpen => _inventoryOpen;

        /// <summary>
        /// Close the inventory if it is open. Used by PauseManager's Escape chain.
        /// Only closes — never opens — to match the "Escape = close" convention.
        /// </summary>
        public void ToggleInventory()
        {
            if (_inventoryOpen) ForceClose();
        }

        // =====================================================================
        // Hotbar
        // =====================================================================

        private void OnHotbarKeyPressed(int index) => _inventory.SelectHotbar(index);

        /// <summary>
        /// Stardew-style hotbar selection. Called by InventorySlotUI on left-click
        /// and by scroll. Selects the slot and auto-equips tools/buildables.
        /// </summary>
        public void SelectHotbarSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Inventory.HotbarSize) return;
            _inventory.SelectHotbar(slotIndex);
        }

        public void SetScrollBlocked(bool blocked) => _scrollBlocked = blocked;

        private void OnScrollHotbar(float scrollValue)
        {
            if (_inventoryOpen || _scrollBlocked) return;
            int current = _inventory.ActiveHotbarIndex;
            current = scrollValue > 0
                ? (current - 1 + Inventory.HotbarSize) % Inventory.HotbarSize
                : (current + 1) % Inventory.HotbarSize;
            _inventory.SelectHotbar(current);
        }

        private void UpdateHotbarSelection(int activeIndex)
        {
            for (int i = 0; i < _hotbarSlotUIs.Count; i++)
                _hotbarSlotUIs[i].SetHotbarSelected(i == activeIndex);
        }

        // =====================================================================
        // Event handlers
        // =====================================================================

        private void OnEquippedChanged(int equippedSlotIndex)
        {
            for (int i = 0; i < _allSlotUIs.Count; i++)
                _allSlotUIs[i].UpdateEquippedVisual();
        }

        private void OnItemConsumed(ItemInstance consumed)
        {
            Debug.Log($"[Inventory] Consumed: {consumed.Data.displayName}");
        }

        private void OnSplitSliderConfirm(int slotIndex, int amount)
        {
            int newSlot = _inventory.SplitExact(slotIndex, amount);
            if (newSlot >= 0)
            {
                RefreshSlot(slotIndex);
                RefreshSlot(newSlot);
            }
        }

        private void OnSplitSliderCancel() { }

        // =====================================================================
        // World drops
        // =====================================================================

        private Vector2 GetPlayerFacingDirection()
        {
            if (playerStateManager == null) return Vector2.down;

            return playerStateManager.playerDir switch
            {
                "Up"    => Vector2.up,
                "Down"  => Vector2.down,
                "Left"  => Vector2.left,
                "Right" => Vector2.right,
                _       => Vector2.down
            };
        }

        private void SpawnWorldDrop(ItemInstance instance, int quantity)
        {
            if (worldItemPrefab == null || playerTransform == null) return;

            Vector2 dropDir = GetPlayerFacingDirection();
            Vector2 dropPos = (Vector2)playerTransform.position + dropDir * dropDistance;

            var go = Instantiate(worldItemPrefab, dropPos, Quaternion.identity);
            var worldItem = go.GetComponent<WorldItem>();
            worldItem?.Initialise(instance, quantity, dropDir);
        }

        // =====================================================================
        // Refresh
        // =====================================================================

        private void OnSlotDataChanged(int slotIndex) => RefreshSlot(slotIndex);

        private void RefreshSlot(int index)
        {
            if (index >= 0 && index < _allSlotUIs.Count)
                _allSlotUIs[index].Refresh();
        }

        private void RefreshAllSlots()
        {
            for (int i = 0; i < _allSlotUIs.Count; i++)
                _allSlotUIs[i].Refresh();
        }
    }
}