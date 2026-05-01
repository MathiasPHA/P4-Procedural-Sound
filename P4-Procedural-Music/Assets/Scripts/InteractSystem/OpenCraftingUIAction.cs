using UnityEngine;
using InteractionSystem;
using InventorySystem.Crafting;
using InventorySystem.UI;

namespace InventorySystem.Crafting
{
    /// <summary>
    /// On-interact action: opens the inventory + crafting UI focused on a
    /// specific BaseCraftingStation. Pluggable replacement for the per-station
    /// Interactable subclasses — pair with a <see cref="WorldStationInteractable"/>.
    ///
    /// Works for every crafting station type (CookingStation, Workbench, Forge, …)
    /// because it targets the shared BaseCraftingStation abstraction.
    ///
    /// Open/close is routed through UICoordinator so both panels always open
    /// together. This script no longer calls InventoryUIManager or
    /// CraftingUIManager directly for open/close operations.
    ///
    /// SETUP:
    ///   1. Attach to the same GameObject as a BaseCraftingStation subclass
    ///   2. Also attach a WorldStationInteractable (the router)
    ///   3. Station ref auto-found via GetComponent; coordinator auto-found in scene
    ///
    /// Note: Awake/Start do NOT error when refs are missing. Placement ghosts
    /// instantiate this prefab too, and ghosts don't need functional refs.
    /// Validation is deferred to Execute, which only runs on real placed objects.
    /// </summary>
    public class OpenCraftingUIAction : MonoBehaviour, IWorldInteractAction
    {
        [Header("Target Station")]
        [Tooltip("The crafting station whose recipes should appear when the " +
                 "player clicks this structure. Auto-found on this GameObject.")]
        [SerializeField] private BaseCraftingStation station;

        [Header("UI Coordinator (auto-found if empty)")]
        [SerializeField] private UICoordinator coordinator;

        private void Awake()
        {
            if (station == null)
                station = GetComponent<BaseCraftingStation>();
        }

        private void Start()
        {
            if (coordinator == null)
                coordinator = FindAnyObjectByType<UICoordinator>();
            // No errors here — ghost previews don't need functional refs.
        }

        public void Execute(PlayerStateManager player)
        {
            if (station == null)
            {
                Debug.LogError($"[OpenCraftingUIAction] {name}: no BaseCraftingStation found — can't open panel.");
                return;
            }

            if (coordinator == null)
            {
                // Last-chance lookup in case Start hadn't fired yet
                coordinator = FindAnyObjectByType<UICoordinator>();
                if (coordinator == null)
                {
                    Debug.LogError($"[OpenCraftingUIAction] {name}: no UICoordinator in scene — can't open panels.");
                    return;
                }
            }

            // Opens both inventory + crafting panels together and targets this station.
            coordinator.OpenWithStation(station);

            Debug.Log($"[OpenCraftingUIAction] Opened panels for station: {station.StationType}.");
        }
    }
}