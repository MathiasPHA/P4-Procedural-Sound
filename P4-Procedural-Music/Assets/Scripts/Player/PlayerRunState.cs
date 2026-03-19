using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class PlayerRunState : PlayerBaseState
{
    private PlayerStateManager PlayerStateManager;


    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("I'm Running");
        PlayerStateManager = player;
    }

    public override void UpdateState(PlayerStateManager player)
    {
        PlayerStateManager.playerRB.linearVelocity = PlayerStateManager.moveInput * PlayerStateManager.moveSpeed;// Move the player based on input and speed

        // If there is no moveinput, switch back to idle state
        if (PlayerStateManager.moveInput == Vector2.zero)
        {
            PlayerStateManager.SwitchState(PlayerStateManager.idleState);
        }
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
        
    }

   
}
