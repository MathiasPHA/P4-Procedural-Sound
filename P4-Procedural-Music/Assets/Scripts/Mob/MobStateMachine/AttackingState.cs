using UnityEngine;

namespace MobSystem.States
{
    /// <summary>
    /// Melee attack state with three distinct phases — WINDUP, STRIKE, RECOVERY.
    /// Replaces the old "tick damage on cooldown" model with a telegraphed,
    /// dodge-able attack that creates real combat rhythm.
    ///
    /// PHASES:
    ///
    ///   WINDUP (attackWindupDuration sec):
    ///     Mob stops, commits to attacking. Plays "AttackWindup" animation
    ///     so the player can see the telegraph. For the first portion
    ///     (attackAimLockRatio of the windup) the mob tracks the player;
    ///     after that, direction locks — giving the player a clear signal
    ///     that the attack is committed. If the player runs out of range
    ///     during windup, the attack cancels and the mob returns to chasing.
    ///
    ///   STRIKE (attackStrikeDuration sec):
    ///     The actual damage window. A circular hitbox is placed in front
    ///     of the mob (offset by attackHitboxOffset, radius attackHitboxRadius).
    ///     The mob lunges forward at attackLungeSpeed to close the gap.
    ///     The hitbox is checked continuously — a player caught in it at any
    ///     moment during strike takes damage (once per attack).
    ///     Plays "Attack" animation.
    ///
    ///   RECOVERY (attackRecoveryDuration sec):
    ///     Mob is locked in place, vulnerable. Can't move or attack.
    ///     This is the player's window to counter-attack.
    ///     Plays "AttackRecovery" animation.
    ///
    /// AFTER RECOVERY:
    ///   If player is still in attackRange → start another attack (windup)
    ///   If player moved away → return to ChasingState
    ///
    /// Transitions (can interrupt any phase):
    ///   → ChasingState    when player leaves attackRange*1.5 during windup
    ///   → ChasingState    after recovery if player out of attackRange
    ///   → SearchingState  when PlayerTransform is lost entirely
    ///   → FleeingState    when health drops below fleeHealthThreshold
    /// </summary>
    public class AttackingState : MobBaseState
    {
        private enum Phase { Windup, Strike, Recovery }

        private Phase _phase;
        private float _phaseTimer;
        private bool _hasStruck;
        private Vector2 _lockedDirection;

        public override void EnterState(MobController mob)
        {
            StartWindup(mob);
        }

        public override void UpdateState(MobController mob)
        {
            // ── Health check (applies to all phases) ──
            if (mob.ShouldFlee())
            {
                mob.SwitchState(mob.fleeingState);
                return;
            }

            // ── Lost target entirely ──
            if (mob.Awareness.PlayerTransform == null)
            {
                mob.SwitchState(mob.searchingState);
                return;
            }

            Vector2 mobPos = mob.transform.position;
            Vector2 playerPos = mob.Awareness.PlayerTransform.position;

            switch (_phase)
            {
                case Phase.Windup: UpdateWindup(mob, mobPos, playerPos); break;
                case Phase.Strike: UpdateStrike(mob, mobPos, playerPos); break;
                case Phase.Recovery: UpdateRecovery(mob, mobPos, playerPos); break;
            }
        }

        public override void ExitState(MobController mob)
        {
            // Stop any lunge velocity when leaving the state
            if (mob.Rb != null) mob.Rb.linearVelocity = Vector2.zero;
        }

        // ───────────────────────── Windup ─────────────────────────

        private void StartWindup(MobController mob)
        {
            _phase = Phase.Windup;
            _phaseTimer = mob.Data.attackWindupDuration;
            _hasStruck = false;

            mob.Steering.Stop();
            mob.AnimationQueue = "AttackWindup";

            // Play windup telegraph sound
            if (mob.Data.attackWindupSound != null)
                mob.PlaySound(mob.Data.attackWindupSound, mob.Data.attackWindupVolume);

            // Initialise locked direction to current facing (will be updated during tracking)
            _lockedDirection = (mob.Awareness.PlayerTransform != null)
                ? ((Vector2)mob.Awareness.PlayerTransform.position - (Vector2)mob.transform.position).normalized
                : mob.FacingDirection;
        }

