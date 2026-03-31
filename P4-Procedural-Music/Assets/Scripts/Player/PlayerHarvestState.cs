using UnityEngine;

/// <summary>
/// Player state active while performing a harvest swing.
/// ToolUseSystem sets player.animationQue BEFORE calling StartHarvest(),
/// so this state just locks movement and waits for the animation event.
/// Exit is triggered by an Animation Event calling OnHarvestAnimationComplete().
/// </summary>
public class PlayerHarvestState : PlayerBaseState
{
    private PlayerStateManager _player;

    public override void EnterState(PlayerStateManager player)
    {
        _player = player;

        // Stop movement while harvesting
        _player.playerRB.linearVelocity = Vector2.zero;
        _player.moveInput = Vector2.zero;

        // animationQue is already set by ToolUseSystem before this state is entered:
        //   player.animationQue = "HarvestAxe";
        //   player.StartHarvest();
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Keep player stationary during the swing
        _player.playerRB.linearVelocity = Vector2.zero;

        // Exit is driven by an Animation Event on each harvest clip
        // calling PlayerStateManager.OnHarvestAnimationComplete()
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
    }
}