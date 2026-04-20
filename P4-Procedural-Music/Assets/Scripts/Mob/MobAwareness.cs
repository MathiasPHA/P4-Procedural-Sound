using UnityEngine;

namespace MobSystem
{
    /// <summary>
    /// Replaces binary player detection with a gradual awareness model.
    /// Instead of "detected / not detected", mobs build awareness over time
    /// based on distance and stimulus type.
    ///
    /// AWARENESS LEVELS:
    ///   0.0         — Unaware (roaming normally)
    ///   0.0 → 0.5   — Building awareness (no visible change yet)
    ///   0.5         — Suspicious threshold (mob turns toward stimulus, slows down)
    ///   0.5 → 1.0   — Growing suspicion (AlertState — cautious approach/hesitation)
    ///   1.0         — Fully alert (commits to chasing or fleeing)
    ///
    /// STIMULI:
    ///   Visual   — player within detection range, builds fast at close range
    ///   Auditory — combat sounds, player sprinting, builds slower but works through obstacles
    ///   Alert    — pack member raised the alarm, instant boost to a configurable level
    ///
    /// DECAY:
    ///   When no stimulus is active, awareness decays toward zero.
    ///   Decay is slower than buildup so mobs don't instantly forget.
    ///
    /// SETUP:
    ///   Added automatically by MobController. All tuning from MobData.
    ///   MobController reads Awareness, IsSuspicious, IsFullyAlert each frame
    ///   to drive state transitions.
    /// </summary>
    public class MobAwareness : MonoBehaviour
    {
        // ───────────────────────── Tuning (set by MobController) ─────────────────────────

        private float _detectionRange = 5f;
        private float _hearingRange = 8f;
        private float _visualGainRate = 1.5f;     // Awareness/sec at point-blank
        private float _auditoryGainRate = 0.6f;    // Awareness/sec from sounds
        private float _decayRate = 0.4f;           // Awareness/sec when no stimulus
        private float _suspiciousThreshold = 0.5f;
        private float _alertThreshold = 1f;
        private LayerMask _playerLayer;

        // ───────────────────────── Runtime ─────────────────────────

        private float _awareness;
        private float _detectionTimer;
        private float _detectionInterval = 0.25f;
        private float _detectionOffset;
        private Transform _playerTransform;
        private Vector3 _lastStimulusPosition;
        private bool _hasStimulus;

        // ───────────────────────── Public Read ─────────────────────────

        /// <summary>Current awareness level (0–1).</summary>
        public float Awareness => _awareness;

        /// <summary>True when awareness has crossed the suspicious threshold.</summary>
        public bool IsSuspicious => _awareness >= _suspiciousThreshold;

        /// <summary>True when awareness has reached full alert.</summary>
        public bool IsFullyAlert => _awareness >= _alertThreshold;

        /// <summary>True when the player is currently within visual detection range.</summary>
        public bool PlayerInRange { get; private set; }

        /// <summary>Cached player transform. Null until first detection.</summary>
        public Transform PlayerTransform => _playerTransform;

        /// <summary>Position of the most recent stimulus (visual or auditory).</summary>
        public Vector3 LastStimulusPosition => _lastStimulusPosition;

        // ───────────────────────── Lifecycle ─────────────────────────

        private void Start()
        {
            // Stagger detection checks across mobs
            _detectionOffset = Random.Range(0f, _detectionInterval);
            _detectionTimer = _detectionOffset;
        }

        private void Update()
        {
            // ── Staggered detection tick ──
            _detectionTimer -= Time.deltaTime;
            if (_detectionTimer <= 0f)
            {
                RunDetection();
                _detectionTimer = _detectionInterval;
            }

            // ── Update awareness ──
            if (_hasStimulus)
            {
                // Build awareness — rate depends on stimulus distance
                float gain = CalculateGainRate();
                _awareness = Mathf.MoveTowards(_awareness, _alertThreshold, gain * Time.deltaTime);
            }
            else
            {
                // Decay awareness when no stimulus
                _awareness = Mathf.MoveTowards(_awareness, 0f, _decayRate * Time.deltaTime);
            }

            _awareness = Mathf.Clamp01(_awareness);
        }

        // ───────────────────────── Detection ─────────────────────────

        private void RunDetection()
        {
            // Visual detection — OverlapCircle for the player
            var hit = Physics2D.OverlapCircle(transform.position, _detectionRange, _playerLayer);

            if (hit != null)
            {
                PlayerInRange = true;
                _playerTransform = hit.transform;
                _lastStimulusPosition = hit.transform.position;
                _hasStimulus = true;
            }
            else
            {
                PlayerInRange = false;
                _hasStimulus = false;
                // Keep _playerTransform and _lastStimulusPosition for searching
            }
        }

