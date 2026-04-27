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
    /// SETUP:
    ///   1. Attach to the same GameObject as a BaseCraftingStation subclass
    ///   2. Also attach a WorldStationInteractable (the router)
    ///   3. Station ref auto-found via GetComponent; UI refs auto-found in scene
    /// </summary>
    public class OpenCraftingUIAction : MonoBehaviour, IWorldInteractAction
    {
        [Header("Target Station")]
        [Tooltip("The crafting station whose recipes should appear when the " +
                 "player clicks this structure. Auto-found on this GameObject.")]
        [SerializeField] private BaseCraftingStation station;

        [Header("UI References (auto-found if empty)")]
        [SerializeField] private InventoryUIManager inventoryUI;
        [SerializeField] private CraftingUIManager craftingUI;

        private void Awake()
        {
            if (station == null)
                station = GetComponent<BaseCraftingStation>();
        }

        private void Start()
        {
            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUIManager>();

            if (craftingUI == null)
                craftingUI = FindAnyObjectByType<CraftingUIManager>();

            if (station == null)
                Debug.LogError($"[OpenCraftingUIAction] No BaseCraftingStation on {name}!");
            if (craftingUI == null)
                Debug.LogError("[OpenCraftingUIAction] CraftingUIManager not found in scene!");
        }

        public void Execute(PlayerStateManager player)
        {
            if (station == null || craftingUI == null) return;

            // Open the inventory panel if it isn't already — puts player in
            // inventoryState (stops movement) and reveals the inventory UI.
            if (inventoryUI != null && !inventoryUI.IsInventoryOpen)
                inventoryUI.ToggleInventory();

            // Focus the crafting panel on this station. If already open, re-targets.
            craftingUI.OpenWithStation(station);
        }
    }
}
