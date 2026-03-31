using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Hostile pursuit state. Moves directly toward the player.
    /// Triggers GameStateManager.EnterCombat() on entry so the procedural
    /// music system switches to combat mode.
    ///
    /// Transitions:
    ///   → AttackingState    when within attackRange
    ///   → SearchingState    when player escapes detection range
    ///   → FleeingState      when health drops below fleeHealthThreshold
    /// </summary>
    public class ChasingState : MobBaseState
    {
        // Multiplier beyond detectionRange before giving up the chase.
        // Lets the mob chase slightly past its detection bubble so it
        // doesn't instantly lose the player at the range boundary.
        private const float ChaseLeashMultiplier = 1.3f;

        public override void EnterState(MobController mob)
        {
            mob.AnimationQueue = "Walk";

            // Trigger combat music
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.EnterCombat();
        }

        public override void UpdateState(MobController mob)
        {
            // ── Health check — should we flee? ──
            if (mob.ShouldFlee())
            {
                mob.SwitchState(mob.fleeingState);
                return;
            }

            // ── Do we still have a target? ──
            if (mob.PlayerTransform == null)
            {
                mob.SwitchState(mob.searchingState);
                return;
            }

            Vector2 mobPos = mob.transform.position;
            Vector2 playerPos = mob.PlayerTransform.position;
            float distToPlayer = Vector2.Distance(mobPos, playerPos);

            // ── Within attack range? ──
            if (distToPlayer <= mob.Data.attackRange)
            {
                mob.SwitchState(mob.attackingState);
                return;
            }

            // ── Player escaped? ──
            float leashRange = mob.Data.detectionRange * ChaseLeashMultiplier;
            if (!mob.PlayerDetected && distToPlayer > leashRange)
            {
                mob.SwitchState(mob.searchingState);
                return;
            }

            // ── Move toward player ──
            Vector2 direction = (playerPos - mobPos).normalized;
            mob.Rb.linearVelocity = direction * mob.Data.moveSpeed;
            mob.FacingDirection = direction;
            mob.AnimationQueue = "Walk";
        }

        public override void ExitState(MobController mob)
        {
            mob.Rb.linearVelocity = Vector2.zero;

            // Don't release combat music here — AttackingState and SearchingState
            // keep combat active. Only Roaming and Fleeing release it.
        }
    }
}
