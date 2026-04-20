using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Default idle state. The mob picks random waypoints near its spawn
    /// position, walks to them, pauses, and repeats.
    ///
    /// Detection is now handled by MobAwareness — this state checks
    /// IsSuspicious to transition to AlertState instead of jumping
    /// straight to chasing/fleeing.
    ///
    /// Movement goes through MobSteering, so separation and obstacle
    /// avoidance apply automatically even while roaming.
    ///
    /// Transitions:
    ///   → AlertState    when awareness crosses the suspicious threshold
    /// </summary>
    public class RoamingState : MobBaseState
    {
        private Vector2 _waypoint;
        private float _idleTimer;
        private bool _isIdling;

        private const float ArrivalThreshold = 6f;

        public override void EnterState(MobController mob)
        {
            _isIdling = false;
            _idleTimer = 0f;
            mob.AnimationQueue = "Walk";

            // Reset awareness so we start clean after returning from search/flee
            mob.Awareness.ResetAwareness();

            PickNewWaypoint(mob);
        }

        public override void UpdateState(MobController mob)
        {
            // ── Awareness check — something caught our attention? ──
            if (mob.Awareness.IsSuspicious)
            {
                mob.SwitchState(mob.alertState);
                return;
            }

            // ── Idle at waypoint ──
            if (_isIdling)
            {
                _idleTimer -= Time.deltaTime;
                mob.Steering.Stop();
                mob.AnimationQueue = "Idle";

                if (_idleTimer <= 0f)
                {
                    _isIdling = false;
                    PickNewWaypoint(mob);
                }

                return;
            }

            // ── Move toward waypoint via steering ──
            Vector2 currentPos = mob.transform.position;
            float distance = Vector2.Distance(currentPos, _waypoint);

            if (distance < ArrivalThreshold)
            {
                // Arrived — start idling
                mob.Steering.Stop();
                _isIdling = true;
                _idleTimer = mob.Data.roamIdleTime;
                mob.AnimationQueue = "Idle";
                return;
            }

            mob.Steering.Seek(_waypoint);
            mob.AnimationQueue = "Walk";
        }

        public override void ExitState(MobController mob)
        {
            mob.Steering.Stop();
        }

        private void PickNewWaypoint(MobController mob)
        {
            Vector2 offset = Random.insideUnitCircle * mob.Data.roamRadius;
            _waypoint = (Vector2)mob.SpawnPosition + offset;
        }
    }
}