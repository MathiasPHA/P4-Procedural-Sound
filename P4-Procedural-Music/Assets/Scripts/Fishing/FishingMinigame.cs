using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// The "fish" half of the minigame. Bobs vertically within a clamped range,
    /// accumulates fishingPoints whenever the hook overlaps it, and reports the
    /// outcome (caught vs escaped) back to FishingManager when the session ends.
    ///
    /// Replaces FishingV1.cs. Differences from the original:
    ///   - No GameObject.Find — wired via FishingManager.
    ///   - No drops / inventory writes — FishingManager handles that.
    ///   - No parent-disable cleanup — FishingManager toggles the prefab.
    ///   - Sessions explicitly Begin/Abort, so the prefab can be reused.
    ///   - Trigger handler is fixed (no double-counting on entry frame).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FishingMinigame : MonoBehaviour
    {
        [Header("Fishing Time")]
        [Tooltip("Base session duration before timeout. Randomised each session.")]
        [SerializeField] private float baseFishingTime = 8f;
        [Tooltip("Random ± seconds added to baseFishingTime each session.")]
        [SerializeField] private float fishingTimeJitter = 2f;

        [Header("Fish Movement")]
        [SerializeField] private float posClamp = 3f;
        [SerializeField] private float minMoveSpeed = 0.8f;
        [SerializeField] private float maxMoveSpeed = 2f;
        [Tooltip("Distance to target at which the fish picks a new target.")]
        [SerializeField] private float arriveDistance = 0.4f;

        [Header("Catch Tuning")]
        [Tooltip("Fraction of total time the hook must overlap the fish to catch it. " +
                 "0.7 means the player needs to hold the hook on the fish for 70% of " +
                 "the available time.")]
        [Range(0.1f, 1f)][SerializeField] private float catchFraction = 0.7f;

        // ── Public state read by FishingProgressBar ──
        public float fishingPoints { get; private set; }
        public float targetPoints { get; private set; }
        public bool IsActive { get; private set; }

        // ── Session state ──
        private Vector3 _initialFishPos;
        private Vector3 _targetPos;
        private float _moveSpeed;
        private bool _needsNewTarget;
        private float _timeLeft;
        private bool _caught;
        private System.Action<bool> _onSessionEnded;

        /// <summary>
        /// Called by FishingManager.StartFishing. Resets all state and begins
        /// the session at the current transform position (which the manager has
        /// already placed relative to the player).
        /// </summary>
        public void BeginSession(System.Action<bool> onEnded)
        {
            _onSessionEnded = onEnded;
            _initialFishPos = transform.position;
            _targetPos = _initialFishPos;
            _needsNewTarget = true;
            _timeLeft = baseFishingTime + Random.Range(-fishingTimeJitter, fishingTimeJitter);
            _moveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);

            fishingPoints = 0f;
            targetPoints = _timeLeft * catchFraction;
            _caught = false;
            IsActive = true;
        }

        /// <summary>
        /// Called by FishingManager.CancelFishing. Ends the session immediately
        /// as a non-catch and reports back via the same callback as a normal end.
        /// </summary>
        public void AbortSession()
        {
            if (!IsActive) return;
            EndSession(caught: false);
        }

        private void Update()
        {
            if (!IsActive) return;

            UpdateFishMovement();

            _timeLeft -= Time.deltaTime;

            // Win condition: enough overlap accumulated.
            if (fishingPoints >= targetPoints)
            {
                EndSession(caught: true);
                return;
            }

            // Lose condition: time ran out without enough overlap.
            if (_timeLeft <= 0f)
            {
                EndSession(caught: false);
            }
        }

        private void UpdateFishMovement()
        {
            if (_needsNewTarget)
            {
                _targetPos = new Vector3(
                    _initialFishPos.x,
                    _initialFishPos.y + Random.Range(-posClamp, posClamp),
                    _initialFishPos.z);
                _moveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
                _needsNewTarget = false;
                return;
            }

            transform.position = Vector3.Lerp(transform.position, _targetPos, _moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _targetPos) < arriveDistance)
                _needsNewTarget = true;
        }

        // While the hook collider stays inside the fish trigger, accumulate
        // points proportional to the elapsed time. Note: OnTriggerEnter2D is
        // intentionally NOT used — the original double-counted on the entry
        // frame (Enter + Stay both fire that frame).
        private void OnTriggerStay2D(Collider2D other)
        {
            if (!IsActive) return;
            fishingPoints += Time.deltaTime;
        }

        private void EndSession(bool caught)
        {
            if (!IsActive) return;
            IsActive = false;
            _caught = caught;

            var cb = _onSessionEnded;
            _onSessionEnded = null;
            cb?.Invoke(caught);
        }
    }
}