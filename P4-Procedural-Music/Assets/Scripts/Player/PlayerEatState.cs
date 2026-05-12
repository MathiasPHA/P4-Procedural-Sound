using UnityEngine;

/// <summary>
/// Player state active while eating a consumable.
/// Locks movement and plays the eat animation for a fixed duration,
/// then returns to idle automatically.
/// Triggered by HungerSystem.OnFoodEaten via PlayerStateManager.StartEat().
/// </summary>
public class PlayerEatState : PlayerBaseState
{
    [Tooltip("How long the eat animation locks the player (seconds)")]
    private float _eatDuration = 0.8f;
    private float _timer;

    /// <summary>
    /// Call before SwitchState to configure the lock duration.
    /// </summary>
    public void Configure(float duration)
    {
        _eatDuration = duration;
    }

    public override void EnterState(PlayerStateManager player)
    {
        _timer = _eatDuration;

        // Stop movement while eating
        player.playerRB.linearVelocity = Vector2.zero;
        player.moveInput = Vector2.zero;

        player.animationQue = "Eat";
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Keep frozen while eating
        player.playerRB.linearVelocity = Vector2.zero;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
            player.SwitchState(player.idleState);
    }

    public override void OnCollisionEnter(PlayerStateManager player) { }
}
