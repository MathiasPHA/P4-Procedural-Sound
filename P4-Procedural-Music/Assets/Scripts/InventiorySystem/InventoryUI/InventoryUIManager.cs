using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;
using InventorySystem.Input;

namespace InventorySystem.UI
{
    /// <summary>
    /// Central UI orchestrator. Owns references to all slot UIs, the drag ghost,
    /// tooltip, and the inventory panel. Bridges between the data-layer Inventory
    /// and the visual elements.
    ///
    /// This is the only UI script that talks to the Inventory data directly.
    /// Individual InventorySlotUI components call back into this manager.
    /// </summary>
    public class InventoryUIManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private InventoryInputProvider inputProvider;

        [Header("UI References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private Transform hotbarContainer;
        [SerializeField] private DraggedItemUI draggedItem;
        [SerializeField] private TooltipController tooltip;

        [Header("Prefab")]
        [SerializeField] private GameObject slotPrefab;

        [Header("World Drop")]
        [Tooltip("Prefab spawned when an item is dropped into the world")]
        [SerializeField] private GameObject worldItemPrefab;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float dropDistance = 1.5f;

        // --- Runtime state ---
        private Inventory _inventory;
        private readonly List<InventorySlotUI> _allSlotUIs = new();
        private readonly List<InventorySlotUI> _hotbarSlotUIs = new();
        private bool _inventoryOpen;

        // Drag state
        private bool _isDragging;
        private int _dragSourceIndex = -1;
        private bool _dragIsSplit;
        private int _dragOriginalQuantity;
        private InventorySlot _splitHeldSlot; // temp slot holding split items during drag

        // =====================================================================
        // Initialisation
        // =====================================================================

        /// <summary>
        /// Call this once from your game bootstrap after creating the Inventory.
        /// Spawns all slot UI elements and wires up events.
        /// </summary>
        public void Initialise(Inventory inventory)
        {
            _inventory = inventory;

            SpawnSlotUIs();
            SubscribeToEvents();
            RefreshAllSlots();

            // Start with inventory panel closed, hotbar visible
            inventoryPanel.SetActive(false);
            _inventoryOpen = false;

            UpdateHotbarSelection(_inventory.ActiveHotbarIndex);
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

                if (isHotbar)
                    _hotbarSlotUIs.Add(slotUI);
            }
        }

        private void SubscribeToEvents()
        {
            // Data events
            _inventory.OnSlotChanged += OnSlotDataChanged;
            _inventory.OnHotbarSelectionChanged += UpdateHotbarSelection;
            _inventory.OnEquippedChanged += OnEquippedChanged;
            _inventory.OnItemConsumed += OnItemConsumed;

            // Input events
            if (inputProvider != null)
            {
                inputProvider.OnToggleInventory += ToggleInventory;
                inputProvider.OnHotbarSelect += OnHotbarKeyPressed;
                inputProvider.OnScrollHotbar += OnScrollHotbar;
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
                inputProvider.OnToggleInventory -= ToggleInventory;
                inputProvider.OnHotbarSelect -= OnHotbarKeyPressed;
                inputProvider.OnScrollHotbar -= OnScrollHotbar;
            }
        }

        // =====================================================================
        // Public API — called by InventorySlotUI
        // =====================================================================

        public InventorySlot GetSlotData(int index)
        {
            if (_inventory == null || index < 0 || index >= _inventory.SlotCount)
                return null;
            return _inventory.Slots[index];
        }

        /// <summary>
        /// Whether the shift modifier key is currently held.
        /// Proxies through the InventoryInputProvider (new Input System).
        /// </summary>
        public bool IsModifierHeld => inputProvider != null && inputProvider.IsModifierHeld;

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
                // Split: take half, leave rest in source slot
                _dragOriginalQuantity = slot.Quantity;
                dragQuantity = slot.Quantity / 2;

                // Perform the split in the data layer
                // We temporarily hold the split portion
                _splitHeldSlot = slot.SplitStack();
                if (_splitHeldSlot != null)
                {
                    dragQuantity = _splitHeldSlot.Quantity;
                }

                // Refresh source to show reduced stack
                RefreshSlot(sourceIndex);
            }
            else
            {
                // Full drag — we'll move the entire slot contents on drop
                dragQuantity = slot.Quantity;
                _splitHeldSlot = null;
            }

