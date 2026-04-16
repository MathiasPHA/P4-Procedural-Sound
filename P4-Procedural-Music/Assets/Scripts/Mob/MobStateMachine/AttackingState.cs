using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Melee attack state. The mob stops moving and deals damage on a
    /// cooldown timer. Uses Steering.Stop() so separation still applies —
    /// two wolves attacking the same player will nudge apart naturally.
    ///
    /// Transitions:
    ///   → ChasingState    when player moves out of attackRange
    ///   → FleeingState    when health drops below fleeHealthThreshold
    /// </summary>
    public class AttackingState : MobBaseState
    {
        private float _cooldownTimer;

        public override void EnterState(MobController mob)
        {
            mob.Steering.Stop();
            mob.AnimationQueue = "Attack";

            // Ready to attack immediately on entry
            _cooldownTimer = 0f;
        }

        public override void UpdateState(MobController mob)
        {
            // ── Health check ──
            if (mob.ShouldFlee())
            {
                mob.SwitchState(mob.fleeingState);
                return;
            }

            // ── Target still valid? ──
            if (mob.Awareness.PlayerTransform == null)
            {
                mob.SwitchState(mob.searchingState);
                return;
            }

            Vector2 mobPos = mob.transform.position;
            Vector2 playerPos = mob.Awareness.PlayerTransform.position;
            float distToPlayer = Vector2.Distance(mobPos, playerPos);

            // ── Player moved out of attack range? Chase them ──
            // 1.3x buffer prevents flickering between attack and chase
            if (distToPlayer > mob.Data.attackRange * 1.3f)
            {
                mob.SwitchState(mob.chasingState);
                return;
            }

            // ── Face the player ──
            Vector2 direction = (playerPos - mobPos).normalized;
            mob.FacingDirection = direction;

            // Keep steering stopped (separation still applies)
            mob.Steering.Stop();

            // ── Attack on cooldown ──
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                PerformAttack(mob);
                _cooldownTimer = mob.Data.attackCooldown;
            }
        }

        public override void ExitState(MobController mob)
        {
            // Nothing to clean up
        }

        private void PerformAttack(MobController mob)
        {
            // Play attack sound
            if (mob.Data.attackSound != null)
                mob.PlaySound(mob.Data.attackSound, mob.Data.attackVolume);

            mob.AnimationQueue = "Attack";

            // ── Deal damage to player ──
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.TakeDamage(
                    mob.Data.attackDamage,
                    mob.Data.happinessPenalty,
                    mob.transform.position);

                // The attack makes noise — alert nearby mobs
                mob.Awareness.HearSound(mob.transform.position, 0.2f);
            }
            else
            {
                Debug.Log($"[MobAttack] {mob.Data.displayName} hits player for " +
                          $"{mob.Data.attackDamage} damage (no PlayerHealth found)");
            }
        }
    }
}
