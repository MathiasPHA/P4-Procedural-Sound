using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Suspicious/alert state. Bridges the gap between Roaming and full commitment
    /// (Chasing or Fleeing). The mob has noticed something but hasn't committed yet.
    ///
    /// Behaviour:
    ///   - Turns to face the stimulus position
    ///   - Pauses briefly, then cautiously approaches (hostile) or backs away (passive)
    ///   - Hostile mobs: slow approach toward the stimulus
    ///   - Passive mobs: slow retreat, ready to bolt
    ///
    /// This creates the creepy folklore feel — a shadow in the trees that
    /// slowly turns toward you before committing to the chase.
    ///
    /// Transitions:
    ///   → ChasingState    when awareness reaches full alert (hostile mobs)
    ///   → FleeingState    when awareness reaches full alert (passive mobs)
    ///   → RoamingState    when awareness decays below suspicious threshold
    /// </summary>
    public class AlertState : MobBaseState
    {
        private float _pauseTimer;
        private bool _isPausing;

        // Brief pause on entry before reacting — the "noticing" moment
        private const float InitialPause = 0.4f;

        public override void EnterState(MobController mob)
        {
            _isPausing = true;
            _pauseTimer = InitialPause;

            mob.Steering.Stop();
            mob.AnimationQueue = "Idle";

            // Face toward the stimulus
            Vector2 toStimulus = (mob.Awareness.LastStimulusPosition - mob.transform.position);
            if (toStimulus.sqrMagnitude > 0.01f)
                mob.FacingDirection = toStimulus.normalized;
        }

        public override void UpdateState(MobController mob)
        {
            // ── Check awareness transitions ──

            // Awareness decayed — false alarm, return to roaming
            if (!mob.Awareness.IsSuspicious)
            {
                mob.SwitchState(mob.roamingState);
                return;
            }

            // Awareness peaked — commit to action
            if (mob.Awareness.IsFullyAlert)
            {
                if (mob.CanUseHostileStates())
                {
                    // Alert the pack before committing
                    mob.PackCoordinator.RaiseAlarm(mob.Awareness.LastStimulusPosition);
                    mob.SwitchState(mob.chasingState);
                }
                else
                {
                    mob.PackCoordinator.RaiseAlarm(mob.Awareness.LastStimulusPosition);
                    mob.SwitchState(mob.fleeingState);
                }
                return;
            }

            // ── Initial pause — the "noticing" beat ──
            if (_isPausing)
            {
                _pauseTimer -= Time.deltaTime;
                mob.Steering.Stop();

                // Keep facing the stimulus
                Vector2 toStimulus = (mob.Awareness.LastStimulusPosition - mob.transform.position);
                if (toStimulus.sqrMagnitude > 0.01f)
                    mob.FacingDirection = toStimulus.normalized;

                if (_pauseTimer <= 0f)
                    _isPausing = false;

                return;
            }

            // ── React based on behaviour type ──
            Vector2 stimulusPos = mob.Awareness.LastStimulusPosition;

            if (mob.CanUseHostileStates())
            {
                // Hostile: cautious approach — slow, deliberate
                mob.Steering.Approach(stimulusPos, 0.4f);
                mob.AnimationQueue = "Walk";
            }
            else
            {
                // Passive: back away slowly — not fleeing yet, just uneasy
                mob.Steering.Flee(stimulusPos, 0.3f);
                mob.AnimationQueue = "Walk";
            }

            // Update facing toward stimulus
            Vector2 dir = (stimulusPos - (Vector2)mob.transform.position);
            if (dir.sqrMagnitude > 0.01f)
                mob.FacingDirection = mob.CanUseHostileStates() ? dir.normalized : -dir.normalized;
        }

        public override void ExitState(MobController mob)
        {
            mob.Steering.Stop();
        }
    }
}
