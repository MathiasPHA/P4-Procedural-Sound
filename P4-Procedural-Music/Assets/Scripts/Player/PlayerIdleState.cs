using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    private PlayerStateManager playerStateManager;
    public override void EnterState(PlayerStateManager player)
    {
        //Debug.Log("I'm Idle");
        playerStateManager = player;
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // If there is moveinput, switch to run state
        if (playerStateManager.moveInput != Vector2.zero)
        {
            playerStateManager.SwitchState(playerStateManager.runState);
        }

        playerStateManager.animationQue = "Idle";
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
        
    }

}
