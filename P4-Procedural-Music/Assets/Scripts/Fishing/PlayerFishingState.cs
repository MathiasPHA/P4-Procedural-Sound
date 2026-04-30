using UnityEngine;
using UnityEngine.InputSystem;
using FishingSystem;

/// <summary>
/// Player state entered when fishing begins. Locks the player in place,
/// faces them toward the cast point, plays the fishing animation, and
/// listens for Escape to cancel. When FishingManager reports the outcome
/// (caught or escaped), exits back to idle.
///
/// Lifecycle matches your other states (PlayerHarvestState, PlayerIdleState etc.):
/// the manager creates one instance and calls EnterState/UpdateState/ExitState
/// passing itself as the parameter.
///
/// IMPORTANT: this state does NOT call player.StartHarvest(). StartHarvest
/// switches to harvestState, which would immediately yank us out of fishing.
/// Instead we set animationQue directly and let the animator drive itself.
/// </summary>
public class PlayerFishingState : PlayerBaseState
{
    /// <summary>Set by ToolUseSystem before SwitchState. World position of the cast.</summary>
    public Vector3 CastPosition { get; set; }

    private bool _outcomeReceived;
    private bool _wasCaught;

    public override void EnterState(PlayerStateManager player)
    {
        _outcomeReceived = false;
        _wasCaught = false;

        // Face the cast point.
        float xDiff = CastPosition.x - player.transform.position.x;
        player.playerDir = xDiff < 0 ? "Left" : "Right";

        // Stop the player moving.
        if (player.playerRB != null)
            player.playerRB.linearVelocity = Vector2.zero;

        // Drive animation directly. Convention from ToolUseSystem is "Harvest{toolType}",
        // but we DON'T call StartHarvest() because that would switch states out of fishing.
        // Your animator should have a "HarvestFishingRod" state (or similar — change the
        // string below to match whatever you set up). If you don't have one yet, set this
        // to "Idle" temporarily so the loop still works.
        player.animationQue = "HarvestFishingRod";

        if (FishingManager.Instance == null)
        {
            Debug.LogError("[PlayerFishingState] No FishingManager in scene.");
            player.SwitchState(player.idleState);
            return;
        }

        bool started = FishingManager.Instance.StartFishing(player.transform, OnFishingComplete);
        if (!started)
        {
            player.SwitchState(player.idleState);
        }
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Cancel on Escape.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (FishingManager.Instance != null && FishingManager.Instance.IsFishing)
                FishingManager.Instance.CancelFishing(); // routes back through OnFishingComplete
            return;
        }

        // Once the manager reports the outcome, exit back to idle.
        // We do this in Update (rather than directly from the callback) so the
        // state switch happens at a predictable point in the frame.
        if (_outcomeReceived)
        {
            player.SwitchState(player.idleState);
        }
    }

    public override void OnCollisionEnter(PlayerStateManager player)
    {
        // Fishing is a locked-in minigame state — collisions don't change anything.
    }

    /// <summary>
    /// If your PlayerBaseState defines ExitState, override it here. If it doesn't,
    /// you can delete this method — the cancel-on-leave guard is a defensive
    /// nice-to-have, not strictly required.
    /// </summary>
    public void ExitState(PlayerStateManager player)
    {
        // If we exit for any external reason while a session is still active
        // (e.g. death, scene change), stop the manager. CancelFishing is a
        // no-op when not fishing.
        if (FishingManager.Instance != null && FishingManager.Instance.IsFishing)
            FishingManager.Instance.CancelFishing();
    }

    private void OnFishingComplete(bool caught)
    {
        _outcomeReceived = true;
        _wasCaught = caught;
    }
}