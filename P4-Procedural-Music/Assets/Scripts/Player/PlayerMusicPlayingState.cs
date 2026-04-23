using UnityEngine;

/// <summary>
/// Player state active while the flute ring is open.
/// Locks all movement. FluteTool drives entry via StartMusicPlaying()
/// and exit via StopMusicPlaying() when the ring closes.
/// </summary>
public class PlayerMusicPlayingState : PlayerBaseState
{
    private PlayerStateManager _player;

    public override void EnterState(PlayerStateManager player)
    {
        _player = player;

        // Stop all movement immediately
        _player.playerRB.linearVelocity = Vector2.zero;
        _player.moveInput = Vector2.zero;

        _player.animationQue = "MusicPlaying";
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Keep player frozen while playing — FluteTool calls StopMusicPlaying() to exit
        _player.playerRB.linearVelocity = Vector2.zero;
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
    }
}