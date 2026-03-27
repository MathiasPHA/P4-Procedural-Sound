using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    [SerializeField] private PlayerStateManager playerStateManager;
    private Animator playerAnimator;
    private string animationQue;

    private void Awake()
    {
        playerAnimator = GetComponent<Animator>();
    }

    private void Update()
    {
        Animate();
        animationQue = playerStateManager.animationQue;
    }

    void Animate() 
    {
        if (animationQue == "Idle") 
        {
            if (playerStateManager.playerDir == "Down")
            {
                playerAnimator.Play("PlayerIdleFront");
            }
            else if (playerStateManager.playerDir == "Left")
            {
                playerAnimator.Play("PlayerIdleLeft");
            }
            else if (playerStateManager.playerDir == "Right")
            {
                playerAnimator.Play("PlayerIdleRight");
            }
            else if (playerStateManager.playerDir == "Up")
            {
                playerAnimator.Play("PlayerIdleBack");
            }
        }

        if (animationQue == "Run")
        {
            if (playerStateManager.playerDir == "Down")
            {
                playerAnimator.Play("PlayerIdleFront");
            }
            else if (playerStateManager.playerDir == "Left")
            {
                playerAnimator.Play("PlayerIdleLeft");
            }
            else if (playerStateManager.playerDir == "Right")
            {
                playerAnimator.Play("PlayerIdleRight");
            }
            else if (playerStateManager.playerDir == "Up")
            {
                playerAnimator.Play("PlayerIdleBack");
            }
        }

        if (animationQue == "HarvestAxe")
        {
            if (playerStateManager.playerDir == "Left")
                playerAnimator.Play("PlayerHarvestAxeLeft");
            else if (playerStateManager.playerDir == "Right")
                playerAnimator.Play("PlayerHarvestAxeRight");
        }

        if (animationQue == "HarvestPickaxe")
        {
            if (playerStateManager.playerDir == "Left"||playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerHarvestPickaxeLeft");
            else if (playerStateManager.playerDir == "Right"||playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerHarvestPickaxeRight");
        }

        if (animationQue == "HarvestHand")
        {
            if (playerStateManager.playerDir == "Left"||playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerHarvestHandLeft");
            else if (playerStateManager.playerDir == "Right"||playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerHarvestHandRight");
        }
    }
}
