using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.Data;
using InteractionSystem;

namespace InventorySystem.UI
{
    /// <summary>
    /// Screen-space UI panel that displays a chest's internal Inventory.
    /// Spawns slot UIs dynamically at Open() and supports click-to-transfer
    /// between the chest and the player's inventory.
    ///
    /// SETUP:
    ///   1. Create a Canvas (Screen Space – Overlay) with a Panel child called "ChestPanel"
    ///   2. Inside the panel add:
    ///        - A TextMeshProUGUI named "TitleText"  (e.g. "Chest")
    ///        - A GridLayoutGroup child named "SlotGrid" to hold slot UIs
    ///        - A Button named "CloseButton"
    ///   3. Attach this script to ChestPanel
    ///   4. Assign all serialized fields in the Inspector
    ///   5. Assign the slotPrefab — use the SAME prefab as your player inventory slots
    ///   6. Set the panel inactive in the scene — Open() activates it
    ///
    /// INTERACTION:
    ///   Left-click a chest slot  → transfers that stack to the player inventory (overflow stays)
    ///   Left-click a player slot → transfers that stack to the chest (overflow stays)
    ///   Close button / ESC       → closes the panel and calls chest.CloseChest()
    ///
    /// PATTERN NOTE:
    ///   ChestUIPanel does NOT subclass InventoryUIManager — it intentionally keeps
    ///   things simple. Drag-and-drop between the two inventories is left as a future
    ///   extension; click-to-transfer covers the common case cleanly.
    /// </summary>
    public class ChestUIPanel : MonoBehaviour
    {
        // =====================================================================
        // Inspector
        // =====================================================================

        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform chestSlotContainer;
        [SerializeField] private Transform playerSlotContainer;
        [SerializeField] private Button closeButton;

        [Header("Prefabs")]
        [Tooltip("The same slot prefab used by the player's InventoryUIManager.")]
        [SerializeField] private GameObject slotPrefab;

        [Header("Labels")]
        [SerializeField] private string panelTitle = "Chest";

        // =====================================================================
        // Runtime state
        // =====================================================================

        private Inventory _chestInventory;
        private Inventory _playerInventory;
        private ChestInteractable _currentChest;

        private readonly List<ChestSlotUI> _chestSlotUIs  = new();
        private readonly List<ChestSlotUI> _playerSlotUIs = new();

        private bool _isOpen;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        private void Update()
        {
            // Allow ESC to close the panel
            if (_isOpen && UnityEngine.InputSystem.Keyboard.current != null
                        && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnCloseButtonClicked();
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        // =====================================================================
        // Public API — called by ChestInteractable
        // =====================================================================

        /// <summary>
        /// Opens the panel and binds it to the given chest inventory.
        /// Also shows the player's own inventory alongside it.
        /// </summary>
        public void Open(Inventory chestInventory, ChestInteractable chest)
        {
            _chestInventory  = chestInventory;
            _currentChest    = chest;
            _playerInventory = InventoryBootstrap.PlayerInventory;

            if (titleText != null)
                titleText.text = panelTitle;

            BuildSlotUIs(_chestSlotUIs,  chestSlotContainer,  _chestInventory,  isChest: true);
            BuildSlotUIs(_playerSlotUIs, playerSlotContainer, _playerInventory, isChest: false);

            RefreshAllSlots();

            if (panelRoot != null)
                panelRoot.SetActive(true);

            _isOpen = true;

            // Subscribe to inventory change events so the UI stays in sync
            if (_chestInventory  != null) _chestInventory.OnInventoryChanged  += OnChestInventoryChanged;
            if (_playerInventory != null) _playerInventory.OnInventoryChanged += OnPlayerInventoryChanged;
        }

        /// <summary>
        /// Closes the panel and clears slot UIs. Called by the close button,
        /// ESC, or ChestInteractable.CloseChest().
        /// </summary>
        public void Close()
        {
            if (_chestInventory  != null) _chestInventory.OnInventoryChanged  -= OnChestInventoryChanged;
            if (_playerInventory != null) _playerInventory.OnInventoryChanged -= OnPlayerInventoryChanged;

            ClearSlotUIs(_chestSlotUIs,  chestSlotContainer);
            ClearSlotUIs(_playerSlotUIs, playerSlotContainer);

            if (panelRoot != null)
                panelRoot.SetActive(false);

            _isOpen         = false;
            _currentChest   = null;
            _chestInventory = null;
        }

        /// <summary>Returns true if this panel is currently showing the given chest.</summary>
        public bool IsShowingChest(ChestInteractable chest) =>
            _isOpen && _currentChest == chest;

        // =====================================================================
        // Slot UI construction
        // =====================================================================

        private void BuildSlotUIs(List<ChestSlotUI> uiList, Transform container,
                                  Inventory inventory, bool isChest)
        {
            ClearSlotUIs(uiList, container);

            if (inventory == null || container == null || slotPrefab == null) return;

            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var go   = Instantiate(slotPrefab, container);
                var slot = go.GetComponent<ChestSlotUI>();

                if (slot == null)
                {
                    // If the prefab doesn't already have ChestSlotUI, add it at runtime
                    slot = go.AddComponent<ChestSlotUI>();
                }

                int capturedIndex = i;
                slot.Initialise(i, inventory, onClick: () => OnSlotClicked(capturedIndex, isChest));
                uiList.Add(slot);
            }
        }

        private void ClearSlotUIs(List<ChestSlotUI> uiList, Transform container)
        {
            foreach (var slot in uiList)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            uiList.Clear();
        }

        // =====================================================================
        // Click-to-transfer
        // =====================================================================

        private void OnSlotClicked(int slotIndex, bool fromChest)
        {
            if (_chestInventory == null || _playerInventory == null) return;

            Inventory source      = fromChest ? _chestInventory  : _playerInventory;
            Inventory destination = fromChest ? _playerInventory : _chestInventory;

            var sourceSlot = source.Slots[slotIndex];
            if (sourceSlot.IsEmpty) return;

            ItemData itemData  = sourceSlot.ItemData;
            int      quantity  = sourceSlot.Quantity;

            // Try to add to destination
            int overflow = destination.AddItem(itemData, quantity);
            int transferred = quantity - overflow;

            if (transferred <= 0) return;

            // Remove transferred amount from source
            sourceSlot.RemoveFromStack(transferred);
            source.NotifySlotChanged(slotIndex);
            source.NotifyChanged();
        }

        // =====================================================================
        // Refresh
        // =====================================================================

        private void RefreshAllSlots()
        {
            RefreshSlotList(_chestSlotUIs,  _chestInventory);
            RefreshSlotList(_playerSlotUIs, _playerInventory);
        }

        private void RefreshSlotList(List<ChestSlotUI> uiList, Inventory inventory)
        {
            if (inventory == null) return;
            for (int i = 0; i < uiList.Count && i < inventory.SlotCount; i++)
                uiList[i].Refresh(inventory.Slots[i]);
        }

        private void OnChestInventoryChanged()  => RefreshSlotList(_chestSlotUIs,  _chestInventory);
        private void OnPlayerInventoryChanged() => RefreshSlotList(_playerSlotUIs, _playerInventory);

        // =====================================================================
        // Close button
        // =====================================================================

        private void OnCloseButtonClicked()
        {
            var chest = _currentChest;
            Close();                // closes UI first
            chest?.CloseChest();    // then tells the chest to update its sprite / verb
        }
    }
}