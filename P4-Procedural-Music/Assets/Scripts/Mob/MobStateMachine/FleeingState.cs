using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Flee state. The mob moves away from the threat using MobSteering.Flee(),
    /// which adds slight random drift and blends with separation and avoidance.
    ///
    /// A group of fleeing rabbits naturally scatters because each one's
    /// separation force pushes it away from neighbours while all flee
    /// the same threat — emergent herd scatter with no special code.
    ///
    /// Transitions:
    ///   → RoamingState     when distance to threat exceeds fleeDistance
    /// </summary>
    public class FleeingState : MobBaseState
    {
        private Vector3 _threatPosition;

        public override void EnterState(MobController mob)
        {
            mob.AnimationQueue = "Walk";

            // What are we running from?
            _threatPosition = mob.Awareness.LastStimulusPosition;

            // Alert the pack — rabbits scatter together
            mob.PackCoordinator.RaiseAlarm(_threatPosition);
        }

        public override void UpdateState(MobController mob)
        {
            // ── Update threat position if player is still visible ──
            if (mob.Awareness.PlayerInRange && mob.Awareness.PlayerTransform != null)
            {
                _threatPosition = mob.Awareness.PlayerTransform.position;
            }

            // ── Check if we've reached safety ──
            float distToThreat = Vector2.Distance(mob.transform.position, _threatPosition);
            if (distToThreat >= mob.Data.fleeDistance)
            {
                mob.SwitchState(mob.roamingState);
                return;
            }

            // ── Flee via steering ──
            mob.Steering.Flee(_threatPosition, mob.Data.fleeSpeedMultiplier);
            mob.AnimationQueue = "Walk";
        }

        public override void ExitState(MobController mob)
        {
            mob.Steering.Stop();

            // Release combat music — the encounter is over
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsManualOverride)
                GameStateManager.Instance.ReturnToAuto();
        }
    }
}
