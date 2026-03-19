using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class PlayerRunState : PlayerBaseState
{
    private PlayerStateManager playerStateManager;

    public override void EnterState(PlayerStateManager player)
    {
        //Debug.Log("I'm Running");
        playerStateManager = player;
    }

    public override void UpdateState(PlayerStateManager player)
    {
        playerStateManager.playerRB.linearVelocity = playerStateManager.moveInput * playerStateManager.moveSpeed;// Move the player based on input and speed

        playerStateManager.animationQue = "Run";

        // If there is no moveinput, switch back to idle state
        if (playerStateManager.moveInput == Vector2.zero)
        {
            playerStateManager.SwitchState(playerStateManager.idleState);
        }
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
        
    }

   
}
