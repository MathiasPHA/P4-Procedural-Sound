using UnityEngine;

/// <summary>
/// Player state active while the inventory panel is open.
/// Zeroes velocity so the player stops moving, and ignores movement input.
/// The player returns to IdleState when the inventory is closed.
/// </summary>
public class PlayerInventoryState : PlayerBaseState
{
    private PlayerStateManager _player;

    public override void EnterState(PlayerStateManager player)
    {
        _player = player;

        // Stop all movement immediately
        _player.playerRB.linearVelocity = Vector2.zero;
        _player.moveInput = Vector2.zero;

        Debug.Log("I'm in Inventory");
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Keep velocity zeroed — movement input is disabled at the
        // input layer (Movement action map is off), but this is a
        // safety net in case anything leaks through.
        _player.playerRB.linearVelocity = Vector2.zero;

        // State exit is handled externally by InventoryUIManager
        // calling PlayerStateManager.SwitchState(idleState) when
        // the inventory closes — not by checking input here.
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
    }
}
