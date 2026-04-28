using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Drop this on a parent GameObject ("CutsceneRoot") that contains your cutscene actors,
/// props, and Cinemachine virtual camera as children. Author the children at LOCAL positions
/// relative to where the player will end up — typically (0,0,0) for the player's spot,
/// then NPCs offset around that.
///
/// Lifecycle:
/// 1. PlayerSaveSystem.LoadPlayer runs (called by SaveSystemManager).
/// 2. PlayerSaveSystem.OnPlayerLoaded fires.
/// 3. This script moves CutsceneRoot to the player's position and plays the Timeline.
///
/// Requires the OnPlayerLoaded event added to PlayerSaveSystem.
/// If LoadPlayer is skipped (no save, dungeon, etc.) the event won't fire — set
/// playOnAwakeIfNoLoadEvent to true to fall back to playing immediately at the rig's
/// authored position.
/// </summary>
public class CutsceneAtPlayerPosition : MonoBehaviour
{
    [Tooltip("PlayableDirector to start once the rig is in position. Usually a child of this GameObject.")]
    [SerializeField] private PlayableDirector director;

    [Tooltip("Optional. Leave empty to auto-find by tag 'Player'. Used to read the loaded position.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Optional. A child Transform of this GameObject marking 'where the player should appear' " +
             "in your authored layout. If set, the rig is moved so that this anchor lands on the " +
             "player's position — useful when the cutscene was authored with the player in a specific " +
             "spot relative to other elements, rather than at the rig's own pivot. Leave empty if your " +
             "rig's pivot (0,0,0) is already where the player should be.")]
    [SerializeField] private Transform playerAnchor;

    [Tooltip("If true, also align the player object as a child of the rig before playing. " +
             "Useful if the Timeline animates the player and you want to author its motion in local space.")]
    [SerializeField] private bool reparentPlayerUnderRig = false;

    [Tooltip("If OnPlayerLoaded never fires (e.g. no save file, dungeon flag set), play the cutscene " +
             "anyway at the rig's authored position after this many seconds. Set to 0 to disable.")]
    [SerializeField] private float fallbackDelaySeconds = 1f;

    private bool _played;

    private void OnEnable()
    {
        PlayerSaveSystem.OnPlayerLoaded += HandlePlayerLoaded;
    }

    private void OnDisable()
    {
        PlayerSaveSystem.OnPlayerLoaded -= HandlePlayerLoaded;
    }

    private void Start()
    {
        if (fallbackDelaySeconds > 0f)
            Invoke(nameof(FallbackPlay), fallbackDelaySeconds);
    }

    private void HandlePlayerLoaded()
    {
        if (_played) return;
        _played = true;
        CancelInvoke(nameof(FallbackPlay));

        var player = ResolvePlayer();
        if (player != null)
        {
            AlignToPlayer(player);
        }
        else
        {
            Debug.LogWarning("[CutsceneAtPlayerPosition] OnPlayerLoaded fired but no player Transform was found. " +
                             "Playing cutscene at the rig's authored position.", this);
        }

        Play();
    }

    private void FallbackPlay()
    {
        if (_played) return;
        _played = true;

        // OnPlayerLoaded didn't fire — but the player may still be at the right position
        // (e.g. carried over via DontDestroyOnLoad, or save loaded before we subscribed).
        // Snap to the player's current position regardless, so the rig and player line up.
        var player = ResolvePlayer();
        if (player != null)
        {
            AlignToPlayer(player);
        }
        else
        {
            Debug.LogWarning("[CutsceneAtPlayerPosition] Fallback: no player Transform found. " +
                             "Playing cutscene at the rig's authored position.", this);
        }

        Play();
    }

    /// <summary>
    /// Move the rig so that either its own pivot, or the playerAnchor child if assigned,
    /// lands on the player's world position. Optionally reparent the player under the rig.
    /// </summary>
    private void AlignToPlayer(Transform player)
    {
        if (playerAnchor != null)
        {
            Vector3 offset = playerAnchor.position - transform.position;
            transform.position = player.position - offset;
        }
        else
        {
            transform.position = player.position;
        }

        if (reparentPlayerUnderRig)
        {
            // Reparent without keeping world position, then snap the player to the rig's pivot.
            // This guarantees the player is at the layout's "player spot" regardless of any
            // other systems that may have moved the player before this ran.
            player.SetParent(transform, worldPositionStays: false);
            player.localPosition = Vector3.zero;
        }
    }

    private void Play()
    {
        if (director == null)
        {
            Debug.LogError("[CutsceneAtPlayerPosition] No PlayableDirector assigned.", this);
            return;
        }
        director.Play();
    }

    private Transform ResolvePlayer()
    {
        if (playerTransform != null) return playerTransform;

        if (PlayerSaveSystem.Instance != null)
        {
            // PlayerSaveSystem is attached to the player GameObject itself
            playerTransform = PlayerSaveSystem.Instance.transform;
            return playerTransform;
        }

        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            playerTransform = tagged.transform;
            return playerTransform;
        }

        return null;
    }
}