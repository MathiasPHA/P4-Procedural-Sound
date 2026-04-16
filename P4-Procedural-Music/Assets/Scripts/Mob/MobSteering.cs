using UnityEngine;

namespace MobSystem
{
    /// <summary>
    /// Context-based steering for mobs. Instead of adding competing forces
    /// together (which lets desire overpower avoidance), this system:
    ///
    ///   1. Casts rays in 16 evenly-spaced directions around the mob
    ///   2. Scores each direction with an "interest" value (how close is this
    ///      direction to where I want to go?)
    ///   3. Scores each direction with a "danger" value (obstacles or other
    ///      mobs blocking this direction)
    ///   4. Subtracts danger from interest — blocked directions become negative
    ///   5. Picks the best non-blocked direction and moves that way
    ///
    /// This means a mob chasing the player toward a tree will smoothly
    /// steer around the tree rather than sliding along it, because the
    /// "through the tree" direction is zeroed out and adjacent directions
    /// score highest.
    ///
    /// States still call the same API:
    ///   steering.Seek(target)     — I want to move toward this point
    ///   steering.Flee(threat)     — I want to move away from this point
    ///   steering.Wander()         — I want to drift randomly
    ///   steering.Stop()           — I want to stay put
    ///
    /// SETUP:
    ///   Added automatically by MobController at runtime.
    ///   All tuning values come from MobData.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MobSteering : MonoBehaviour
    {
        // ───────────────────────── Tuning (set by MobController) ─────────────────────────

        private float _maxSpeed = 2f;
        private float _separationRadius = 1.5f;
        private float _separationWeight = 1.2f;
        private float _avoidanceDistance = 2f;
        private float _avoidanceWeight = 1.5f;
        private float _wanderStrength = 0.4f;
        private LayerMask _obstacleMask;
        private LayerMask _mobMask;

        // ───────────────────────── Context Map ─────────────────────────

        // 16 directions gives good resolution without being expensive
        private const int DirectionCount = 16;
        private static readonly Vector2[] Directions = new Vector2[DirectionCount];
        private readonly float[] _interest = new float[DirectionCount];
        private readonly float[] _danger = new float[DirectionCount];

        // ───────────────────────── Runtime ─────────────────────────

        private Rigidbody2D _rb;
        private Vector2 _desiredDirection;
        private float _speedMultiplier = 1f;
        private bool _hasDesire;
        private float _wanderAngle;

        // Collider radius for CircleCast (detected at startup)
        private float _colliderRadius = 0.3f;

        /// <summary>Current facing direction derived from actual movement.</summary>
        public Vector2 FacingDirection { get; private set; } = Vector2.down;

        /// <summary>True when the mob is actively moving.</summary>
        public bool IsMoving => _rb != null && _rb.linearVelocity.sqrMagnitude > 0.01f;

        // ───────────────────────── Static Init ─────────────────────────

        static MobSteering()
        {
            // Pre-calculate the 16 evenly-spaced direction vectors
            for (int i = 0; i < DirectionCount; i++)
            {
                float angle = (360f / DirectionCount) * i * Mathf.Deg2Rad;
                Directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }

        // ───────────────────────── Lifecycle ─────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _wanderAngle = Random.Range(0f, 360f);

            // Get collider radius for CircleCasts
            var col = GetComponent<CircleCollider2D>();
            if (col != null)
                _colliderRadius = col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
            else
            {
                var box = GetComponent<BoxCollider2D>();
                if (box != null)
                    _colliderRadius = Mathf.Max(box.size.x, box.size.y) * 0.4f *
                                      Mathf.Max(transform.localScale.x, transform.localScale.y);
            }
        }

        private void FixedUpdate()
        {
            if (!_hasDesire)
            {
                // No desire — just apply separation nudge so overlapping mobs push apart
                Vector2 sep = CalculateSeparationDirect();
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, sep, Time.fixedDeltaTime * 5f);
                return;
            }

            // ── Build context maps ──
            BuildInterestMap();
            BuildDangerMap();

            // ── Pick best direction ──
            Vector2 chosenDirection = EvaluateContextMap();

            // ── Apply velocity ──
            float currentMax = _maxSpeed * _speedMultiplier;
            Vector2 targetVelocity = chosenDirection * currentMax;

            // Smooth steering so mobs don't snap instantly to new directions
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * 8f);

