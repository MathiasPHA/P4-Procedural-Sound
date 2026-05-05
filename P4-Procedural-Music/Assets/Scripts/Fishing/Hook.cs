using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingSystem
{
    /// <summary>
    /// The player-controlled "reel" half of the minigame, with Stardew-style
    /// physics. Gravity pulls the hook downward; holding left-click applies a
    /// continuous upward thrust. Velocity is damped each frame so the hook
    /// doesn't oscillate forever, and hitting the top/bottom of the travel
    /// range produces a soft bounce that decays quickly.
    ///
    /// All movement is in LOCAL space so the hook respects the parent
    /// prefab's scale (the FishingSystem prefab is scaled up 6x in Marcus's
    /// scene, so world-space movement of small clamp values would have only
    /// spanned a tiny fraction of the visible bar).
    /// </summary>
    public class Hook : MonoBehaviour
    {
        [Header("Vertical Range (local Y, relative to start position)")]
        [Tooltip("Distance the hook can travel up or down from its starting localPosition. " +
                 "Expressed in LOCAL units — i.e. before parent scale.")]
        [SerializeField] private float clampY = 2.55f;

        [Header("Physics")]
        [Tooltip("Constant downward acceleration applied each frame (local units / sec²). " +
                 "Larger = hook falls faster when not thrusting.")]
        [SerializeField] private float gravity = 12f;

        [Tooltip("Upward acceleration applied while left mouse is held (local units / sec²). " +
                 "Should be larger than gravity so the hook can rise when held.")]
        [SerializeField] private float thrust = 27f;

        [Tooltip("Per-second velocity damping. 0 = no damping (oscillates forever), " +
                 "higher = velocity bleeds off faster. Around 1–3 feels controllable.")]
        [SerializeField] private float damping = 1.5f;

        [Tooltip("Velocity multiplier when bouncing off the top/bottom edge. " +
                 "0 = stops dead, 1 = perfect bounce, ~0.3 feels soft and dampens quickly.")]
        [Range(0f, 1f)][SerializeField] private float bounceCoefficient = 0.3f;

        [Tooltip("Max velocity in either direction (local units / sec). Stops the hook " +
                 "from getting absurd if held against gravity for a long time.")]
        [SerializeField] private float maxSpeed = 7f;

        private Vector3 _startLocal;
        private float _minY, _maxY;
        private float _velocity; // local units / second, +up
        private bool _active;
        private bool _restCaptured;

        private void Awake()
        {
            // Capture the prefab's authored rest position ONCE so subsequent
            // sessions don't inherit drift from where the hook ended up last time.
            _startLocal = transform.localPosition;
            _maxY = _startLocal.y + clampY;
            _minY = _startLocal.y - clampY;
            _restCaptured = true;
        }

        public void BeginSession()
        {
            // Defensive: if Awake somehow didn't run, capture now.
            if (!_restCaptured)
            {
                _startLocal = transform.localPosition;
                _maxY = _startLocal.y + clampY;
                _minY = _startLocal.y - clampY;
                _restCaptured = true;
            }

            // Reset to bottom of travel, no momentum.
            transform.localPosition = new Vector3(_startLocal.x, _minY, _startLocal.z);
            _velocity = 0f;
            _active = true;
        }

        public void EndSession()
        {
            _active = false;
            _velocity = 0f;
        }

        private void Update()
        {
            if (!_active) return;

            float dt = Time.deltaTime;
            bool reeling = Mouse.current != null && Mouse.current.leftButton.isPressed;

            // Apply forces: gravity down always, thrust up while held.
            float accel = -gravity + (reeling ? thrust : 0f);
            _velocity += accel * dt;

            // Damping bleeds velocity toward zero — multiplicative, frame-rate safe.
            _velocity *= Mathf.Exp(-damping * dt);

            // Clamp to terminal velocity so things stay sane.
            _velocity = Mathf.Clamp(_velocity, -maxSpeed, maxSpeed);

            // Integrate position.
            Vector3 p = transform.localPosition;
            p.y += _velocity * dt;

            // Bounce off the edges with soft restitution.
            if (p.y > _maxY)
            {
                p.y = _maxY;
                if (_velocity > 0f) _velocity = -_velocity * bounceCoefficient;
            }
            else if (p.y < _minY)
            {
                p.y = _minY;
                if (_velocity < 0f) _velocity = -_velocity * bounceCoefficient;
            }

            transform.localPosition = p;
        }
    }
}