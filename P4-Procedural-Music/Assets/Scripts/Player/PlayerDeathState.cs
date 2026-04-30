using UnityEngine;

/// <summary>
/// Terminal state — entered when the player dies. Locks movement, sets the
/// "Death" animation queue, and never exits on its own. PlayerDeathHandler
/// switches into this state as the first step of its death sequence; the
/// game scene is wiped/reloaded afterward, so there is no recovery from here.
///
/// Mirrors PlayerHurtState's structure but with no timer — the state simply
/// holds the player frozen while the death fade plays out.
/// </summary>
public class PlayerDeathState : PlayerBaseState
{
    private PlayerStateManager _player;

    public override void EnterState(PlayerStateManager player)
    {
        _player = player;

        // Freeze movement permanently
        _player.playerRB.linearVelocity = Vector2.zero;
        _player.playerRB.angularVelocity = 0f;
        _player.moveInput = Vector2.zero;

        // Play death animation — clip names: PlayerDeathFront, PlayerDeathLeft, etc.
        // (or just "PlayerDeath" if you only want a single non-directional clip;
        // PlayerAnimations needs a matching block either way.)
        _player.animationQue = "Death";
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Stay frozen. No exit condition — PlayerDeathHandler owns the
        // post-death flow (fade, world wipe, scene reload).
        _player.playerRB.linearVelocity = Vector2.zero;
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
    }
}