        private void UpdateWindup(MobController mob, Vector2 mobPos, Vector2 playerPos)
        {
            // Cancel if player escapes range during windup
            // 1.5x buffer — commitment has a bit of forgiveness
            float distToPlayer = Vector2.Distance(mobPos, playerPos);
            if (distToPlayer > mob.Data.attackRange * 1.5f)
            {
                mob.SwitchState(mob.chasingState);
                return;
            }

            mob.Steering.Stop();
            mob.AnimationQueue = "AttackWindup";

            // ── Direction: track player for first portion, then lock ──
            float totalWindup = mob.Data.attackWindupDuration;
            float elapsed = totalWindup - _phaseTimer;
            float trackingCutoff = totalWindup * mob.Data.attackAimLockRatio;

            if (elapsed < trackingCutoff)
            {
                // Still tracking — update both facing and locked direction
                Vector2 toPlayer = (playerPos - mobPos).normalized;
                _lockedDirection = toPlayer;
                mob.FacingDirection = toPlayer;
            }
            else
            {
                // Direction locked — player can now dodge by leaving the line of attack
                mob.FacingDirection = _lockedDirection;
            }

            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f)
                StartStrike(mob);
        }

        // ───────────────────────── Strike ─────────────────────────

        private void StartStrike(MobController mob)
        {
            _phase = Phase.Strike;
            _phaseTimer = mob.Data.attackStrikeDuration;

            // Play strike sound (bite, slash — plays even on miss)
            if (mob.Data.attackStrikeSound != null)
                mob.PlaySound(mob.Data.attackStrikeSound, mob.Data.attackStrikeVolume);

            mob.AnimationQueue = "Attack";
            mob.FacingDirection = _lockedDirection;
        }

        private void UpdateStrike(MobController mob, Vector2 mobPos, Vector2 playerPos)
        {
            // Lunge forward (bypasses steering — this is scripted attack motion)
            if (mob.Rb != null && mob.Data.attackLungeSpeed > 0f)
                mob.Rb.linearVelocity = _lockedDirection * mob.Data.attackLungeSpeed;

            mob.AnimationQueue = "Attack";
            mob.FacingDirection = _lockedDirection;

            // ── Hitbox check — continuous during strike until first hit ──
            if (!_hasStruck)
                CheckHitbox(mob);

            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f)
                StartRecovery(mob);
        }

        private void CheckHitbox(MobController mob)
        {
            Vector2 hitboxCenter = (Vector2)mob.transform.position +
                                   _lockedDirection * mob.Data.attackHitboxOffset;

            var hit = Physics2D.OverlapCircle(
                hitboxCenter,
                mob.Data.attackHitboxRadius,
                mob.PlayerLayerMask);

            if (hit == null) return;

            // Found the player in the hitbox — reduce happiness
            _hasStruck = true;

            if (HappinessSystem.Instance != null)
            {
                HappinessSystem.Instance.AdjustHappiness(-mob.Data.happinessPenalty);

                // Attack makes noise — other mobs hear it
                mob.Awareness.HearSound(mob.transform.position, 0.2f);
            }
            else
            {
                Debug.Log($"[MobAttack] {mob.Data.displayName} strikes player for " +
                          $"{mob.Data.happinessPenalty:F2} happiness (no HappinessSystem found)");
            }
        }

        // ───────────────────────── Recovery ─────────────────────────

        private void StartRecovery(MobController mob)
        {
            _phase = Phase.Recovery;
            _phaseTimer = mob.Data.attackRecoveryDuration;

            if (mob.Rb != null) mob.Rb.linearVelocity = Vector2.zero;
            mob.AnimationQueue = "AttackRecovery";
        }

        private void UpdateRecovery(MobController mob, Vector2 mobPos, Vector2 playerPos)
        {
            // Still face the player — mob is committed to being in combat,
            // just can't act. Turning head toward the threat looks alert.
            Vector2 toPlayer = (playerPos - mobPos).normalized;
            mob.FacingDirection = toPlayer;

            mob.Steering.Stop();
            mob.AnimationQueue = "AttackRecovery";

            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f)
                DecideNextAction(mob, mobPos, playerPos);
        }

        private void DecideNextAction(MobController mob, Vector2 mobPos, Vector2 playerPos)
        {
            float distToPlayer = Vector2.Distance(mobPos, playerPos);

            if (distToPlayer <= mob.Data.attackRange)
            {
                // Still in range — another attack
                StartWindup(mob);
            }
            else
            {
                // Player moved away — chase
                mob.SwitchState(mob.chasingState);
            }
        }
    }
}