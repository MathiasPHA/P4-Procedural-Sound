using UnityEngine;

public class PlayerShrugState : PlayerBaseState
{
    private PlayerStateManager playerStateManager;
    private float _timer;
    private const float ShrugDuration = 1f; // match your animation clip length

    public override void EnterState(PlayerStateManager player)
{
    _timer = ShrugDuration;
    playerStateManager = player;
    playerStateManager.animationQue = "Shrug";
    Debug.Log("I do not know");
}

    public override void UpdateState(PlayerStateManager player)
    {
       /* playerStateManager.StartShrug();
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
            player.SwitchState(player.idleState);*/
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
        
    }
}