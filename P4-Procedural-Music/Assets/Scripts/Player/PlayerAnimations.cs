using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    [SerializeField] private PlayerStateManager playerStateManager;
    private Animator playerAnimator;

    private void Awake()
    {
        playerAnimator = GetComponent<Animator>();
    }

    private void Update()
    {
        Animate();
    }

    void Animate() 
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
}
