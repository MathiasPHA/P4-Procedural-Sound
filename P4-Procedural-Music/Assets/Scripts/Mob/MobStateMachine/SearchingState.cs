using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Search state for hostile mobs. When the player escapes during a chase,
    /// the mob moves to the player's last known position, wanders briefly,
    /// then gives up. Uses Steering.Seek() for approach and Steering.Wander()
    /// for the searching phase.
    ///
    /// Now integrates with MobAwareness — if the player is re-detected
    /// (awareness spikes), the mob transitions back to chasing.
    ///
    /// Transitions:
    ///   → ChasingState    when awareness reaches full alert (player re-detected)
    ///   → RoamingState    when searchDuration expires (gives up)
    /// </summary>
    public class SearchingState : MobBaseState
    {
        private float _searchTimer;
        private Vector2 _searchCenter;
        private Vector2 _currentTarget;
        private bool _reachedCenter;

        private const float ArrivalThreshold = 10f;

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
            if (mob.Awareness.IsFullyAlert)
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
                    _reachedCenter = true;
                    PickSearchWaypoint(mob);
                }
                else
                {
                    PickSearchWaypoint(mob);
                }
            }

            // Slower movement while searching — scanning the area
            mob.Steering.Seek(_currentTarget, 0.7f);
            mob.AnimationQueue = "Walk";
        }

        public override void ExitState(MobController mob)
        {
            mob.Steering.Stop();

            // If giving up (not re-detecting), release combat music
            if (!mob.Awareness.IsFullyAlert)
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