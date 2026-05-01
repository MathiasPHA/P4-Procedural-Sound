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

            // Walk toward the closest point on the collider (natural path into the object).
            // Distance is measured from the SAME point so interactables with large or
            // blocking colliders (e.g. water — the player can't physically pass through it
            // to reach an InteractCenter that's inside the lake) still trigger correctly.
            // For small colliders (bushes, items) ClosestPoint ≈ center, so behavior is
            // effectively unchanged.
            // Walk toward the closest point on the collider (natural path into the object).
            // Distance is measured from the SAME point so interactables with large or
            // blocking colliders (e.g. water — the player can't physically pass through it
            // to reach an InteractCenter that's inside the lake) still trigger correctly.
            // For small colliders (bushes, items) ClosestPoint ≈ center, so behavior is
            // effectively unchanged.
            Vector2 destination = _targetCollider != null
                ? _targetCollider.ClosestPoint(player.transform.position)
                : (Vector2)_target.InteractCenter;

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

        // Cached on first lookup. -1 means "Water layer doesn't exist" — in
        // that case the water-collision shortcut is skipped and the normal
        // distance-based arrival logic still works for everything else.
        private int _waterLayer = -2; // -2 = uninitialised, -1 = doesn't exist

        public override void OnCollisionEnter(PlayerStateManager player)
        {
            // If we're walking toward a WaterFishingInteractable, the normal
            // distance check won't trigger because the cursor is inside the
            // lake (player can't physically reach it). Instead, treat the
            // moment we collide with any water tile as "arrived" and trigger interact.
            if (_target == null) return;
            if (!(_target is WaterFishingInteractable)) return;

            var col = player.LastCollision;
            if (col == null) return;

            // Lazy-init the cached layer index.
            if (_waterLayer == -2) _waterLayer = LayerMask.NameToLayer("Water");
            if (_waterLayer == -1) return; // Water layer doesn't exist — bail.

            // Layer check: only react if the thing we hit is on the Water layer.
            if (col.gameObject.layer != _waterLayer) return;

            // Stop, face the collision point, and trigger interact.
            player.playerRB.linearVelocity = Vector2.zero;

            Vector2 dir = (col.GetContact(0).point - (Vector2)player.transform.position).normalized;
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                player.playerDir = dir.x < 0 ? "Left" : "Right";
            else
                player.playerDir = dir.y < 0 ? "Down" : "Up";

            // Use whichever water collider we actually hit (it may be a different
            // chunk than the originally-clicked one — fine, any water tile is
            // valid for fishing).
            var hitWater = col.collider.GetComponent<WaterFishingInteractable>();
            if (hitWater != null)
                hitWater.Interact(player);
            else
                _target.Interact(player); // fallback to original target

            // If Interact() didn't switch state itself, fall back to idle.
            if (player.CurrentState == this)
                player.SwitchState(player.idleState);
        }

        private void Cancel()
        {
            _player.playerRB.linearVelocity = Vector2.zero;
            _target = null;
        }
    }
}