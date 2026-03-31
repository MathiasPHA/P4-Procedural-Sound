using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Flee state. The mob moves directly away from the threat (player)
    /// at boosted speed until it reaches a safe distance.
    ///
    /// Used by:
    ///   Passive mobs      → always flee when player detected
    ///   Neutral (unprovoked) → flee like passive
    ///   Hostile (low HP)   → flee when health below threshold
    ///
    /// Transitions:
    ///   → RoamingState     when distance to threat exceeds fleeDistance
    ///
    /// On exit, releases combat music back to auto mode so the procedural
    /// music system can return to the ambient tension-based state.
    /// </summary>
    public class FleeingState : MobBaseState
    {
        private Vector2 _fleeDirection;
        private Vector3 _threatPosition;

        public override void EnterState(MobController mob)
        {
            mob.AnimationQueue = "Walk";

            // Determine what we're running from
            _threatPosition = mob.LastKnownPlayerPos;

            // Calculate initial flee direction (away from threat)
            UpdateFleeDirection(mob);
        }

        public override void UpdateState(MobController mob)
        {
            // ── Update threat position if player is still detected ──
            if (mob.PlayerDetected && mob.PlayerTransform != null)
            {
                _threatPosition = mob.PlayerTransform.position;
                UpdateFleeDirection(mob);
            }

            // ── Check if we've reached safety ──
            float distToThreat = Vector2.Distance(mob.transform.position, _threatPosition);
            if (distToThreat >= mob.Data.fleeDistance)
            {
                mob.SwitchState(mob.roamingState);
                return;
            }

            // ── Move away ──
            float fleeSpeed = mob.Data.moveSpeed * mob.Data.fleeSpeedMultiplier;
            mob.Rb.linearVelocity = _fleeDirection * fleeSpeed;
            mob.FacingDirection = _fleeDirection;
            mob.AnimationQueue = "Walk";
        }

        public override void ExitState(MobController mob)
        {
            mob.Rb.linearVelocity = Vector2.zero;

            // Release combat music — the encounter is over
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsManualOverride)
                GameStateManager.Instance.ReturnToAuto();
        }

        private void UpdateFleeDirection(MobController mob)
        {
            Vector2 away = ((Vector2)mob.transform.position - (Vector2)_threatPosition).normalized;

            // Add a slight random offset so mobs don't all flee in an identical line
            away += Random.insideUnitCircle * 0.2f;
            _fleeDirection = away.normalized;
        }
    }
}