            draggedItem.Show(slot.ItemData.icon, dragQuantity, sourceIndex, isSplit);
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (!_isDragging) return;
            draggedItem.FollowPointer(screenPosition);
        }

        public void EndDrag(bool droppedOnSlot, int targetSlotIndex)
        {
            if (!_isDragging) return;

            if (droppedOnSlot && targetSlotIndex >= 0 && targetSlotIndex != _dragSourceIndex)
            {
                if (_dragIsSplit && _splitHeldSlot != null)
                {
                    // Place the split half into the target
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
                        // Target can't accept — return split to source
                        _inventory.Slots[_dragSourceIndex].AddToStack(_splitHeldSlot.Quantity);
                        RefreshSlot(_dragSourceIndex);
                    }
                }
                else
                {
                    // Full drag — merge or swap
                    _inventory.MergeOrSwap(_dragSourceIndex, targetSlotIndex);
                }
            }
            else if (!droppedOnSlot)
            {
                if (_dragIsSplit && _splitHeldSlot != null)
                {
                    // Dropped outside — try to drop split portion to world
                    if (_splitHeldSlot.ItemData.isDroppable)
                    {
                        SpawnWorldDrop(_splitHeldSlot.Instance, _splitHeldSlot.Quantity);
                    }
                    else
                    {
                        // Can't drop — return to source
                        _inventory.Slots[_dragSourceIndex].AddToStack(_splitHeldSlot.Quantity);
                        RefreshSlot(_dragSourceIndex);
                    }
                }
                else
                {
                    // Full drag dropped outside — drop to world
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
                // Dropped on self or invalid — return split if applicable
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

            // Ensure crafting and other listeners know something changed
            _inventory.NotifyChanged();
        }

        // --- Tooltip ---

        public void ShowTooltip(ItemInstance instance, int quantity, RectTransform slotRect)
        {
            if (_isDragging) return; // Don't show tooltip while dragging
            tooltip?.Show(instance, quantity, slotRect);
        }

        public void HideTooltip()
        {
            tooltip?.Hide();
        }

        // --- Stack splitting (right-click without drag) ---

        public void SplitStack(int slotIndex)
        {
            int newSlotIndex = _inventory.SplitStack(slotIndex);
            if (newSlotIndex >= 0)
            {
                RefreshSlot(slotIndex);
                RefreshSlot(newSlotIndex);
            }
        }

        // --- Item use (equip / consume) ---

        /// <summary>
        /// Called by InventorySlotUI on right-click. Delegates to Inventory.
        /// </summary>
        public void UseSlot(int slotIndex)
        {
            _inventory.UseSlot(slotIndex);
        }

        /// <summary>
        /// Query: is this slot the currently equipped item?
        /// Used by InventorySlotUI to show the equipped border.
        /// </summary>
        public bool IsSlotEquipped(int slotIndex)
        {
            return _inventory.EquippedSlotIndex == slotIndex;
        }

        /// <summary>
        /// Query: is this slot the currently active hotbar selection?
        /// </summary>
        public bool IsSlotActiveHotbar(int slotIndex)
        {
            return slotIndex == _inventory.ActiveHotbarIndex;
        }

        // =====================================================================
        // Inventory panel toggle
        // =====================================================================

        public void ToggleInventory()
        {
            _inventoryOpen = !_inventoryOpen;
            inventoryPanel.SetActive(_inventoryOpen);

            if (_inventoryOpen)
            {
                RefreshAllSlots();
            }
            else
            {
                HideTooltip();
            }
        }

        public bool IsInventoryOpen => _inventoryOpen;

        // =====================================================================
        // Hotbar
        // =====================================================================

        private void OnHotbarKeyPressed(int index)
        {
            // Valheim-style: pressing the key both selects AND uses the slot
            _inventory.HotbarUse(index);
        }

        private void OnScrollHotbar(float scrollValue)
        {
            int current = _inventory.ActiveHotbarIndex;
            if (scrollValue > 0)
                current = (current - 1 + Inventory.HotbarSize) % Inventory.HotbarSize;
            else
                current = (current + 1) % Inventory.HotbarSize;

            // Scroll just selects, doesn't auto-use
            _inventory.SetActiveHotbar(current);
        }

        private void UpdateHotbarSelection(int activeIndex)
        {
            for (int i = 0; i < _hotbarSlotUIs.Count; i++)
            {
                _hotbarSlotUIs[i].SetHotbarSelected(i == activeIndex);
            }
        }

        private void OnEquippedChanged(int equippedSlotIndex)
        {
            // Refresh all slots to update equipped borders
            for (int i = 0; i < _allSlotUIs.Count; i++)
            {
                _allSlotUIs[i].UpdateEquippedVisual();
            }

            if (equippedSlotIndex >= 0)
            {
                var item = _inventory.Slots[equippedSlotIndex].ItemData;
                Debug.Log($"[Inventory] Equipped: {item.displayName}");
            }
            else
            {
                Debug.Log("[Inventory] Unequipped");
            }
        }

        private void OnItemConsumed(Data.ItemInstance consumed)
        {
            Debug.Log($"[Inventory] Consumed: {consumed.Data.displayName}");
            // TODO: Apply consumable effects (heal, buff, etc.)
        }

        // =====================================================================
        // World drops
        // =====================================================================

        private void SpawnWorldDrop(ItemInstance instance, int quantity)
        {
            if (worldItemPrefab == null || playerTransform == null)
            {
                Debug.LogWarning("[InventoryUI] Cannot spawn world drop — prefab or player transform not assigned.");
                return;
            }

            // Drop slightly in front of the player
            Vector2 dropPos = (Vector2)playerTransform.position
                + (Vector2)(playerTransform.right * dropDistance);

            var go = Instantiate(worldItemPrefab, dropPos, Quaternion.identity);

            // The world item prefab should have a component that accepts an ItemInstance.
            // For now we'll use a simple WorldItem component (you'll implement this).
            var worldItem = go.GetComponent<WorldItem>();
            if (worldItem != null)
            {
                worldItem.Initialise(instance, quantity);
            }
        }

        // =====================================================================
        // Refresh
        // =====================================================================

        private void OnSlotDataChanged(int slotIndex)
        {
            RefreshSlot(slotIndex);
        }

        private void RefreshSlot(int index)
        {
            if (index >= 0 && index < _allSlotUIs.Count)
            {
                _allSlotUIs[index].Refresh();
            }
        }

        private void RefreshAllSlots()
        {
            for (int i = 0; i < _allSlotUIs.Count; i++)
            {
                _allSlotUIs[i].Refresh();
            }
        }
    }
}