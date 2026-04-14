using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using InventorySystem.Data;

namespace InventorySystem.Building
{
    /// <summary>
    /// Handles structure placement when the player equips a Buildable item.
    ///
    /// Flow:
    ///   1. Player equips a Buildable from the hotbar
    ///   2. A translucent ghost preview appears at the cursor, snapped to grid
    ///   3. Ghost is green when placement is valid, red when invalid
    ///   4. Left-click places the structure (consumes one from inventory)
    ///   5. If the player has more, stays in placement mode for rapid building
    ///   6. Right-click or Escape cancels and unequips
    ///
    /// Validation checks:
    ///   - Position overlaps valid ground (groundLayer)
    ///   - Position does NOT overlap existing structures (obstacleLayer)
    ///
    /// SETUP:
    ///   1. Attach to the Player GameObject
    ///   2. Create PlaceableData assets for each buildable item
    ///   3. Drag them all into the Placeables list
    ///   4. Configure ground and obstacle layer masks
    ///   5. Set your structure prefabs to the obstacle layer
    /// </summary>
    public class PlacementSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Main camera. Auto-finds Camera.main if left empty.")]
        [SerializeField] private Camera mainCamera;

        [Header("Placeable Database")]
        [Tooltip("All placeable item definitions. The system builds a lookup by item ID.")]
        [SerializeField] private List<PlaceableData> placeables = new();

        [Header("Grid")]
        [Tooltip("World-space size of one grid cell.")]
        [SerializeField] private float gridSize = 1f;

        [Header("Validation")]
        [Tooltip("If true, skip the ground check — placement is valid anywhere without obstacles.")]
        [SerializeField] private bool skipGroundCheck = false;

        [Tooltip("Layer(s) considered valid ground for placement.")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("Layer(s) that block placement (existing structures, walls, trees, etc.)")]
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Preview Colours")]
        [SerializeField] private Color validColour = new Color(0.2f, 1f, 0.2f, 0.5f);
        [SerializeField] private Color invalidColour = new Color(1f, 0.2f, 0.2f, 0.5f);

        [Header("Ghost Sorting")]
        [Tooltip("Sorting order for the placement ghost so it renders above the world.")]
        [SerializeField] private int ghostSortingOrder = 100;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip placementSound;
        [Tooltip("Base volume for the placement sound.")]
        [Range(0f, 1f)]
        [SerializeField] private float placementVolume = 0.5f;

        // =====================================================================
        // Runtime state
        // =====================================================================

        private Dictionary<string, PlaceableData> _lookup = new();
        private Inventory _inventory;

        // Placement state
        private bool _isPlacing;
        private PlaceableData _activePlaceable;
        private int _equippedSlotIndex = -1;

        // Ghost preview
        private GameObject _ghost;
        private SpriteRenderer[] _ghostRenderers;
        private Vector2 _currentGridPos;
        private bool _currentValid;

        /// <summary>True while the player is in placement mode.</summary>
        public bool IsPlacing => _isPlacing;

        public static PlacementSystem Instance { get; private set; }

