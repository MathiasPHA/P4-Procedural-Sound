using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InventorySystem.Input
{
    /// <summary>
    /// Centralised access point for all inventory/UI input actions.
    /// Lives on a persistent GameObject (e.g. GameManager or InputManager).
    /// 
    /// Other scripts grab a reference to this and subscribe to the C# events.
    /// Nobody else touches the InputActionAsset directly.
    /// 
    /// IMPORTANT: Make sure your EventSystem uses InputSystemUIInputModule,
    /// not the legacy StandaloneInputModule. Without this, Unity's built-in
    /// pointer/drag interfaces (IPointerEnterHandler, IDragHandler, etc.)
    /// won't receive events from the new Input System.
    /// </summary>
    public class InventoryInputProvider : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        // --- Cached action references ---
        private InputAction _point;
        private InputAction _click;
        private InputAction _rightClick;
        private InputAction _middleClick;
        private InputAction _modifier;
        private InputAction _toggleInventory;
        private InputAction _hotbarSelect;
        private InputAction _scrollHotbar;
        private InputAction _interact;
        private InputAction _dropItem;

        // =====================================================================
        // Public events — subscribe to these from UI and gameplay scripts
        // =====================================================================

        /// <summary>Current mouse position (read via PointerPosition property).</summary>
        public Vector2 PointerPosition => _point?.ReadValue<Vector2>() ?? Vector2.zero;

        /// <summary>Whether the shift modifier is currently held.</summary>
        public bool IsModifierHeld => _modifier?.IsPressed() ?? false;

        // Button events
        public event Action OnClick;
        public event Action OnRightClick;
        public event Action OnMiddleClick;
        public event Action OnToggleInventory;
        public event Action OnDropItem;
        public event Action OnInteract;

        /// <summary>
        /// Fires when a hotbar key (1-6) is pressed.
        /// The int parameter is the 0-based hotbar index.
        /// </summary>
        public event Action<int> OnHotbarSelect;

        /// <summary>
        /// Fires when the scroll wheel moves. Positive = scroll up, negative = scroll down.
        /// </summary>
        public event Action<float> OnScrollHotbar;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("[InventoryInputProvider] No InputActionAsset assigned!");
                return;
            }

            // Resolve actions from the asset
            var uiMap = inputActions.FindActionMap("UI", throwIfNotFound: true);
            var gameplayMap = inputActions.FindActionMap("Gameplay", throwIfNotFound: true);

            _point          = uiMap.FindAction("Point");
            _click          = uiMap.FindAction("Click");
            _rightClick     = uiMap.FindAction("RightClick");
            _middleClick    = uiMap.FindAction("MiddleClick");
            _modifier       = uiMap.FindAction("Modifier");
            _toggleInventory = uiMap.FindAction("ToggleInventory");
            _hotbarSelect   = uiMap.FindAction("HotbarSelect");
            _scrollHotbar   = uiMap.FindAction("ScrollHotbar");
            _interact       = gameplayMap.FindAction("Interact");
            _dropItem       = gameplayMap.FindAction("DropItem");
        }

        private void OnEnable()
        {
            inputActions?.Enable();

            _click.performed          += HandleClick;
            _rightClick.performed     += HandleRightClick;
            _middleClick.performed    += HandleMiddleClick;
            _toggleInventory.performed += HandleToggleInventory;
            _hotbarSelect.performed   += HandleHotbarSelect;
            _scrollHotbar.performed   += HandleScrollHotbar;
            _interact.performed       += HandleInteract;
            _dropItem.performed       += HandleDropItem;
        }

        private void OnDisable()
        {
            _click.performed          -= HandleClick;
            _rightClick.performed     -= HandleRightClick;
            _middleClick.performed    -= HandleMiddleClick;
            _toggleInventory.performed -= HandleToggleInventory;
            _hotbarSelect.performed   -= HandleHotbarSelect;
            _scrollHotbar.performed   -= HandleScrollHotbar;
            _interact.performed       -= HandleInteract;
            _dropItem.performed       -= HandleDropItem;

            inputActions?.Disable();
        }

        // =====================================================================
        // Handlers — translate InputAction callbacks into clean C# events
        // =====================================================================

        private void HandleClick(InputAction.CallbackContext ctx) => OnClick?.Invoke();
        private void HandleRightClick(InputAction.CallbackContext ctx) => OnRightClick?.Invoke();
        private void HandleMiddleClick(InputAction.CallbackContext ctx) => OnMiddleClick?.Invoke();
        private void HandleToggleInventory(InputAction.CallbackContext ctx) => OnToggleInventory?.Invoke();
        private void HandleInteract(InputAction.CallbackContext ctx) => OnInteract?.Invoke();
        private void HandleDropItem(InputAction.CallbackContext ctx) => OnDropItem?.Invoke();

        private void HandleHotbarSelect(InputAction.CallbackContext ctx)
        {
            // The Scale processor on each binding encodes the slot index (0-5)
            int index = Mathf.RoundToInt(ctx.ReadValue<float>());
            OnHotbarSelect?.Invoke(index);
        }

        private void HandleScrollHotbar(InputAction.CallbackContext ctx)
        {
            float value = ctx.ReadValue<float>();
            if (Mathf.Abs(value) > 0.01f)
            {
                OnScrollHotbar?.Invoke(value);
            }
        }

        // =====================================================================
        // Action map switching
        // =====================================================================

        /// <summary>
        /// Call when opening a full-screen menu that should block gameplay input.
        /// </summary>
        public void EnableUIOnly()
        {
            inputActions.FindActionMap("Gameplay")?.Disable();
            inputActions.FindActionMap("UI")?.Enable();
        }

        /// <summary>
        /// Call when closing menus to restore gameplay input.
        /// </summary>
        public void EnableAll()
        {
            inputActions.FindActionMap("Gameplay")?.Enable();
            inputActions.FindActionMap("UI")?.Enable();
        }
    }
}
