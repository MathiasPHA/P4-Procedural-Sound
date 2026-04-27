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

            // ── Pick seek target and arrival condition ──
            // For horizontal-only attackers (trolls with a sideways-only swing),
            // we target a point beside the player at attackHitboxOffset distance
            // — i.e. exactly where the attack will land. The mob arrives "in range"
            // only when it's actually alongside the player, not when it's close
            // in general. This prevents the classic failure where the mob triggers
            // its attack while still above or below the player and swings at air.
            Vector2 seekTarget;
            bool readyToAttack;

            if (mob.Data.horizontalAttackOnly)
            {
                float dx = mobPos.x - playerPos.x;
                Vector2 sideDir = Mathf.Approximately(dx, 0f)
                    ? (mob.FacingDirection.x < 0f ? Vector2.left : Vector2.right)
                    : (dx > 0f ? Vector2.right : Vector2.left);

                seekTarget = playerPos + sideDir * mob.Data.attackHitboxOffset;

                // Arrival tolerance is half the hitbox radius — tight enough that
                // the mob really is beside the player when it swings, loose enough
                // that minor steering jitter doesn't prevent the transition.
                float arrivalRadius = mob.Data.attackHitboxRadius * 0.5f;
                readyToAttack = Vector2.Distance(mobPos, seekTarget) <= arrivalRadius;
            }
            else
            {
                seekTarget = playerPos;
                readyToAttack = distToPlayer <= mob.Data.attackRange;
            }

            // ── Within attack range? ──
            if (readyToAttack)
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

            // ── Steer toward the chosen seek target ──
            mob.Steering.Seek(seekTarget);
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