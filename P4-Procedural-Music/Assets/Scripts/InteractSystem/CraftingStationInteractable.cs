using UnityEngine;
using InventorySystem.Crafting;
using InventorySystem.UI;

namespace InteractionSystem
{
    /// <summary>
    /// Interactable for crafting stations (workbench, furnace, etc.).
    /// On interact, activates the station on CraftingUIManager.
    ///
    /// This replaces the proximity-based trigger on CraftingStationInteraction.
    /// You can keep CraftingStationInteraction for its proximity mode if you want both,
    /// or remove it and use this exclusively.
    ///
    /// SETUP:
    ///   1. Attach alongside BaseCraftingStation on the station GameObject
    ///   2. Set actionVerb to "Use" or "Open" in Inspector
    ///   3. Ensure Collider2D on "Interactable" layer (can be separate from the trigger collider)
    /// </summary>
    public class CraftingStationInteractable : Interactable
    {
        [Header("Crafting")]
        [SerializeField] private BaseCraftingStation station;
        [SerializeField] private CraftingUIManager craftingUIManager;

        private void Start()
        {
            if (station == null)
                station = GetComponent<BaseCraftingStation>();
            if (craftingUIManager == null)
                craftingUIManager = FindAnyObjectByType<CraftingUIManager>();
        }

        public override void Interact(PlayerStateManager player)
        {
            if (station == null || craftingUIManager == null) return;

            if (craftingUIManager.ActiveStation == station)
                craftingUIManager.ClearStation();
            else
                craftingUIManager.SetActiveStation(station);
        }
    }
}