        /// <summary>
        /// Look up a PlaceableData by item ID. Used by DungeonDeltaTracker
        /// to re-place saved structures.
        /// </summary>
        public PlaceableData GetPlaceableByItemId(string itemId)
        {
            _lookup.TryGetValue(itemId, out var data);
            return data;
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Start()
        {
            Instance = this;

            if (mainCamera == null)
                mainCamera = Camera.main;

            // Build item ID → PlaceableData lookup
            foreach (var p in placeables)
            {
                if (p == null || p.item == null) continue;

                if (_lookup.ContainsKey(p.item.id))
                {
                    Debug.LogWarning($"[PlacementSystem] Duplicate placeable for item '{p.item.id}'.");
                    continue;
                }

                _lookup[p.item.id] = p;
            }

            // Subscribe to inventory (InventoryBootstrap runs at -50, so PlayerInventory exists by now)
            _inventory = InventoryBootstrap.PlayerInventory;

            if (_inventory != null)
            {
                _inventory.OnEquippedChanged += OnEquippedChanged;
            }
            else
            {
                Debug.LogWarning("[PlacementSystem] PlayerInventory not available. " +
                                 "Ensure InventoryBootstrap runs before PlacementSystem (execution order -50).");
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnEquippedChanged -= OnEquippedChanged;

            DestroyGhost();
        }

        private void Update()
        {
            if (!_isPlacing || _ghost == null) return;

            // Don't process placement input when pointer is over UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _ghost.SetActive(false);
                return;
            }

            _ghost.SetActive(true);
            UpdateGhostPosition();
            _currentValid = ValidatePlacement();
            UpdateGhostColour();
            HandleInput();
        }

        // =====================================================================
        // Equip handler — enters/exits placement mode
        // =====================================================================

        private void OnEquippedChanged(int slotIndex)
        {
            // If already placing, clean up the ghost
            if (_isPlacing)
            {
                DestroyGhost();
                _isPlacing = false;
            }

            // Nothing equipped
            if (slotIndex < 0)
            {
                _activePlaceable = null;
                _equippedSlotIndex = -1;
                return;
            }

            var slot = _inventory.Slots[slotIndex];

            // Only activate for buildable items that have placement data
            if (slot.IsEmpty || slot.ItemData.category != ItemCategory.Buildable)
            {
                _activePlaceable = null;
                _equippedSlotIndex = -1;
                return;
            }

            if (_lookup.TryGetValue(slot.ItemData.id, out var data))
            {
                _activePlaceable = data;
                _equippedSlotIndex = slotIndex;
                EnterPlacement();
            }
        }

        // =====================================================================
        // Placement mode
        // =====================================================================

        private void EnterPlacement()
        {
            if (_activePlaceable == null || _activePlaceable.prefab == null) return;

            CreateGhost();
            _isPlacing = true;
        }

        private void ExitPlacement()
        {
            if (!_isPlacing) return;

            _isPlacing = false;
            DestroyGhost();
            _activePlaceable = null;
            _equippedSlotIndex = -1;
        }

        /// <summary>
        /// Cancel placement and unequip the buildable item.
        /// </summary>
        private void CancelPlacement()
        {
            if (!_isPlacing) return;

            int slotToUnequip = _equippedSlotIndex;

            // Set _isPlacing false BEFORE calling UseSlot to prevent
            // OnEquippedChanged from double-cleaning-up
            _isPlacing = false;
            DestroyGhost();
            _activePlaceable = null;
            _equippedSlotIndex = -1;

            // Unequip the item (UseSlot toggles equip off for Buildables)
            if (slotToUnequip >= 0 && _inventory.EquippedSlotIndex == slotToUnequip)
            {
                _inventory.UseSlot(slotToUnequip);
            }
        }

        // =====================================================================
        // Ghost preview
        // =====================================================================

        private void CreateGhost()
        {
            _ghost = Instantiate(_activePlaceable.prefab);
            _ghost.name = "PlacementGhost";

            // Disable all gameplay components so the ghost is purely visual
            foreach (var col in _ghost.GetComponentsInChildren<Collider2D>())
                col.enabled = false;

            foreach (var rb in _ghost.GetComponentsInChildren<Rigidbody2D>())
                Object.Destroy(rb);

            // Disable all MonoBehaviours (PlacedStructure, ComfortInfluenceSource, etc.)
            foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;

            // Cache renderers and set sorting order
            _ghostRenderers = _ghost.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in _ghostRenderers)
            {
                sr.sortingOrder = ghostSortingOrder;
            }
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
                _ghostRenderers = null;
            }
        }

        private void UpdateGhostPosition()
        {
            if (mainCamera == null || Mouse.current == null) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(mouseScreen);

            // Snap to grid
            float x = Mathf.Round(mouseWorld.x / gridSize) * gridSize;
            float y = Mathf.Round(mouseWorld.y / gridSize) * gridSize;
            _currentGridPos = new Vector2(x, y);

            _ghost.transform.position = new Vector3(_currentGridPos.x, _currentGridPos.y, 0f);
        }

        private void UpdateGhostColour()
        {
            if (_ghostRenderers == null) return;

            Color tint = _currentValid ? validColour : invalidColour;

            foreach (var sr in _ghostRenderers)
            {
                sr.color = tint;
            }
        }

        // =====================================================================
        // Validation
        // =====================================================================

        private bool ValidatePlacement()
        {
            if (_activePlaceable == null) return false;

            // Calculate the footprint in world units
            Vector2 footprint = new Vector2(
                _activePlaceable.gridCells.x * gridSize,
                _activePlaceable.gridCells.y * gridSize
            );

            // Shrink slightly to avoid false positives from exactly-touching neighbours
            Vector2 checkSize = footprint * 0.95f;

            // Must be on valid ground (unless skipped)
            bool onGround = skipGroundCheck || Physics2D.OverlapBox(
                _currentGridPos, checkSize, 0f, groundLayer) != null;

            // Must not overlap existing structures or obstacles
            bool hasObstacle = Physics2D.OverlapBox(
                _currentGridPos, checkSize, 0f, obstacleLayer) != null;

            return onGround && !hasObstacle;
        }

        // =====================================================================
        // Input
        // =====================================================================

        private void HandleInput()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse == null) return;

            // Left-click — place structure
            if (mouse.leftButton.wasPressedThisFrame && _currentValid)
            {
                PlaceStructure();
                return;
            }

            // Right-click or Escape — cancel placement
            if (mouse.rightButton.wasPressedThisFrame ||
                (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            {
                CancelPlacement();
            }
        }

        // =====================================================================
        // Place
        // =====================================================================

        private void PlaceStructure()
        {
            // Spawn the real structure
            var go = Instantiate(
                _activePlaceable.prefab,
                new Vector3(_currentGridPos.x, _currentGridPos.y, 0f),
                Quaternion.identity
            );

            // Play placement audio
            PlayPLacementSound();

            // Tag it with a reference back to its data
            var structure = go.GetComponent<PlacedStructure>();
            if (structure != null)
            {
                structure.sourceData = _activePlaceable;
            }

            // Register with save system
            var structureManager = ProceduralTerrain.PlacedStructureManager.Instance;
            if (structureManager != null && structure != null)
            {
                structureManager.RegisterStructure(structure);
            }

            // Track placement in dungeon for delta save
            if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            {
                DungeonDeltaTracker.RecordPlacement(
                    _activePlaceable.item.id,
                    go.transform.position);
            }

            // Consume one from inventory
            _inventory.RemoveItem(_activePlaceable.item.id, 1);

            // If the player has more, stay in placement mode for rapid building
            if (_inventory.HasItem(_activePlaceable.item.id, 1))
            {
                // Keep placing
                return;
            }

            // Out of items — exit placement mode
            ExitPlacement();
        }

        private void PlayPLacementSound()
        {
            if (audioSource != null && placementSound != null)
            {
                audioSource.PlayOneShot(placementSound, placementVolume);
            }
        }

        // =====================================================================
        // Editor gizmos
        // =====================================================================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_isPlacing) return;

            Vector2 footprint = new Vector2(
                (_activePlaceable?.gridCells.x ?? 1) * gridSize,
                (_activePlaceable?.gridCells.y ?? 1) * gridSize
            );

            // Draw placement footprint
            Gizmos.color = _currentValid
                ? new Color(0.2f, 1f, 0.2f, 0.3f)
                : new Color(1f, 0.2f, 0.2f, 0.3f);

            Gizmos.DrawWireCube((Vector3)_currentGridPos, (Vector3)footprint);
        }
#endif
    }
}