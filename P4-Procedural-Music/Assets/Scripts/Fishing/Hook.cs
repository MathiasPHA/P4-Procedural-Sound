using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingSystem
{
    /// <summary>
    /// The player-controlled "reel" half of the minigame. Rises while the
    /// reel input is held, falls when released. Sits as a child of the
    /// FishingSystem prefab and only updates while a session is active.
    ///
    /// Replaces the original Hook.cs:
    ///   - No GameObject.Find — runs only when BeginSession is called.
    ///   - Uses Mouse.current (Input System) to match ToolUseSystem instead
    ///     of legacy Input.GetKey(Space).
    ///   - Hook clamp range is computed from inspector-configurable extents
    ///     rather than hardcoded ±2.55 units.
    /// </summary>
    public class Hook : MonoBehaviour
    {
        [Header("Vertical Range (local Y, relative to start position)")]
        [SerializeField] private float clampY = 2.55f;

        [Header("Movement")]
        [SerializeField] private float hookMoveSpeed = 3f;
        [Tooltip("Distance to target at which we blend from linear motion to smoothed motion. " +
                 "Keeps the hook from jittering when very close to the extreme.")]
        [SerializeField] private float smoothZone = 0.05f;

        private Vector3 _startPos;
        private Vector3 _minPos;
        private Vector3 _maxPos;
        private bool _active;

        /// <summary>
        /// Called by FishingManager when a session begins. Captures the current
        /// position as the "rest" position, computes clamps, and resets the
        /// hook to the bottom.
        /// </summary>
        public void BeginSession()
        {
            _startPos = transform.position;
            _maxPos = _startPos + Vector3.up * clampY;
            _minPos = _startPos + Vector3.down * clampY;
            transform.position = _minPos;
            _active = true;
        }

        public void EndSession()
        {
            _active = false;
        }

        private void Update()
        {
            if (!_active) return;

            bool reeling = Mouse.current != null && Mouse.current.leftButton.isPressed;
            Vector3 target = reeling ? _maxPos : _minPos;
            Vector3 current = transform.position;

            float distToTarget = Vector3.Distance(current, target);
            float t = Mathf.InverseLerp(smoothZone, 0f, distToTarget);

            Vector3 linearMove = Vector3.MoveTowards(current, target, hookMoveSpeed * Time.deltaTime);
            Vector3 smoothMove = Vector3.Lerp(current, target, hookMoveSpeed * Time.deltaTime);

            transform.position = Vector3.Lerp(linearMove, smoothMove, t);
        }
    }
}