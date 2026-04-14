using UnityEngine;

/// <summary>
/// Interface for any world object that influences the player's comfort.
/// Implement this on campfires, enemies, shelters, weather zones, etc.
/// </summary>
public interface IComfortInfluence
{
    /// <summary>The comfort value this source pushes toward (0 = terrifying, 1 = cozy).</summary>
    float ComfortValue { get; }

    /// <summary>How strongly this source influences comfort (0–1). Acts as a multiplier.</summary>
    float Weight { get; }

    /// <summary>The radius in world units within which this influence is active.</summary>
    float Radius { get; }

    /// <summary>World position of this influence source.</summary>
    Vector3 Position { get; }

    /// <summary>Display name for debug UI.</summary>
    string Name { get; }

    /// <summary>Return true if the underlying GameObject has been destroyed.</summary>
    bool IsDestroyed { get; }
}
