using System;
using System.Collections.Generic;
using UnityEngine;
using InventorySystem.Data;
using InventorySystem.UI;
using ProceduralTerrain;

namespace InteractionSystem
{
    /// <summary>
    /// A placeable chest with its own internal Inventory.
    /// Swaps between open/closed sprites on interact and opens/closes
    /// the ChestUIPanel to let the player move items in and out.
    ///
    /// Implements IPersistentStructureState so PlacedStructureManager
    /// automatically saves and restores its contents.
    ///
    /// SETUP:
    ///   1. Add this component to the chest prefab root
    ///   2. Assign closedSprite and openSprite in the Inspector
    ///   3. Assign the spriteRenderer field (or leave null — Awake will find it)
    ///   4. Assign chestUIPanel (the ChestUIPanel in the scene)
    ///   5. Assign itemDatabase (the shared ItemDatabase ScriptableObject)
    ///   6. Ensure the prefab has a Collider2D on the "Interactable" layer
    ///   7. Set actionVerb to "Open" on the base Interactable header
    /// </summary>
    public class ChestInteractable : Interactable, IPersistentStructureState
    {
        // =====================================================================
        // Inspector
        // =====================================================================

        [Header("Chest Visuals")]
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openSprite;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Chest Inventory")]
        [Tooltip("How many slots this chest has.")]
        [SerializeField] private int slotCount = 16;

        [Header("References")]
        [SerializeField] private ChestUIPanel chestUIPanel;
        [SerializeField] private ItemDatabase itemDatabase;

        // =====================================================================
        // Runtime state
        // =====================================================================

        private Inventory _inventory;
        private bool _isOpen;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            _inventory = new Inventory(slotCount);

            ApplySprite();
            SetActionVerb("Open");
        }

        private void OnDestroy()
        {
            // If the chest was open when destroyed (e.g. scene unload), close the UI
            if (_isOpen && chestUIPanel != null)
                chestUIPanel.Close();
        }

        // =====================================================================
        // Interactable overrides
        // =====================================================================

        public override bool CanInteract() => chestUIPanel != null;

        public override void Interact(PlayerStateManager player)
        {
            if (chestUIPanel == null)
            {
                Debug.LogError("[ChestInteractable] chestUIPanel is not assigned!", this);
                return;
            }

            if (_isOpen)
                CloseChest();
            else
                OpenChest();
        }

        // =====================================================================
        // Open / Close
        // =====================================================================

        private void OpenChest()
        {
            _isOpen = true;
            ApplySprite();
            SetActionVerb("Close");
            chestUIPanel.Open(_inventory, this);
        }

        /// <summary>
        /// Called by ChestUIPanel when the player closes the UI from the button,
        /// OR by Interact() when the player clicks an already-open chest.
        /// </summary>
        public void CloseChest()
        {
            _isOpen = false;
            ApplySprite();
            SetActionVerb("Open");

            // Only tell the panel to close if it's currently showing this chest.
            // This avoids a loop if the panel itself called us first.
            if (chestUIPanel != null && chestUIPanel.IsShowingChest(this))
                chestUIPanel.Close();
        }

        // =====================================================================
        // Visuals
        // =====================================================================

        private void ApplySprite()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.sprite = _isOpen ? openSprite : closedSprite;
        }

        // =====================================================================
        // IPersistentStructureState — save / load
        // =====================================================================

        public string SerializeState()
        {
            var saveData = new ChestSaveData();
            saveData.slots = new List<ChestSlotSaveEntry>(_inventory.SlotCount);

            for (int i = 0; i < _inventory.SlotCount; i++)
            {
                var slot = _inventory.Slots[i];
                if (slot.IsEmpty) continue;

                saveData.slots.Add(new ChestSlotSaveEntry
                {
                    slotIndex = i,
                    itemId    = slot.ItemData.id,
                    quantity  = slot.Quantity
                });
            }

            return JsonUtility.ToJson(saveData);
        }

        public void DeserializeState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            if (itemDatabase == null)
            {
                Debug.LogError("[ChestInteractable] ItemDatabase is not assigned — cannot load chest contents.", this);
                return;
            }

            ChestSaveData saveData;
            try
            {
                saveData = JsonUtility.FromJson<ChestSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ChestInteractable] Failed to parse save data: {e.Message}", this);
                return;
            }

            if (saveData?.slots == null) return;

            foreach (var entry in saveData.slots)
            {
                if (entry.slotIndex < 0 || entry.slotIndex >= _inventory.SlotCount) continue;

                var itemData = itemDatabase.GetById(entry.itemId);
                if (itemData == null)
                {
                    Debug.LogWarning($"[ChestInteractable] Unknown item id '{entry.itemId}' — skipping slot {entry.slotIndex}.");
                    continue;
                }

                var instance = new ItemInstance(itemData);
                _inventory.Slots[entry.slotIndex].Set(instance, entry.quantity);
            }
        }

        // =====================================================================
        // Save data structures
        // =====================================================================

        [Serializable]
        private class ChestSaveData
        {
            public List<ChestSlotSaveEntry> slots = new();
        }

        [Serializable]
        private class ChestSlotSaveEntry
        {
            public int    slotIndex;
            public string itemId;
            public int    quantity;
        }
    }
}