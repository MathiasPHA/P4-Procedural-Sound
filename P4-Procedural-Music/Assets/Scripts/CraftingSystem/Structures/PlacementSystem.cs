using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using InventorySystem.Data;
using InventorySystem.Crafting;

namespace InventorySystem.Building
{
    /// <summary>
    /// Handles structure placement via the Build Hammer's recipe-driven flow.
    ///
    /// Flow (recipe-driven — Build Hammer):
    ///   1. BuildHammerStation.Craft() calls BeginPlacementFromRecipe(recipe)
    ///   2. A translucent ghost preview appears at the cursor, snapped to grid
    ///   3. Ghost is green when placement is valid, red when invalid
    ///   4. Left-click places the structure (consumes recipe ingredients)
    ///   5. If the player still has enough materials, stays in placement mode
    ///   6. Right-click or Escape cancels — fires OnPlacementCancelled so the
    ///      build menu can re-open
    ///
    /// The legacy item-driven path (equip a Buildable directly) is deprecated.
    /// All structures now go through the hammer.
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
        [Tooltip("All placeable item definitions. Use right-click → Auto-Populate Placeables " +
                 "to scan the project and fill this list automatically.")]
        [SerializeField] private List<PlaceableData> placeables = new();

        /// <summary>Read-only access to the registered placeables (runtime use).</summary>
        public IReadOnlyList<PlaceableData> Placeables => placeables;

#if UNITY_EDITOR
        /// <summary>
        /// Right-click the PlacementSystem component → Auto-Populate Placeables.
        /// Scans the entire project for every PlaceableData asset and fills the list.
        /// Run this whenever you add a new structure — no manual dragging needed.
        /// </summary>
        [ContextMenu("Auto-Populate Placeables")]
        private void AutoPopulatePlaceables()
        {
            placeables.Clear();

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PlaceableData");

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaceableData>(path);

                if (data != null)
                    placeables.Add(data);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[PlacementSystem] Auto-populated {placeables.Count} PlaceableData asset(s).");
        }
#endif

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

        [Header("Ghost Material")]
        [Tooltip("Assign 'Universal Render Pipeline/2D/Sprite-Unlit-Default' here. " +
                 "Shader.Find fails in builds — this serialized reference is required.")]
        [SerializeField] private Shader ghostShader;

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

        // Recipe-driven placement (Build Hammer)
        private Recipe _activeRecipe;
        private bool _isRecipeDriven;

        // Ghost preview
        private GameObject _ghost;
        private SpriteRenderer[] _ghostRenderers;
        private Vector2 _currentGridPos;
        private bool _currentValid;

        /// <summary>True while the player is in placement mode.</summary>
        public bool IsPlacing => _isPlacing;

        /// <summary>
        /// Fired when the player cancels recipe-driven placement (right-click/Escape).
        /// BuildHammerEquipHandler or CraftingUIManager can listen to re-open the build menu.
        /// </summary>
        public event Action OnPlacementCancelled;

        /// <summary>
        /// Fired when placement finishes because the player ran out of materials.
        /// Allows the build menu to re-open automatically.
        /// </summary>
        public event Action OnPlacementFinished;

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

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
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

            _inventory = InventoryBootstrap.PlayerInventory;

