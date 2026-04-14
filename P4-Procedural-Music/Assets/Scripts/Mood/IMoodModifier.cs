/// <summary>
/// Interface for any system that continuously influences the player's mood.
/// Implementors register themselves with MoodSystem and provide a per-second
/// rate that pushes mood up (positive) or down (negative).
///
/// Examples: HungerSystem, ComfortSystem, day/night cycle, shelter proximity.
///
/// For one-off mood changes (e.g. a jump scare, discovering a landmark),
/// use MoodSystem.AdjustMood(delta) directly instead of implementing this.
/// </summary>
public interface IMoodModifier
{
    /// <summary>
    /// Current mood contribution in units per second.
    /// Positive = improves mood, negative = worsens mood.
    /// Called every frame by MoodSystem — keep it lightweight.
    /// </summary>
    float MoodRate { get; }

    /// <summary>
    /// Display name for the debug GUI (e.g. "Hunger", "Environment", "Darkness").
    /// </summary>
    string ModifierName { get; }

    /// <summary>
    /// Whether this modifier is currently active. Inactive modifiers are
    /// skipped during summation but stay registered (no need to unregister
    /// and re-register when toggling on/off).
    /// </summary>
    bool IsActive { get; }
}
