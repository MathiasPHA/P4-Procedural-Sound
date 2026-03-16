using UnityEngine;

/// <summary>
/// Attach this to your Canvas (World Space).
/// In Unity, UNPARENT the Canvas from the Player (drag it out in the Hierarchy).
/// Then assign the Player transform in the Inspector.
/// The Canvas will follow the player's position with an offset, but never inherit its scale flip.
/// </summary>

public class HappyBarFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, 0f); // Adjust to sit above the player
 
    private void LateUpdate()
    {
        if (player == null) return;
        transform.position = player.position + offset;
    }
}
