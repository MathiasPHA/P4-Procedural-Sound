using UnityEngine;
using InventorySystem;
using InventorySystem.Data;

namespace FishingSystem
{
    /// <summary>
    /// Singleton coordinator for the fishing minigame. Sits on the root of the
    /// persistent FishingSystem prefab. The prefab itself stays active at all
    /// times so this script keeps running; the visual minigame container
    /// (minigameRoot) is what gets toggled per session.
    ///
    /// External systems (ToolUseSystem, PlayerFishingState) call StartFishing
    /// to begin a session and receive the outcome via callback.
    /// </summary>
    public class FishingManager : MonoBehaviour
    {
        public static FishingManager Instance { get; private set; }

        [Header("Minigame Components (children of this prefab)")]
        [Tooltip("Container holding all the visual minigame elements. Toggled on/off per session.")]
        [SerializeField] private GameObject minigameRoot;
        [SerializeField] private FishingMinigame minigame;
        [SerializeField] private Hook hook;
        [SerializeField] private FishingProgressBar progressBar;
        [Tooltip("Optional. Radial countdown that depletes as time runs out.")]
        [SerializeField] private FishingTimer timer;

        [Header("Drops")]
        [Tooltip("Item awarded on a successful catch.")]
        [SerializeField] private ItemData dropItem;
        [Min(1)][SerializeField] private int dropAmount = 1;

        [Header("Audio")]
        [SerializeField] private AudioClip catchSound;
        [Range(0f, 1f)][SerializeField] private float catchVolume = 0.6f;
        [Range(0f, 0.5f)][SerializeField] private float catchPitchVariation = 0.1f;

        [Header("UI Placement")]
        [Tooltip("Local offset applied to the minigame root, relative to the player. " +
                 "X is mirrored automatically based on player facing direction so " +
                 "the UI appears on the side the player is facing.")]
        [SerializeField] private Vector3 uiOffsetFromPlayer = new Vector3(2f, 1.5f, 0f);

        // Active session state
        private bool _isFishing;
        private System.Action<bool> _onComplete; // bool: true = caught, false = escaped/cancelled
        private Transform _followTarget;         // Player transform — UI sticks to them
        private PlayerStateManager _followPlayer; // Cached so we can read playerDir each frame
        private float _frozenXSign = 1f;         // Captured once at session start; UI doesn't flip mid-fish

        public bool IsFishing => _isFishing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (minigameRoot != null) minigameRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            // Stick the minigame UI to the player while fishing so it follows
            // any animation root motion / camera adjustments. Mirror the X
            // offset so the UI appears on whichever side the player is facing.
            if (_isFishing && _followTarget != null && minigameRoot != null)
            {
                minigameRoot.transform.position = _followTarget.position + GetMirroredOffset();
            }
        }

        private Vector3 GetMirroredOffset()
        {
            // Use the sign captured at session start, NOT the player's current
            // direction — otherwise the UI would flip if the player turned mid-fish.
            return new Vector3(uiOffsetFromPlayer.x * _frozenXSign, uiOffsetFromPlayer.y, uiOffsetFromPlayer.z);
        }

        /// <summary>
        /// Begin a fishing session.
        /// </summary>
        /// <param name="player">Player transform; the UI will follow this each frame.</param>
        /// <param name="onComplete">Invoked with true (caught) or false (escaped/cancelled).</param>
        public bool StartFishing(Transform player, System.Action<bool> onComplete)
        {
            if (_isFishing)
            {
                Debug.LogWarning("[FishingManager] StartFishing called while already fishing.");
                return false;
            }

            if (minigame == null || hook == null || progressBar == null || minigameRoot == null)
            {
                Debug.LogError("[FishingManager] Minigame components are not assigned in the inspector.");
                return false;
            }

            _isFishing = true;
            _onComplete = onComplete;
            _followTarget = player;
            _followPlayer = player != null ? player.GetComponent<PlayerStateManager>() : null;

            // Force the player into fishing state from here, no matter who called
            // StartFishing or what state they were in. This guarantees the run
            // animation from moveToInteractState (or any other state) doesn't
            // linger into the fishing session.
            if (_followPlayer != null && _followPlayer.CurrentState != _followPlayer.fishingState)
            {
                _followPlayer.fishingState.CastPosition = _followPlayer.transform.position;
                _followPlayer.SwitchState(_followPlayer.fishingState);
            }

            // Capture facing direction ONCE — UI side won't flip mid-session
            // even if the player's playerDir changes for some reason.
            _frozenXSign = (_followPlayer != null && _followPlayer.playerDir == "Left") ? -1f : 1f;

            // Snap into position immediately so the first visible frame is correct.
            if (_followTarget != null)
                minigameRoot.transform.position = _followTarget.position + GetMirroredOffset();

            minigameRoot.SetActive(true);

            // Order matters: minigame initialises its own state and then the
            // others can read from it (progress bar reads targetPoints, timer
            // reads InitialTime/TimeLeft).
            minigame.BeginSession(OnSessionEnded);
            hook.BeginSession();
            progressBar.BeginSession(minigame);
            if (timer != null) timer.BeginSession(minigame);

            return true;
        }

        /// <summary>
        /// Force-end the current session as a cancel (no fish caught).
        /// Safe to call when not fishing.
        /// </summary>
        public void CancelFishing()
        {
            if (!_isFishing) return;
            minigame.AbortSession(); // routes back through OnSessionEnded(false)
        }

        // Internal callback from FishingMinigame when a session ends
        // (success, timeout-escape, or abort).
        private void OnSessionEnded(bool caught)
        {
            if (!_isFishing) return;
            _isFishing = false;
            _followTarget = null;
            _followPlayer = null;

            hook.EndSession();
            progressBar.EndSession();
            if (timer != null) timer.EndSession();
            if (minigameRoot != null) minigameRoot.SetActive(false);

            if (caught)
            {
                AwardDrops();
                PlayCatchSound();
            }

            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke(caught);
        }

        private void AwardDrops()
        {
            if (dropItem == null) return;

            var inventory = InventoryBootstrap.PlayerInventory;
            if (inventory == null)
            {
                Debug.LogWarning("[FishingManager] PlayerInventory unavailable — drop lost.");
                return;
            }

            int overflow = inventory.AddItem(dropItem, dropAmount);
            if (overflow > 0)
                Debug.LogWarning($"[FishingManager] {overflow}x {dropItem.displayName} didn't fit in inventory.");
        }

        private void PlayCatchSound()
        {
            if (catchSound == null) return;

            var go = new GameObject("FishCatchSound");
            go.transform.position = transform.position;
            var src = go.AddComponent<AudioSource>();
            src.clip = catchSound;
            src.volume = catchVolume;
            src.pitch = 1f + Random.Range(-catchPitchVariation, catchPitchVariation);
            src.spatialBlend = 0f;
            src.Play();
            Destroy(go, catchSound.length / Mathf.Max(src.pitch, 0.1f));
        }
    }
}