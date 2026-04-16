using UnityEngine;

/// <summary>
/// Player state while the instrument ring UI is open.
/// Keeps the player stationary until the ring is closed.
/// </summary>
public class PlayerMusicPlayingState : PlayerBaseState
{
    private PlayerStateManager _player;

    public override void EnterState(PlayerStateManager player)
    {
        _player = player;
        _player.playerRB.linearVelocity = Vector2.zero;
        _player.animationQue = "Idle";
    }

    public override void UpdateState(PlayerStateManager player)
    {
        _player.playerRB.linearVelocity = Vector2.zero;
        _player.animationQue = "Idle";
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
    }
}
