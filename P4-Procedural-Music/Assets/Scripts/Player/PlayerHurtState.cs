using UnityEngine;

/// <summary>
/// Player state active while reeling from a mob hit. Locks movement for a
/// short stun duration, then returns to idle. The stun duration is supplied
/// by PlayerHealth when it triggers the state — see PlayerStateManager.StartHurt.
///
/// Exit is timer-driven rather than animation-event-driven (unlike
/// PlayerHarvestState) so that a missing or mis-named Hurt clip doesn't
/// leave the player permanently stuck in this state.
/// </summary>
public class PlayerHurtState : PlayerBaseState
{
    private PlayerStateManager _player;
    private float _stunTimer;

    /// <summary>
    /// Called by PlayerStateManager.StartHurt before transitioning in.
    /// Lets PlayerHealth supply the stun duration so this state stays dumb.
    /// </summary>
    public void Configure(float stunDuration)
    {
        _stunTimer = stunDuration;
    }

    public override void EnterState(PlayerStateManager player)
    {
        _player = player;

        // Freeze movement
        _player.playerRB.linearVelocity = Vector2.zero;
        _player.moveInput = Vector2.zero;

        // Play hurt animation — clip names: PlayerHurtFront, PlayerHurtLeft, etc.
        _player.animationQue = "Hurt";
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Keep frozen for the duration of the stun
        _player.playerRB.linearVelocity = Vector2.zero;

        _stunTimer -= Time.deltaTime;
        if (_stunTimer <= 0f)
        {
            player.SwitchState(player.idleState);
        }
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
    }
}
