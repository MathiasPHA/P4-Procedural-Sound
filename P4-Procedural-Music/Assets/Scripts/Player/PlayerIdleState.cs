using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    private PlayerStateManager PlayerStateManager;
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("I'm Idle");
        PlayerStateManager = player;
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // If there is moveinput, switch to run state
        if (PlayerStateManager.moveInput != Vector2.zero)
        {
            PlayerStateManager.SwitchState(PlayerStateManager.runState);
        }
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
        
    }

}
