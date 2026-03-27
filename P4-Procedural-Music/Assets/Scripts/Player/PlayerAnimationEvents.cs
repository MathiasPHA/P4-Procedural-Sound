using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerStateManager playerStateManager;

    public void OnHarvestAnimationComplete()
    {
        playerStateManager.OnHarvestAnimationComplete();
    }
}