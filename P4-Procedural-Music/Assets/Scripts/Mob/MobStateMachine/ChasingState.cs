using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Hostile pursuit state. The mob moves toward the player using
    /// MobSteering.Seek(), so separation and avoidance apply automatically.
    ///
    /// Wolf packs naturally fan out during a chase because each wolf's
    /// separation force pushes it away from its neighbours while still
    /// seeking the same target.
    ///
    /// Transitions:
    ///   → AttackingState    when within attackRange
    ///   → SearchingState    when player escapes detection (awareness drops)
    ///   → FleeingState      when health drops below fleeHealthThreshold
    /// </summary>
    public class ChasingState : MobBaseState
    {
        // Multiplier beyond detectionRange before giving up.
        // Lets the mob chase slightly past its detection bubble.
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
            if (mob.Awareness.PlayerTransform == null)
            {
                mob.SwitchState(mob.searchingState);
                return;
            }

            Vector2 mobPos = mob.transform.position;
            Vector2 playerPos = mob.Awareness.PlayerTransform.position;
            float distToPlayer = Vector2.Distance(mobPos, playerPos);

            // ── Within attack range? ──
            if (distToPlayer <= mob.Data.attackRange)
            {
                mob.SwitchState(mob.attackingState);
                return;
            }

            // ── Player escaped detection? ──
            float leashRange = mob.Data.detectionRange * ChaseLeashMultiplier;
            if (!mob.Awareness.PlayerInRange && distToPlayer > leashRange)
            {
                mob.SwitchState(mob.searchingState);
                return;
            }

            // ── Steer toward player ──
            mob.Steering.Seek(playerPos);
            mob.AnimationQueue = "Walk";

            // Update last known position for searching
            mob.UpdateLastKnownPlayerPos(playerPos);
        }

        public override void ExitState(MobController mob)
        {
            mob.Steering.Stop();
            // Don't release combat music — AttackingState and SearchingState keep it active
        }
    }
}