        private float CalculateGainRate()
        {
            if (_playerTransform == null) return _auditoryGainRate;

            float dist = Vector2.Distance(transform.position, _playerTransform.position);

            // Closer = faster awareness buildup (inverse distance, clamped)
            float proximityFactor = 1f - Mathf.Clamp01(dist / _detectionRange);

            // Base visual rate scaled by proximity — point-blank builds fastest
            return Mathf.Lerp(_auditoryGainRate, _visualGainRate, proximityFactor);
        }

        // ───────────────────────── External Stimuli ─────────────────────────

        /// <summary>
        /// Hear a sound from a specific position (combat, player sprinting, etc).
        /// Boosts awareness by a flat amount if within hearing range.
        /// </summary>
        public void HearSound(Vector3 soundPosition, float intensity = 0.3f)
        {
            float dist = Vector2.Distance(transform.position, soundPosition);
            if (dist > _hearingRange) return;

            // Intensity falls off with distance
            float falloff = 1f - (dist / _hearingRange);
            float boost = intensity * falloff;

            _awareness = Mathf.Clamp01(_awareness + boost);
            _lastStimulusPosition = soundPosition;

            // If we don't have a player ref yet, try to find one
            if (_playerTransform == null)
            {
                var hit = Physics2D.OverlapCircle(soundPosition, 1f, _playerLayer);
                if (hit != null)
                    _playerTransform = hit.transform;
            }
        }

        /// <summary>
        /// Pack alert — another mob directly raises this mob's awareness.
        /// Used by MobPackCoordinator when a pack member detects the player.
        /// </summary>
        public void AlertFromPack(Vector3 threatPosition, float awarenessBoost = 0.7f)
        {
            _awareness = Mathf.Clamp01(_awareness + awarenessBoost);
            _lastStimulusPosition = threatPosition;

            // Try to acquire player reference from alert position
            if (_playerTransform == null)
            {
                var hit = Physics2D.OverlapCircle(threatPosition, 2f, _playerLayer);
                if (hit != null)
                    _playerTransform = hit.transform;
            }
        }

        /// <summary>
        /// Force full awareness immediately. Used when a mob takes damage —
        /// getting hit always means you know exactly where the threat is.
        /// </summary>
        public void ForceFullAlert(Vector3 attackerPosition, Transform attackerTransform = null)
        {
            _awareness = _alertThreshold;
            _lastStimulusPosition = attackerPosition;
            _hasStimulus = true;

            if (attackerTransform != null)
                _playerTransform = attackerTransform;
        }

        /// <summary>
        /// Reset awareness to zero. Used when a mob returns to roaming
        /// after a search timeout.
        /// </summary>
        public void ResetAwareness()
        {
            _awareness = 0f;
            _hasStimulus = false;
            PlayerInRange = false;
        }

        // ───────────────────────── Configuration ─────────────────────────

        /// <summary>Set all tuning values. Called once by MobController.</summary>
        public void Configure(
            float detectionRange,
            float hearingRange,
            float visualGainRate,
            float auditoryGainRate,
            float decayRate,
            float suspiciousThreshold,
            float alertThreshold,
            float detectionInterval,
            LayerMask playerLayer)
        {
            _detectionRange = detectionRange;
            _hearingRange = hearingRange;
            _visualGainRate = visualGainRate;
            _auditoryGainRate = auditoryGainRate;
            _decayRate = decayRate;
            _suspiciousThreshold = suspiciousThreshold;
            _alertThreshold = alertThreshold;
            _detectionInterval = detectionInterval;
            _playerLayer = playerLayer;
        }

        // ───────────────────────── Debug ─────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Visual detection range
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _detectionRange);

            // Hearing range
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, _hearingRange);

            // Awareness bar (editor only visual)
            if (Application.isPlaying)
            {
                Vector3 barStart = transform.position + Vector3.up * 1.5f + Vector3.left * 0.5f;
                Vector3 barEnd = barStart + Vector3.right * _awareness;

                Color barColor = _awareness < _suspiciousThreshold
                    ? Color.green
                    : _awareness < _alertThreshold
                        ? Color.yellow
                        : Color.red;

                Gizmos.color = barColor;
                Gizmos.DrawLine(barStart, barEnd);

                // Last stimulus position
                if (_hasStimulus)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                    Gizmos.DrawLine(transform.position, _lastStimulusPosition);
                }
            }
        }
#endif
    }
}
