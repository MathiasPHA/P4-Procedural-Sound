using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Melee attack state. The mob stands near the player and deals damage
    /// on a cooldown timer. Stops moving while attacking.
    ///
    /// Transitions:
    ///   → ChasingState    when player moves out of attackRange
    ///   → FleeingState    when health drops below fleeHealthThreshold
    ///
    /// FUTURE: This is where you'd add attack animations, hit feedback,
    /// and the hook for player health reduction once a player HP system exists.
    /// </summary>
    public class AttackingState : MobBaseState
    {
        private float _cooldownTimer;

        public override void EnterState(MobController mob)
        {
            mob.Rb.linearVelocity = Vector2.zero;
            mob.AnimationQueue = "Attack";

            // Start ready to attack immediately on entry
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
            if (mob.PlayerTransform == null)
            {
                mob.SwitchState(mob.searchingState);
                return;
            }

            Vector2 mobPos = mob.transform.position;
            Vector2 playerPos = mob.PlayerTransform.position;
            float distToPlayer = Vector2.Distance(mobPos, playerPos);

            // ── Player moved out of attack range? Chase them ──
            // Small buffer (1.3x) prevents flickering between attack and chase
            if (distToPlayer > mob.Data.attackRange * 1.3f)
            {
                mob.SwitchState(mob.chasingState);
                return;
            }

            // ── Face the player ──
            Vector2 direction = (playerPos - mobPos).normalized;
            mob.FacingDirection = direction;

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
            // Nothing to clean up — velocity already zero
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
                PlayerHealth.Instance.TakeDamage(mob.Data.attackDamage, mob.transform.position);
            }
            else
            {
                Debug.Log($"[MobAttack] {mob.Data.displayName} hits player for {mob.Data.attackDamage} damage (no PlayerHealth found)");
            }
        }
    }
}