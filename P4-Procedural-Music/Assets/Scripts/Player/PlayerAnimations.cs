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
                playerAnimator.Play("PlayerRunFront");
            }
            else if (playerStateManager.playerDir == "Left")
            {
                playerAnimator.Play("PlayerRunLeft");
            }
            else if (playerStateManager.playerDir == "Right")
            {
                playerAnimator.Play("PlayerRunRight");
            }
            else if (playerStateManager.playerDir == "Up")
            {
                playerAnimator.Play("PlayerRunBack");
            }
        }

        if (animationQue == "HarvestAxe")
        {
            if (playerStateManager.playerDir == "Left" || playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerAxeSwingLeft");
            else if (playerStateManager.playerDir == "Right" || playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerAxeSwingRight");
        }

        if (animationQue == "HarvestPickaxe")
        {
            if (playerStateManager.playerDir == "Left"|| playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerPickaxeSwingLeft");
            else if (playerStateManager.playerDir == "Right"|| playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerPickaxeSwingRight");
        }

        if (animationQue == "HarvestHand")
        {
            if (playerStateManager.playerDir == "Left"||playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerHarvestHandLeft");
            else if (playerStateManager.playerDir == "Right"||playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerHarvestHandRight");
        }

        if (animationQue == "PickUp")
        {
            if (playerStateManager.playerDir == "Left" || playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerHarvestHandLeft");
            else
                playerAnimator.Play("PlayerHarvestHandRight");
        }
    }
}
