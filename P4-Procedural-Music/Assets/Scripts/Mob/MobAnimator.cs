using UnityEngine;
using MobSystem.States;

namespace MobSystem
{
    /// <summary>
    /// Drives mob visuals based on MobController's state and facing direction.
    /// Works in two modes, determined automatically at startup:
    ///
    ///   SPRITE-ONLY (no Animator):
    ///     Flips the SpriteRenderer horizontally based on FacingDirection.
    ///     This is the minimum viable setup — attach a sprite, add MobAnimator,
    ///     and mobs will face the right way while moving. Good for prototyping.
    ///
    ///   ANIMATOR (Animator component found):
    ///     Calls Animator.Play() with clip names built from a naming convention:
    ///       "{clipPrefix}_{State}_{Direction}"
    ///     e.g. "Rabbit_Walk_Left", "Wolf_Attack_Right"
    ///
    ///     Supported states: Idle, Walk, Attack, Hurt, Die
    ///     Supported directions (4-dir mode): Front, Back, Left, Right
    ///     Supported directions (2-dir mode): Left, Right (with sprite flip)
    ///
    ///     If a clip isn't found, falls back gracefully:
    ///       1. Try "{prefix}_{state}_{dir}"
    ///       2. Try "{prefix}_{state}" (no direction suffix)
    ///       3. Do nothing (no error spam — just keeps last frame)
    ///
    /// SETUP:
    ///   1. Attach to the mob prefab (same GameObject as SpriteRenderer)
    ///   2. Set clipPrefix to match your animation clip names (e.g. "Rabbit")
    ///   3. For Animator mode: add an Animator component with an AnimatorController
    ///      containing clips named "{prefix}_{state}_{direction}"
    ///   4. Choose 2-dir or 4-dir mode based on your sprite sheet
    ///
    /// The MobController sets AnimationQueue ("Idle", "Walk", "Attack") and
    /// FacingDirection every frame — this component reads those values.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MobAnimator : MonoBehaviour
    {
        [Header("Naming")]
        [Tooltip("Prefix for animation clip names (e.g. 'Rabbit' → 'Rabbit_Walk_Left'). " +
                 "Leave empty to use the MobData.id at runtime.")]
        [SerializeField] private string clipPrefix = "";

        [Header("Direction Mode")]
        [Tooltip("If true, uses only Left/Right clips and flips the sprite. " +
                 "If false, uses Front/Back/Left/Right clips (4-directional).")]
        [SerializeField] private bool twoDirectionMode = true;

        [Header("Flip Settings")]
        [Tooltip("Which axis to flip for horizontal direction changes.")]
        [SerializeField] private bool flipViaScale = false;

        [Tooltip("If the sprite is drawn facing right by default, enable this " +
                 "so the idle/default state faces right without flipping.")]
        [SerializeField] private bool defaultFacingRight = true;

        // ───────────────────────── Runtime ─────────────────────────

        private SpriteRenderer _spriteRenderer;
        private Animator _animator;
        private MobController _mob;
        private bool _hasAnimator;
        private string _lastPlayedClip;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
            _hasAnimator = _animator != null && _animator.runtimeAnimatorController != null;
        }

        private void Start()
        {
            _mob = GetComponent<MobController>();
            if (_mob == null)
                _mob = GetComponentInParent<MobController>();

            if (_mob == null)
            {
                Debug.LogWarning($"[MobAnimator] No MobController found on {gameObject.name}!");
                enabled = false;
                return;
            }

            // Auto-set prefix from MobData if not set in inspector
            if (string.IsNullOrEmpty(clipPrefix) && _mob.Data != null)
                clipPrefix = _mob.Data.id;
        }

        private void LateUpdate()
        {
            if (_mob == null) return;

            UpdateFlip();

            if (_hasAnimator)
                UpdateAnimator();
        }

        // ───────────────────────── Sprite Flipping ─────────────────────────

        private void UpdateFlip()
        {
            float dirX = _mob.FacingDirection.x;

            // Dead zone — don't flip when moving mostly vertically
            if (Mathf.Abs(dirX) < 0.1f) return;

            bool shouldFaceLeft = dirX < 0f;
            bool shouldFlip = defaultFacingRight ? shouldFaceLeft : !shouldFaceLeft;

            if (flipViaScale)
            {
                Vector3 scale = transform.localScale;
                float absX = Mathf.Abs(scale.x);
                scale.x = shouldFlip ? -absX : absX;
                transform.localScale = scale;
            }
            else
            {
                _spriteRenderer.flipX = shouldFlip;
            }
        }

        // ───────────────────────── Animator ─────────────────────────

        private void UpdateAnimator()
        {
            string state = MapAnimationQueue(_mob.AnimationQueue);
            string direction = GetDirectionSuffix();

            // Try full name: Rabbit_AttackWindup_Left
            string fullClip = $"{clipPrefix}_{state}_{direction}";
            if (TryPlay(fullClip)) return;

            // Fallback: Rabbit_AttackWindup (no direction)
            string noDir = $"{clipPrefix}_{state}";
            if (TryPlay(noDir)) return;

            // Intermediate fallback for attack phases — if dedicated windup/recovery
            // clips don't exist, use the regular Attack clip so the mob still
            // visually acknowledges the attack.
            if (state == "AttackWindup" || state == "AttackRecovery")
            {
                string attackClip = $"{clipPrefix}_Attack_{direction}";
                if (TryPlay(attackClip)) return;

                string attackNoDir = $"{clipPrefix}_Attack";
                if (TryPlay(attackNoDir)) return;
            }

            // No matching clip — keep current frame (no spam)
        }

        private bool TryPlay(string clipName)
        {
            // Don't restart the same clip
            if (clipName == _lastPlayedClip) return true;

            // Check if the clip exists in the current controller
            if (!HasClip(clipName)) return false;

            _animator.Play(clipName);
            _lastPlayedClip = clipName;
            return true;
        }

        private bool HasClip(string clipName)
        {
            // Hash-based check is faster than string iteration
            int hash = Animator.StringToHash(clipName);

            // Check all layers (typically just one for 2D mobs)
            for (int layer = 0; layer < _animator.layerCount; layer++)
            {
                if (_animator.HasState(layer, hash))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Map MobController.AnimationQueue strings to clip-name state segments.
        /// States set "Idle", "Walk", "Attack" — these map directly.
        /// AttackWindup and AttackRecovery fall back to Attack → Idle if dedicated clips don't exist.
        /// </summary>
        private string MapAnimationQueue(string queue)
        {
            return queue switch
            {
                "Idle" => "Idle",
                "Walk" => "Walk",
                "Attack" => "Attack",
                "AttackWindup" => "AttackWindup",
                "AttackRecovery" => "AttackRecovery",
                "Hurt" => "Hurt",
                "Die" => "Die",
                _ => "Idle"
            };
        }

        /// <summary>
        /// Convert FacingDirection vector to a direction suffix for clip names.
        /// </summary>
        private string GetDirectionSuffix()
        {
            Vector2 dir = _mob.FacingDirection;

            if (twoDirectionMode)
            {
                // Only Left/Right — flip handles the visuals
                return dir.x >= 0 ? "Right" : "Left";
            }

            // 4-direction: pick dominant axis
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            {
                return dir.x >= 0 ? "Right" : "Left";
            }
            else
            {
                return dir.y >= 0 ? "Back" : "Front";
            }
        }
    }
}