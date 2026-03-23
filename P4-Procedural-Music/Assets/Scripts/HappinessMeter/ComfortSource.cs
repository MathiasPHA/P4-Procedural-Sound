using UnityEngine;

/// <summary>
/// Attach this to any object that should affect the player's happiness.
///
/// comfortValue > 0  = Good object  (campfire, friend, cosy room...)
/// comfortValue < 0  = Evil object  (monster, dark altar, cursed item...)
///
/// The magnitude controls how powerful the effect is at point-blank range.
/// Recommended range: -1.0 to +1.0  (the ComfortSystem multiplier scales it up).
/// </summary>
public class ComfortSource : MonoBehaviour
{
    [Tooltip("Positive = good for the player. Negative = bad for the player.\nRange: -1 (very evil) to +1 (very good).")]
    [Range(-1f, 1f)]
    public float comfortValue = 1f;

    [Tooltip("Optional label shown in debug UI.")]
    public string sourceName = "Comfort Source";

    // Draws a small icon in the Scene view so you can spot sources easily
    private void OnDrawGizmos()
    {
        Gizmos.color = comfortValue >= 0
            ? new Color(0.2f, 1f, 0.4f, 0.6f)   // green = good
            : new Color(1f, 0.2f, 0.2f, 0.6f);  // red   = evil

        Gizmos.DrawSphere(transform.position, 0.25f);
    }
}