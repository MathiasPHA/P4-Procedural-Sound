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
        animationQue = playerStateManager.animationQue;
        Animate();
    }

    void Animate()
    {
        if (animationQue == "Idle")
        {
            if (playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerIdleFront");
            else if (playerStateManager.playerDir == "Left")
                playerAnimator.Play("PlayerIdleLeft");
            else if (playerStateManager.playerDir == "Right")
                playerAnimator.Play("PlayerIdleRight");
            else if (playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerIdleBack");
        }

        if (animationQue == "Run")
        {
            if (playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerRunFront");
            else if (playerStateManager.playerDir == "Left")
                playerAnimator.Play("PlayerRunLeft");
            else if (playerStateManager.playerDir == "Right")
                playerAnimator.Play("PlayerRunRight");
            else if (playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerRunBack");
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
            if (playerStateManager.playerDir == "Left" || playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerPickaxeSwingLeft");
            else if (playerStateManager.playerDir == "Right" || playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerPickaxeSwingRight");
        }

        if (animationQue == "HarvestHammer")
        {
            if (playerStateManager.playerDir == "Left" || playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerHammerSwingLeft");
            else if (playerStateManager.playerDir == "Right" || playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerHammerSwingRight");
        }

        if (animationQue == "HarvestSpear")
        {
            if (playerStateManager.playerDir == "Left" || playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerSpearSwingLeft");
            else if (playerStateManager.playerDir == "Right" || playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerSpearSwingRight");
        }

        if (animationQue == "HarvestHand")
        {
            if (playerStateManager.playerDir == "Left" || playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerHarvestHandLeft");
            else if (playerStateManager.playerDir == "Right" || playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerHarvestHandRight");
        }

        if (animationQue == "PickUp")
        {
            if (playerStateManager.playerDir == "Left" || playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerHarvestHandLeft");
            else
                playerAnimator.Play("PlayerHarvestHandRight");
        }

        if (animationQue == "Shrug")
        {
            playerAnimator.Play("PlayerShrug");
        }

        if (animationQue == "Hurt")
        {
            if (playerStateManager.playerDir == "Down")
                playerAnimator.Play("PlayerHurtFront");
            else if (playerStateManager.playerDir == "Left")
                playerAnimator.Play("PlayerHurtLeft");
            else if (playerStateManager.playerDir == "Right")
                playerAnimator.Play("PlayerHurtRight");
            else if (playerStateManager.playerDir == "Up")
                playerAnimator.Play("PlayerHurtBack");
        }

        if (animationQue == "Death")
        {
            if (playerStateManager.playerDir == "Down")
            {
                playerAnimator.Play("PlayerDeathFront");
            }
            else if (playerStateManager.playerDir == "Left")
            {
                playerAnimator.Play("PlayerDeathLeft");
            }
            else if (playerStateManager.playerDir == "Right")
            {
                playerAnimator.Play("PlayerDeathRight");
            }
            else if (playerStateManager.playerDir == "Up")
            {
                playerAnimator.Play("PlayerDeathBack");
            }
        }
    }
}