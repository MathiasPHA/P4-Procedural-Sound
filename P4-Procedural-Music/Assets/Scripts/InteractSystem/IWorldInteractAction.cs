namespace InteractionSystem
{
    /// <summary>
    /// Marker interface for a composable on-interact behavior.
    /// Any MonoBehaviour implementing this can live alongside a
    /// <see cref="WorldStationInteractable"/> — the router finds every
    /// IWorldInteractAction on the same GameObject and fires them in order
    /// when the player arrives.
    ///
    /// This is how structures get their on-click behavior without inheriting
    /// from Interactable directly. Want a workbench? Add WorldStationInteractable
    /// + OpenCraftingUIAction. Want a campfire? Add WorldStationInteractable
    /// + AddFuelAction. Want a campfire that also opens a cooking UI?
    /// Add all three — the router runs both actions on arrival.
    /// </summary>
    public interface IWorldInteractAction
    {
        /// <summary>
        /// Called by WorldStationInteractable when the player has walked
        /// into interact range and the click resolves.
        /// </summary>
        void Execute(PlayerStateManager player);
    }
}
