using UnityEngine;

namespace MobSystem
{
    /// <summary>
    /// Handles pack coordination between mobs of the same type.
    /// When a mob becomes fully alert (enters chasing or fleeing),
    /// it notifies nearby mobs of the same type so they react together.
    ///
    /// HOW IT WORKS:
    ///   1. MobController calls RaiseAlarm() when transitioning to chase/flee
    ///   2. This does an OverlapCircle for nearby mobs on the mob layer
    ///   3. Same-type mobs get their MobAwareness boosted toward alert
    ///   4. Different-type mobs are ignored (wolves don't alert rabbits)
    ///
    /// BEHAVIOURS:
    ///   Wolves  → pack chases together, flanking emerges from separation steering
    ///   Rabbits → herd scatters together, one rabbit's panic triggers the group
    ///   Boars   → neutral until one is hit, then nearby boars aggro together
    ///
    /// The pack alert has a cooldown per mob to prevent alert loops
    /// (mob A alerts mob B, mob B tries to alert mob A, etc).
    ///
    /// SETUP:
    ///   Added automatically by MobController. Tuning from MobData.
    /// </summary>
    public class MobPackCoordinator : MonoBehaviour
    {
        // ───────────────────────── Tuning ─────────────────────────

        private float _alertRadius = 10f;
        private float _alertAwarenessBoost = 0.7f;
        private LayerMask _mobMask;
        private string _mobTypeId;

        // ───────────────────────── Runtime ─────────────────────────

        private float _alertCooldown;
        private const float AlertCooldownDuration = 2f; // Prevent alert spam

        // ───────────────────────── Public API ─────────────────────────

        /// <summary>
        /// Broadcast an alarm to nearby mobs of the same type.
        /// Called by MobController when entering an alert state (chase, flee).
        /// </summary>
        public void RaiseAlarm(Vector3 threatPosition)
        {
            // Cooldown prevents alert cascades
            if (_alertCooldown > 0f) return;
            _alertCooldown = AlertCooldownDuration;

            var hits = Physics2D.OverlapCircleAll(transform.position, _alertRadius, _mobMask);

            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;

                // Must be same mob type
                var otherMob = hit.GetComponent<MobController>();
                if (otherMob == null) continue;
                if (otherMob.Data == null || otherMob.Data.id != _mobTypeId) continue;

                // Boost their awareness
                var otherAwareness = hit.GetComponent<MobAwareness>();
                if (otherAwareness != null)
                {
                    otherAwareness.AlertFromPack(threatPosition, _alertAwarenessBoost);
                }
            }
        }

        /// <summary>
        /// Provoke nearby mobs of the same type. Used for Neutral mobs —
        /// when one boar is attacked, nearby boars become hostile too.
        /// </summary>
        public void ProvokeNearby(Vector3 attackerPosition)
        {
            if (_alertCooldown > 0f) return;
            _alertCooldown = AlertCooldownDuration;

            var hits = Physics2D.OverlapCircleAll(transform.position, _alertRadius, _mobMask);

            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;

                var otherMob = hit.GetComponent<MobController>();
                if (otherMob == null) continue;
                if (otherMob.Data == null || otherMob.Data.id != _mobTypeId) continue;

                // Provoke and alert
                otherMob.ProvokeFromPack(attackerPosition);
            }
        }

        // ───────────────────────── Lifecycle ─────────────────────────

        private void Update()
        {
            if (_alertCooldown > 0f)
                _alertCooldown -= Time.deltaTime;
        }

        // ───────────────────────── Configuration ─────────────────────────

        /// <summary>Set tuning values. Called once by MobController.</summary>
        public void Configure(string mobTypeId, float alertRadius, float alertAwarenessBoost, LayerMask mobMask)
        {
            _mobTypeId = mobTypeId;
            _alertRadius = alertRadius;
            _alertAwarenessBoost = alertAwarenessBoost;
            _mobMask = mobMask;
        }

        // ───────────────────────── Debug ─────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, _alertRadius);
        }
#endif
    }
}
