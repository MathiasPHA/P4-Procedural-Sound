using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InventorySystem.Input
{
    [DefaultExecutionOrder(-100)]
    public class InventoryInputProvider : MonoBehaviour
    {
        [Header("Player Reference")]
        [Tooltip("REQUIRED — Drag the Player GameObject here. Must have a PlayerInput component.")]
        [SerializeField] private PlayerInput playerInput;

        [Header("Action Map Names")]
        [Tooltip("Map containing Toggle Inventory, Click, Hotbar, Scroll, Modifiers.")]
        [SerializeField] private string uiMapName = "UI";

        [Tooltip("Map containing movement actions — individual actions here are " +
                 "disabled while inventory is open, but the map itself stays enabled.")]
        [SerializeField] private string playerMapName = "Player";

        [Header("Movement Actions to Disable on Inventory Open")]
        [Tooltip("Exact names (case-sensitive) of actions in the Player map to disable while inventory is open.")]
        [SerializeField] private string[] movementActionNames = { "Move", "Attack", "UseItem", "Interact" };

        private bool _initialized;
        private InputActionMap _uiMap;
        private InputActionMap _playerMap;

        private InputAction _point;
        private InputAction _click;
        private InputAction _rightClick;
        private InputAction _middleClick;
        private InputAction _modifier;
        private InputAction _ctrlModifier;
        private InputAction _toggleInventory;
        private InputAction _hotbarSelect;
        private InputAction _scrollHotbar;

        public Vector2 PointerPosition => _point?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool IsModifierHeld => _modifier?.IsPressed() ?? false;
        public bool IsCtrlHeld => _ctrlModifier?.IsPressed() ?? false;
        public bool IsInitialized => _initialized;

        public event Action OnToggleInventory;
        public event Action OnClick;
        public event Action OnRightClick;
        public event Action OnMiddleClick;
        public event Action<int> OnHotbarSelect;
        public event Action<float> OnScrollHotbar;

        private void Start() => Initialize();

        private void Initialize()
        {
            if (playerInput == null)
            {
                Debug.LogError("[InventoryInputProvider] PlayerInput is NOT assigned!");
                return;
            }
            if (playerInput.actions == null)
            {
                Debug.LogError("[InventoryInputProvider] PlayerInput.actions is null.");
                return;
            }

            var actions = playerInput.actions;

            // Resolve and force-enable the UI map.
            // PlayerInput's defaultActionMap is "Player", which means Unity will
            // disable all other maps on startup — including "UI". We re-enable it
            // here explicitly so Toggle Inventory is always listening.
            _uiMap = actions.FindActionMap(uiMapName);
            if (_uiMap == null)
            {
                Debug.LogError($"[InventoryInputProvider] Action map '{uiMapName}' not found.");
                return;
            }
            _uiMap.Enable();

            // Resolve Player map (used only for DisableMovement/EnableMovement).
            _playerMap = actions.FindActionMap(playerMapName);
            if (_playerMap == null)
                Debug.LogWarning($"[InventoryInputProvider] Player map '{playerMapName}' not found — movement disable will have no effect.");

            // Resolve actions from the UI map.
            _point = Resolve("Point", false);
            _click = Resolve("Click", false);
            _rightClick = Resolve("RightClick", false);
            _middleClick = Resolve("MiddleClick", false);
            _modifier = Resolve("Modifier", false);
            _ctrlModifier = Resolve("Ctrl Modifier", false);
            _toggleInventory = Resolve("Toggle Inventory", true);
            _hotbarSelect = Resolve("HotbarSelect", false);
            _scrollHotbar = Resolve("ScrollHotbar", false);

            Sub(_toggleInventory, HandleToggleInventory);
            Sub(_click, HandleClick);
            Sub(_rightClick, HandleRightClick);
            Sub(_middleClick, HandleMiddleClick);
            Sub(_hotbarSelect, HandleHotbarSelect);
            Sub(_scrollHotbar, HandleScrollHotbar);

            _initialized = true;
            Debug.Log($"[InventoryInputProvider] Initialized. UI map: '{_uiMap.name}', Player map: '{_playerMap?.name ?? "NOT FOUND"}'.");
        }

        private void OnDestroy()
        {
            Unsub(_toggleInventory, HandleToggleInventory);
            Unsub(_click, HandleClick);
            Unsub(_rightClick, HandleRightClick);
            Unsub(_middleClick, HandleMiddleClick);
            Unsub(_hotbarSelect, HandleHotbarSelect);
            Unsub(_scrollHotbar, HandleScrollHotbar);
        }

        private InputAction Resolve(string name, bool required)
        {
            var a = _uiMap.FindAction(name);
            if (a == null)
            {
                if (required) Debug.LogError($"[InventoryInputProvider] REQUIRED action '{name}' not found in map '{_uiMap.name}'.");
                else Debug.LogWarning($"[InventoryInputProvider] Optional action '{name}' not found in map '{_uiMap.name}'.");
            }
            return a;
        }

        private void Sub(InputAction a, Action<InputAction.CallbackContext> h) { if (a != null) a.performed += h; }
        private void Unsub(InputAction a, Action<InputAction.CallbackContext> h) { if (a != null) a.performed -= h; }

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
                OnScrollHotbar?.Invoke(scroll.y);
        }

        // Disables/enables individual Player-map actions only.
        // The UI map stays enabled so Toggle Inventory keeps firing while inventory is open.
        public void DisableMovement()
        {
            if (_playerMap == null) return;
            foreach (var name in movementActionNames)
                _playerMap.FindAction(name)?.Disable();
        }

        public void EnableMovement()
        {
            if (_playerMap == null) return;
            foreach (var name in movementActionNames)
                _playerMap.FindAction(name)?.Enable();
        }
    }
}