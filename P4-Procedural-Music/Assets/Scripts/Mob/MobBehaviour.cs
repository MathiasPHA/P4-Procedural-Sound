namespace MobSystem.Data
{
    /// <summary>
    /// Determines which state-set a mob uses at runtime.
    ///
    ///   Passive  → Roaming + Fleeing only (rabbits, deer)
    ///   Hostile  → Roaming + Chasing + Attacking + Searching + Fleeing (wolves, shadows)
    ///   Neutral  → Roaming + Fleeing, but switches to hostile states when provoked (boars)
    /// </summary>
    public enum MobBehaviour
    {
        Passive,
        Hostile,
        Neutral
    }
}
