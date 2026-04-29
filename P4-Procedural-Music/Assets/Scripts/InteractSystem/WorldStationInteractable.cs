using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Universal Interactable for placed world structures. Replaces one-off
    /// Interactable subclasses (CookingStationInteractable, CampfireInteractable, …).
    ///
    /// On Interact(), runs every <see cref="IWorldInteractAction"/> component on
    /// this GameObject. Behaviors are pure composition — add or remove action
    /// components in the Inspector without touching any code.
    ///
    /// COMMON RECIPES:
    ///   Cooking pot / Workbench / Forge:
    ///     WorldStationInteractable + OpenCraftingUIAction
    ///   Campfire:
    ///     WorldStationInteractable + AddFuelAction
    ///   Campfire that also cooks:
    ///     WorldStationInteractable + OpenCraftingUIAction + AddFuelAction
    ///
    /// SETUP:
    ///   1. Attach to the root of the structure prefab
    ///   2. Add at least one IWorldInteractAction component (see above)
    ///   3. Ensure the GameObject has a non-trigger Collider2D for
    ///      InteractionDetector to pick up
    ///   4. Configure actionVerb / interactRange inherited from Interactable
    /// </summary>
    public class WorldStationInteractable : Interactable
    {
        [Header("Debug")]
        [Tooltip("Log which actions ran on interact.")]
        [SerializeField] private bool logActions = false;

        private IWorldInteractAction[] _actions;

        private void Awake()
        {
            // Cache all action components on this GameObject. GetComponents
            // works fine with interface types in Unity 2019.2+.
            _actions = GetComponents<IWorldInteractAction>();

            if (_actions == null || _actions.Length == 0)
                Debug.LogWarning($"[WorldStationInteractable] {name} has no " +
                                 "IWorldInteractAction components — clicking it will do nothing.");
        }

        public override void Interact(PlayerStateManager player)
        {
            if (_actions == null) return;

            foreach (var action in _actions)
            {
                if (action == null) continue;

                try
                {
                    action.Execute(player);

                    if (logActions)
                        Debug.Log($"[WorldStationInteractable] {name} → {action.GetType().Name}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[WorldStationInteractable] Action {action.GetType().Name} " +
                                   $"on {name} threw: {e.Message}");
                }
            }
        }
    }
}
