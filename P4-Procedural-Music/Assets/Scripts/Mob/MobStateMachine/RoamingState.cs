using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Default idle state. The mob picks random waypoints near its spawn
    /// position, walks to them, pauses, and repeats. On detecting the player:
    ///   Passive → Fleeing
    ///   Hostile → Chasing
    ///   Neutral (unprovoked) → Fleeing
    ///   Neutral (provoked) → Chasing
    /// </summary>
    public class RoamingState : MobBaseState
    {
        private Vector2 _waypoint;
        private float _idleTimer;
        private bool _isIdling;

        // How close to the waypoint before counting as "arrived" (units)
        private const float ArrivalThreshold = 0.3f;

        public override void EnterState(MobController mob)
        {
            _isIdling = false;
            _idleTimer = 0f;
            mob.AnimationQueue = "Walk";

            PickNewWaypoint(mob);
        }

        public override void UpdateState(MobController mob)
        {
            // ── Detection check ──
            if (mob.PlayerDetected)
            {
                if (mob.CanUseHostileStates())
                {
                    mob.SwitchState(mob.chasingState);
                    return;
                }
                else
                {
                    // Passive or unprovoked neutral → flee
                    mob.SwitchState(mob.fleeingState);
                    return;
                }
            }

            // ── Idle at waypoint ──
            if (_isIdling)
            {
                _idleTimer -= Time.deltaTime;
                mob.AnimationQueue = "Idle";

                if (_idleTimer <= 0f)
                {
                    _isIdling = false;
                    PickNewWaypoint(mob);
                }

                return;
            }

            // ── Move toward waypoint ──
            Vector2 currentPos = mob.transform.position;
            Vector2 direction = (_waypoint - currentPos);
            float distance = direction.magnitude;

            if (distance < ArrivalThreshold)
            {
                // Arrived — start idling
                mob.Rb.linearVelocity = Vector2.zero;
                _isIdling = true;
                _idleTimer = mob.Data.roamIdleTime;
                mob.AnimationQueue = "Idle";
                return;
            }

            // Move
            Vector2 moveDir = direction.normalized;
            mob.Rb.linearVelocity = moveDir * mob.Data.moveSpeed;
            mob.FacingDirection = moveDir;
            mob.AnimationQueue = "Walk";
        }

        public override void ExitState(MobController mob)
        {
            mob.Rb.linearVelocity = Vector2.zero;
        }

        private void PickNewWaypoint(MobController mob)
        {
            // Random point within roamRadius of the spawn position
            Vector2 offset = Random.insideUnitCircle * mob.Data.roamRadius;
            _waypoint = (Vector2)mob.SpawnPosition + offset;
        }
    }
}
