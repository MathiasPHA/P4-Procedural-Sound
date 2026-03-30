namespace MobSystem.States
{
    /// <summary>
    /// Abstract base for all mob AI states.
    /// Mirrors the PlayerBaseState pattern but adds ExitState for cleanup
    /// (e.g. stopping velocity when leaving Chasing, clearing timers).
    ///
    /// Each concrete state receives the MobController on every call,
    /// giving it access to MobData, physics, detection results, and
    /// the SwitchState method for transitions.
    /// </summary>
    public abstract class MobBaseState
    {
        /// <summary>Called once when this state becomes active.</summary>
        public abstract void EnterState(MobController mob);

        /// <summary>Called every frame while this state is active.</summary>
        public abstract void UpdateState(MobController mob);

        /// <summary>Called once when transitioning away from this state.</summary>
        public abstract void ExitState(MobController mob);
    }
}
