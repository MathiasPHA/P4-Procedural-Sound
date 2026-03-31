using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Search state for hostile mobs. When the player escapes during a chase,
    /// the mob moves to the player's last known position, wanders briefly
    /// within a small radius, then gives up and returns to roaming.
    ///
    /// Transitions:
    ///   → ChasingState    when the player is re-detected
    ///   → RoamingState    when searchDuration expires (gives up)
    ///
    /// On timeout exit, releases combat music back to auto mode.
    /// </summary>
    public class SearchingState : MobBaseState
    {
        private float _searchTimer;
        private Vector2 _searchCenter;
        private Vector2 _currentTarget;
        private bool _reachedCenter;

        private const float ArrivalThreshold = 0.5f;

        public override void EnterState(MobController mob)
        {
            _searchTimer = mob.Data.searchDuration;
            _searchCenter = mob.LastKnownPlayerPos;
            _currentTarget = _searchCenter;
            _reachedCenter = false;

            mob.AnimationQueue = "Walk";
        }

        public override void UpdateState(MobController mob)
        {
            // ── Re-detected the player? Resume chase ──
            if (mob.PlayerDetected)
            {
                mob.SwitchState(mob.chasingState);
                return;
            }

            // ── Timer expired? Give up ──
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
            {
                mob.SwitchState(mob.roamingState);
                return;
            }

            // ── Move to target ──
            Vector2 mobPos = mob.transform.position;
            float dist = Vector2.Distance(mobPos, _currentTarget);

            if (dist < ArrivalThreshold)
            {
                if (!_reachedCenter)
                {
                    // Just arrived at last known position — start wandering
                    _reachedCenter = true;
                    PickSearchWaypoint(mob);
                }
                else
                {
                    // Arrived at a search waypoint — pick another
                    PickSearchWaypoint(mob);
                }
            }

            Vector2 direction = (_currentTarget - mobPos).normalized;
            mob.Rb.linearVelocity = direction * mob.Data.moveSpeed * 0.7f; // Slower — scanning
            mob.FacingDirection = direction;
            mob.AnimationQueue = "Walk";
        }

        public override void ExitState(MobController mob)
        {
            mob.Rb.linearVelocity = Vector2.zero;

            // If giving up (not re-detecting), release combat music
            if (!mob.PlayerDetected)
            {
                if (GameStateManager.Instance != null && GameStateManager.Instance.IsManualOverride)
                    GameStateManager.Instance.ReturnToAuto();
            }
        }

        private void PickSearchWaypoint(MobController mob)
        {
            Vector2 offset = Random.insideUnitCircle * mob.Data.searchRadius;
            _currentTarget = _searchCenter + offset;
        }
    }
}