            // NOTE: Legacy Buildable-equip path removed (Path A — all structures go through Build Hammer).
            // OnEquippedChanged subscription is no longer needed for placement.
        }

        private void OnDestroy()
        {
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
        // Recipe-driven placement (Build Hammer)
        // =====================================================================

        /// <summary>
        /// Enter placement mode for a structure defined by a recipe.
        /// Called by BuildHammerStation.Craft().
        /// Ingredients are NOT consumed here — they're consumed on actual placement.
        /// </summary>
        /// <returns>True if placement mode was entered successfully.</returns>
        public bool BeginPlacementFromRecipe(Recipe recipe)
        {
            if (recipe == null || recipe.result == null)
            {
                Debug.LogWarning("[PlacementSystem] BeginPlacementFromRecipe — null recipe or result.");
                return false;
            }

            // Look up the PlaceableData via the recipe's result item
            if (!_lookup.TryGetValue(recipe.result.id, out var placeable))
            {
                Debug.LogWarning($"[PlacementSystem] No PlaceableData found for '{recipe.result.id}'. " +
                                 "Make sure it's in the Placeables list.");
                return false;
            }

            if (placeable.prefab == null)
            {
                Debug.LogWarning($"[PlacementSystem] PlaceableData for '{recipe.result.id}' has no prefab.");
                return false;
            }

            // Clean up any existing placement
            if (_isPlacing)
            {
                DestroyGhost();
                _isPlacing = false;
            }

            _activeRecipe = recipe;
            _isRecipeDriven = true;
            _activePlaceable = placeable;
            _equippedSlotIndex = -1; // Not relevant for recipe-driven

            CreateGhost();
            _isPlacing = true;

            Debug.Log($"[PlacementSystem] Recipe-driven placement started for '{recipe.result.displayName}'");
            return true;
        }

        // =====================================================================
        // Placement mode
        // =====================================================================

        private void ExitPlacement()
        {
            if (!_isPlacing) return;

            _isPlacing = false;
            DestroyGhost();
            _activePlaceable = null;
            _equippedSlotIndex = -1;

            bool wasRecipeDriven = _isRecipeDriven;
            _activeRecipe = null;
            _isRecipeDriven = false;

            // Let listeners know so the build menu can re-open
            if (wasRecipeDriven)
                OnPlacementFinished?.Invoke();
        }

        /// <summary>
        /// Cancel placement. For recipe-driven: don't unequip the hammer,
        /// just exit placement and notify listeners (build menu re-opens).
        /// </summary>
        private void CancelPlacement()
        {
            if (!_isPlacing) return;

            bool wasRecipeDriven = _isRecipeDriven;
            int slotToUnequip = _equippedSlotIndex;

            // Clean up state BEFORE any callbacks
            _isPlacing = false;
            DestroyGhost();
            _activePlaceable = null;
            _equippedSlotIndex = -1;
            _activeRecipe = null;
            _isRecipeDriven = false;

            if (wasRecipeDriven)
            {
                // Hammer stays equipped — fire event so build menu re-opens
                OnPlacementCancelled?.Invoke();
            }
            else
            {
                // Legacy path: unequip the buildable item
                if (slotToUnequip >= 0 && _inventory != null &&
                    _inventory.EquippedSlotIndex == slotToUnequip)
                {
                    _inventory.UseSlot(slotToUnequip);
                }
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
                Destroy(rb);

            // Disable all MonoBehaviours (PlacedStructure, ComfortInfluenceSource, etc.)
            foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;

            // Unlit material so the ghost is always visible regardless of ambient light
            // (URP 2D lighting would otherwise darken it at night just like the real world).
            if (ghostShader == null)
            {
                Debug.LogError("[PlacementSystem] ghostShader is not assigned in the Inspector!");
                ghostShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }
            var unlitMat = new Material(ghostShader);

            // Cache renderers, set sorting order, and apply unlit material
            _ghostRenderers = _ghost.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in _ghostRenderers)
            {
                sr.sortingOrder = ghostSortingOrder;
                sr.material = unlitMat;
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

            // Right-click, Escape, or Tab — cancel placement.
            // Tab cancels AND lets the inventory toggle fire on the same frame,
            // acting as a "back to menu" shortcut.
            bool cancelPressed = mouse.rightButton.wasPressedThisFrame
                || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (keyboard != null && keyboard.tabKey.wasPressedThisFrame);

            if (cancelPressed)
            {
                CancelPlacement();
            }
        }

        // =====================================================================
        // Place
        // =====================================================================

        private void PlaceStructure()
        {
            if (_isRecipeDriven)
            {
                PlaceFromRecipe();
            }
            else
            {
                PlaceFromInventoryItem();
            }
        }

        /// <summary>
        /// Recipe-driven placement: consume recipe ingredients, spawn structure.
        /// </summary>
        private void PlaceFromRecipe()
        {
            if (_activeRecipe == null || _inventory == null) return;

            // Double-check the player can still afford the recipe
            foreach (var req in _activeRecipe.ingredients)
            {
                if (!_inventory.HasItem(req.item.id, req.amount))
                {
                    Debug.LogWarning("[PlacementSystem] Can't afford recipe — ingredients changed mid-placement.");
                    ExitPlacement();
                    return;
                }
            }

            // Spawn the real structure
            var go = Instantiate(
                _activePlaceable.prefab,
                new Vector3(_currentGridPos.x, _currentGridPos.y, 0f),
                Quaternion.identity
            );

            PlayPlacementSound();

            // Tag it with a reference back to its data
            var structure = go.GetComponent<PlacedStructure>();
            if (structure != null)
                structure.sourceData = _activePlaceable;

            // Register with save system
            var structureManager = ProceduralTerrain.PlacedStructureManager.Instance;
            if (structureManager != null && structure != null)
                structureManager.RegisterStructure(structure);

            // Track placement in dungeon for delta save
            if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            {
                DungeonDeltaTracker.RecordPlacement(
                    _activePlaceable.item.id,
                    go.transform.position);
            }

            // Consume recipe ingredients
            foreach (var req in _activeRecipe.ingredients)
            {
                _inventory.RemoveItem(req.item.id, req.amount);
            }

            // If the player can still afford another, stay in placement mode
            bool canAffordAnother = true;
            foreach (var req in _activeRecipe.ingredients)
            {
                if (!_inventory.HasItem(req.item.id, req.amount))
                {
                    canAffordAnother = false;
                    break;
                }
            }

            if (!canAffordAnother)
                ExitPlacement();
        }

        /// <summary>
        /// Legacy item-driven placement: consume one Buildable from inventory.
        /// Kept for backwards compatibility but no longer the primary path.
        /// </summary>
        private void PlaceFromInventoryItem()
        {
            // Spawn the real structure
            var go = Instantiate(
                _activePlaceable.prefab,
                new Vector3(_currentGridPos.x, _currentGridPos.y, 0f),
                Quaternion.identity
            );

            PlayPlacementSound();

            var structure = go.GetComponent<PlacedStructure>();
            if (structure != null)
                structure.sourceData = _activePlaceable;

            var structureManager = ProceduralTerrain.PlacedStructureManager.Instance;
            if (structureManager != null && structure != null)
                structureManager.RegisterStructure(structure);

            if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            {
                DungeonDeltaTracker.RecordPlacement(
                    _activePlaceable.item.id,
                    go.transform.position);
            }

            _inventory.RemoveItem(_activePlaceable.item.id, 1);

            if (_inventory.HasItem(_activePlaceable.item.id, 1))
                return;

            ExitPlacement();
        }

        private void PlayPlacementSound()
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

            Gizmos.color = _currentValid
                ? new Color(0.2f, 1f, 0.2f, 0.3f)
                : new Color(1f, 0.2f, 0.2f, 0.3f);

            Gizmos.DrawWireCube((Vector3)_currentGridPos, (Vector3)footprint);
        }
#endif
    }
}