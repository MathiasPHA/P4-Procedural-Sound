using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InventorySystem.Input
{
    /// <summary>
    /// Centralised access point for all inventory/UI input actions.
    /// 
    /// Uses the PlayerInput component's runtime clone (which is guaranteed
    /// to be bound to devices). Resolves in Start() so that PlayerInput
    /// has finished its Awake/OnEnable initialization first.
    /// 
    /// Execution order -100 ensures this resolves before anything that
    /// subscribes to its events (InventoryBootstrap runs at -50).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InventoryInputProvider : MonoBehaviour
    {
        [Header("Player Reference")]
        [Tooltip("REQUIRED — Drag the Player GameObject here. Must have a PlayerInput component.")]
        [SerializeField] private PlayerInput playerInput;

        // --- Resolved at Start ---
        private bool _initialized;
        private InputActionMap _uiMap;
        private InputAction _point;
        private InputAction _click;
        private InputAction _rightClick;
        private InputAction _middleClick;
        private InputAction _modifier;
        private InputAction _ctrlModifier;
        private InputAction _toggleInventory;
        private InputAction _hotbarSelect;
        private InputAction _scrollHotbar;

        // =====================================================================
        // Public properties
        // =====================================================================

        public Vector2 PointerPosition => _point?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool IsModifierHeld => _modifier?.IsPressed() ?? false;
        public bool IsCtrlHeld => _ctrlModifier?.IsPressed() ?? false;
        public bool IsInitialized => _initialized;

        // =====================================================================
        // Public events
        // =====================================================================

        public event Action OnToggleInventory;
        public event Action OnClick;
        public event Action OnRightClick;
        public event Action OnMiddleClick;
        public event Action<int> OnHotbarSelect;
        public event Action<float> OnScrollHotbar;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Start()
        {
            // Start() runs after ALL Awake() and OnEnable() calls across
            // all GameObjects. This guarantees PlayerInput has created its
            // clone and bound it to devices before we try to read from it.
            Initialize();
        }

        private void Initialize()
        {
            // --- Validate PlayerInput ---
            if (playerInput == null)
            {
                Debug.LogError(
                    "[InventoryInputProvider] PlayerInput is NOT assigned! " +
                    "Drag the Player GameObject into the 'Player Input' field.");
                return;
            }

            if (playerInput.actions == null)
            {
                Debug.LogError(
                    "[InventoryInputProvider] PlayerInput.actions is null! " +
                    "Make sure the Player's PlayerInput component has an Actions asset assigned.");
                return;
            }

            // --- Resolve UI map from PlayerInput's clone ---
            var actions = playerInput.actions;
            _uiMap = actions.FindActionMap("UI");

            if (_uiMap == null)
            {
                Debug.LogError(
                    "[InventoryInputProvider] No 'UI' action map found! " +
                    "Open your PlayerInput.inputactions and add a map named exactly 'UI'.");
                return;
            }

            // --- Resolve individual actions ---
            // Each call logs clearly if an action is missing
            _point           = ResolveAction("Mouse Position");
            _click           = ResolveAction("Left Button");
            _rightClick      = ResolveAction("RightButton");
            _middleClick     = ResolveAction("MiddleButton");
            _modifier        = ResolveAction("Modifier");
            _ctrlModifier    = ResolveAction("Ctrl Modifier");
            _toggleInventory = ResolveAction("Toggle Inventory");
            _hotbarSelect    = ResolveAction("HotbarSelect");
            _scrollHotbar    = ResolveAction("ScrollHotbar");

            // --- Enable the UI map on the clone ---
            _uiMap.Enable();

            // --- Subscribe to callbacks ---
            Subscribe(_toggleInventory, HandleToggleInventory);
            Subscribe(_click, HandleClick);
            Subscribe(_rightClick, HandleRightClick);
            Subscribe(_middleClick, HandleMiddleClick);
            Subscribe(_hotbarSelect, HandleHotbarSelect);
            Subscribe(_scrollHotbar, HandleScrollHotbar);

            _initialized = true;
            Debug.Log("[InventoryInputProvider] Initialized successfully.");
        }

        private void OnDestroy()
        {
            // Unsubscribe from everything
            Unsubscribe(_toggleInventory, HandleToggleInventory);
            Unsubscribe(_click, HandleClick);
            Unsubscribe(_rightClick, HandleRightClick);
            Unsubscribe(_middleClick, HandleMiddleClick);
            Unsubscribe(_hotbarSelect, HandleHotbarSelect);
            Unsubscribe(_scrollHotbar, HandleScrollHotbar);
        }

        // =====================================================================
        // Action resolution helpers
        // =====================================================================

        private InputAction ResolveAction(string actionName)
        {
            var action = _uiMap.FindAction(actionName);
            if (action == null)
            {
                Debug.LogWarning(
                    $"[InventoryInputProvider] Action '{actionName}' not found in the UI map. " +
                    "Check that the name in your .inputactions asset matches EXACTLY " +
                    "(including spaces and capitalization).");
            }
            return action;
        }

        private void Subscribe(InputAction action, Action<InputAction.CallbackContext> handler)
        {
            if (action != null)
                action.performed += handler;
        }

        private void Unsubscribe(InputAction action, Action<InputAction.CallbackContext> handler)
        {
            if (action != null)
                action.performed -= handler;
        }

        // =====================================================================
        // Handlers
        // =====================================================================

        private void HandleToggleInventory(InputAction.CallbackContext ctx) => OnToggleInventory?.Invoke();
        private void HandleClick(InputAction.CallbackContext ctx) => OnClick?.Invoke();
        private void HandleRightClick(InputAction.CallbackContext ctx) => OnRightClick?.Invoke();
        private void HandleMiddleClick(InputAction.CallbackContext ctx) => OnMiddleClick?.Invoke();

        private void HandleHotbarSelect(InputAction.CallbackContext ctx)
        {
            int index = Mathf.RoundToInt(ctx.ReadValue<float>());
            OnHotbarSelect?.Invoke(index);
        }

        private void HandleScrollHotbar(InputAction.CallbackContext ctx)
        {
            var scroll = ctx.ReadValue<Vector2>();
            if (Mathf.Abs(scroll.y) > 0.01f)
            {
                OnScrollHotbar?.Invoke(scroll.y);
            }
        }

        // =====================================================================
        // Movement map control
        // =====================================================================

        public void DisableMovement()
        {
            if (playerInput != null && playerInput.actions != null)
            {
                playerInput.actions.FindActionMap("Movement")?.Disable();
            }
        }

        public void EnableMovement()
        {
            if (playerInput != null && playerInput.actions != null)
            {
                playerInput.actions.FindActionMap("Movement")?.Enable();
            }
        }
    }
}