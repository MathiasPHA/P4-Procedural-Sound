using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class PlayerRunState : PlayerBaseState
{
    private PlayerStateManager PlayerStateManager;
    public string playerDir;

    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("I'm Running");
        PlayerStateManager = player;
    }

    public override void UpdateState(PlayerStateManager player)
    {
        GetDircetion(PlayerStateManager.moveInput.x, PlayerStateManager.moveInput.y);

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

    private void GetDircetion(float x, float y)
    {
        if (x < 0)
        {
            playerDir = "Left"; 
            Debug.Log("Left");
        }
        else if (x > 0)
        {
            playerDir = "Right";
            Debug.Log("Right");
        }
        else if (y < 0)
        {
            playerDir = "Down";
            Debug.Log("Down");
        }
        else if (y > 0)
        {
            playerDir = "Up";
            Debug.Log("Up");
        }
    }
}