            // ── Update facing from actual movement ──
            if (_rb.linearVelocity.sqrMagnitude > 0.01f)
                FacingDirection = _rb.linearVelocity.normalized;
        }

        // ───────────────────────── Desire API (called by states) ─────────────────────────

        /// <summary>Move toward a world position.</summary>
        public void Seek(Vector2 target, float speedMultiplier = 1f)
        {
            Vector2 toTarget = target - (Vector2)transform.position;
            _desiredDirection = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Vector2.zero;
            _speedMultiplier = speedMultiplier;
            _hasDesire = _desiredDirection.sqrMagnitude > 0.01f;
        }

        /// <summary>Move away from a world position.</summary>
        public void Flee(Vector2 threat, float speedMultiplier = 1f)
        {
            Vector2 away = (Vector2)transform.position - threat;
            _desiredDirection = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitCircle.normalized;
            _speedMultiplier = speedMultiplier;
            _hasDesire = true;
        }

        /// <summary>Drift randomly with organic-feeling curves.</summary>
        public void Wander(float speedMultiplier = 1f)
        {
            _wanderAngle += Random.Range(-45f, 45f) * Time.deltaTime;
            float rad = _wanderAngle * Mathf.Deg2Rad;
            _desiredDirection = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _speedMultiplier = speedMultiplier * _wanderStrength;
            _hasDesire = true;
        }

        /// <summary>Zero desire. Separation still nudges overlapping mobs apart.</summary>
        public void Stop()
        {
            _desiredDirection = Vector2.zero;
            _speedMultiplier = 1f;
            _hasDesire = false;
        }

        /// <summary>Cautious approach — slow seek used by AlertState.</summary>
        public void Approach(Vector2 target, float speedMultiplier = 0.4f)
        {
            Seek(target, speedMultiplier);
        }

        // ───────────────────────── Context Map: Interest ─────────────────────────

        private void BuildInterestMap()
        {
            for (int i = 0; i < DirectionCount; i++)
            {
                // Interest = how aligned this direction is with where we want to go
                // Dot product: 1.0 = same direction, 0 = perpendicular, -1 = opposite
                float dot = Vector2.Dot(Directions[i], _desiredDirection);

                // Remap from [-1, 1] to [0, 1] so even sideways directions get some interest
                // This lets the mob pick a diagonal path around obstacles
                _interest[i] = Mathf.Max(0f, (dot + 1f) * 0.5f);
            }
        }

        // ───────────────────────── Context Map: Danger ─────────────────────────

        private void BuildDangerMap()
        {
            Vector2 pos = transform.position;

            for (int i = 0; i < DirectionCount; i++)
            {
                float danger = 0f;

                // ── Obstacle danger: CircleCast in this direction ──
                var hit = Physics2D.CircleCast(
                    pos,
                    _colliderRadius,
                    Directions[i],
                    _avoidanceDistance,
                    _obstacleMask
                );

                if (hit.collider != null)
                {
                    // Closer obstacles = more danger (1.0 at contact, 0.0 at max distance)
                    float proximity = 1f - Mathf.Clamp01(hit.distance / _avoidanceDistance);
                    danger = proximity * _avoidanceWeight;
                }

                // ── Mob danger: nearby mobs make this direction dangerous ──
                danger += CalculateMobDangerInDirection(pos, Directions[i]);

                _danger[i] = danger;
            }
        }

        private float CalculateMobDangerInDirection(Vector2 pos, Vector2 direction)
        {
            if (_separationRadius <= 0f) return 0f;

            float totalDanger = 0f;

            var hits = Physics2D.OverlapCircleAll(pos, _separationRadius, _mobMask);

            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;

                Vector2 toMob = (Vector2)hit.transform.position - pos;
                float dist = toMob.magnitude;

                if (dist < 0.01f)
                {
                    // Nearly overlapping — every direction has some danger
                    totalDanger += _separationWeight * 0.3f;
                    continue;
                }

                // How aligned is this direction with the direction TO the other mob?
                float dot = Vector2.Dot(direction, toMob.normalized);

                if (dot <= 0f) continue; // Other mob is behind us — no danger

                // Closer mobs = more danger, and more aligned = more danger
                float proximity = 1f - Mathf.Clamp01(dist / _separationRadius);
                totalDanger += proximity * dot * _separationWeight;
            }

            return totalDanger;
        }

        // ───────────────────────── Context Map: Evaluation ─────────────────────────

        private Vector2 EvaluateContextMap()
        {
            float bestScore = float.NegativeInfinity;
            int bestIndex = 0;

            float secondBestScore = float.NegativeInfinity;
            int secondBestIndex = 0;

            for (int i = 0; i < DirectionCount; i++)
            {
                // Final score = interest minus danger
                // Blocked directions go negative and are never chosen
                float score = _interest[i] - _danger[i];

                if (score > bestScore)
                {
                    secondBestScore = bestScore;
                    secondBestIndex = bestIndex;
                    bestScore = score;
                    bestIndex = i;
                }
                else if (score > secondBestScore)
                {
                    secondBestScore = score;
                    secondBestIndex = i;
                }
            }

            // If everything is blocked, back away from the densest danger
            if (bestScore <= 0f)
                return -FindDensestDangerDirection();

            // Blend between the two best directions for smoother paths
            if (secondBestScore > 0f)
            {
                float totalScore = bestScore + secondBestScore;
                float bestWeight = bestScore / totalScore;
                float secondWeight = secondBestScore / totalScore;

                Vector2 blended = Directions[bestIndex] * bestWeight +
                                  Directions[secondBestIndex] * secondWeight;
                return blended.normalized;
            }

            return Directions[bestIndex];
        }

        /// <summary>
        /// When all directions are blocked, find which direction has the most
        /// danger and move opposite to it (back out of a corner).
        /// </summary>
        private Vector2 FindDensestDangerDirection()
        {
            float maxDanger = 0f;
            Vector2 dangerDir = Vector2.zero;

            for (int i = 0; i < DirectionCount; i++)
            {
                if (_danger[i] > maxDanger)
                {
                    maxDanger = _danger[i];
                    dangerDir = Directions[i];
                }
            }

            return dangerDir.sqrMagnitude > 0.01f ? dangerDir.normalized : Random.insideUnitCircle.normalized;
        }

        // ───────────────────────── Direct Separation (for Stop state) ─────────────────────────

        /// <summary>
        /// Simple direct separation force used when the mob has no desire.
        /// Prevents mobs from stacking when they're all idling at the same spot.
        /// </summary>
        private Vector2 CalculateSeparationDirect()
        {
            if (_separationRadius <= 0f) return Vector2.zero;

            Vector2 force = Vector2.zero;
            int neighbours = 0;

            var hits = Physics2D.OverlapCircleAll(transform.position, _separationRadius, _mobMask);

            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;

                Vector2 away = (Vector2)transform.position - (Vector2)hit.transform.position;
                float dist = away.magnitude;

                if (dist < 0.01f)
                {
                    force += Random.insideUnitCircle.normalized * _separationWeight;
                    neighbours++;
                    continue;
                }

                float strength = 1f - Mathf.Clamp01(dist / _separationRadius);
                force += away.normalized * strength * _separationWeight;
                neighbours++;
            }

            if (neighbours > 0)
                force /= neighbours;

            // Clamp so stopped mobs don't rocket away
            float maxSepSpeed = _maxSpeed * 0.5f;
            if (force.sqrMagnitude > maxSepSpeed * maxSepSpeed)
                force = force.normalized * maxSepSpeed;

            return force;
        }

        // ───────────────────────── Configuration ─────────────────────────

        /// <summary>Set all tuning values. Called once by MobController.</summary>
        public void Configure(
            float maxSpeed,
            float separationRadius,
            float separationWeight,
            float avoidanceDistance,
            float avoidanceWeight,
            float wanderStrength,
            LayerMask obstacleMask,
            LayerMask mobMask)
        {
            _maxSpeed = maxSpeed;
            _separationRadius = separationRadius;
            _separationWeight = separationWeight;
            _avoidanceDistance = avoidanceDistance;
            _avoidanceWeight = avoidanceWeight;
            _wanderStrength = wanderStrength;
            _obstacleMask = obstacleMask;
            _mobMask = mobMask;
        }

        /// <summary>Update max speed at runtime.</summary>
        public void SetMaxSpeed(float speed)
        {
            _maxSpeed = speed;
        }

        // ───────────────────────── Debug ─────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Separation radius
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _separationRadius);

            // Avoidance distance
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, _avoidanceDistance);

            if (!Application.isPlaying) return;

            // Draw context map as coloured rays
            Vector2 pos = transform.position;
            for (int i = 0; i < DirectionCount; i++)
            {
                float score = _interest[i] - _danger[i];
                float length = Mathf.Abs(score) * _avoidanceDistance;

                if (score > 0f)
                {
                    // Green = safe/interesting direction
                    Gizmos.color = new Color(0f, 1f, 0f, 0.4f * score);
                    Gizmos.DrawLine(pos, pos + Directions[i] * length);
                }
                else
                {
                    // Red = dangerous/blocked direction
                    Gizmos.color = new Color(1f, 0f, 0f, 0.4f * Mathf.Abs(score));
                    Gizmos.DrawLine(pos, pos + Directions[i] * length);
                }
            }

            // Desired direction (cyan)
            if (_hasDesire)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pos, pos + _desiredDirection * 1.5f);
            }

            // Actual velocity (white)
            if (_rb != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(pos, pos + _rb.linearVelocity.normalized * 1f);
            }
        }
#endif
    }
}