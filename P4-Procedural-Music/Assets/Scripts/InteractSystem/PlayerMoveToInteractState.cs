using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Player state that walks toward a target Interactable and triggers
    /// interaction when in range. Cancels to idle if the target is destroyed
    /// or becomes invalid, or if the player provides movement input.
    ///
    /// Movement uses direct velocity (same as PlayerRunState) — 
    /// swap to pathfinding later by changing the movement logic in UpdateState.
    /// </summary>
    public class PlayerMoveToInteractState : PlayerBaseState
    {
        private PlayerStateManager _player;
        private Interactable _target;
        private Collider2D _targetCollider;

        /// <summary>
        /// Call this BEFORE switching to this state.
        /// </summary>
        public void SetTarget(Interactable target)
        {
            _target = target;
            _targetCollider = target.GetComponent<Collider2D>();
        }

        public override void EnterState(PlayerStateManager player)
        {
            _player = player;
            player.animationQue = "Run";
        }

        public override void UpdateState(PlayerStateManager player)
        {
            // Cancel if player gives manual movement input
            if (player.moveInput != Vector2.zero)
            {
                Cancel();
                player.SwitchState(player.runState);
                return;
            }

            // Cancel if target was destroyed or can no longer be interacted with
            if (_target == null || !_target.CanInteract())
            {
                Cancel();
                player.SwitchState(player.idleState);
                return;
            }

            // Get closest point on the target's collider to walk toward
            Vector2 destination = _targetCollider != null
                ? _targetCollider.ClosestPoint(player.transform.position)
                : (Vector2)_target.transform.position;

            float distance = Vector2.Distance(player.transform.position, destination);

            // Arrived — interact
            if (distance <= _target.InteractRange)
            {
                player.playerRB.linearVelocity = Vector2.zero;

                // Face the target
                Vector2 dir = (destination - (Vector2)player.transform.position).normalized;
                if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                    player.playerDir = dir.x < 0 ? "Left" : "Right";
                else
                    player.playerDir = dir.y < 0 ? "Down" : "Up";

                _target.Interact(player);

                // Only switch to idle if Interact() didn't already change state
                // (e.g. HarvestInteractable switches to harvestState)
                if (player.CurrentState == this)
                    player.SwitchState(player.idleState);
                return;
            }

            // Move toward target
            Vector2 moveDir = ((Vector2)destination - (Vector2)player.transform.position).normalized;
            player.playerRB.linearVelocity = moveDir * player.moveSpeed;

            // Update facing direction for animation
            if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y))
                player.playerDir = moveDir.x < 0 ? "Left" : "Right";
            else
                player.playerDir = moveDir.y < 0 ? "Down" : "Up";

            player.animationQue = "Run";
        }

        public override void OnCollisionEnter(PlayerStateManager player)
        {
        }

        private void Cancel()
        {
            _player.playerRB.linearVelocity = Vector2.zero;
            _target = null;
        }
    }
}