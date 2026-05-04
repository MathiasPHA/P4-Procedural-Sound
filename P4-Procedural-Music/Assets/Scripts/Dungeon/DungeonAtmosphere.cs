using UnityEngine;

/// <summary>
/// Place in the Dungeon Scene alongside DungeonGenerator.
/// 
/// Does two things:
///   1. Registers as an IComfortInfluence that drags comfort down,
///      which naturally pushes tension up through the comfort system.
///   2. Puts the ComfortMusicBridge into dungeon mode so it uses
///      the DungeonConfig's tension→state thresholds instead of day/night.
///
/// The result: tension still drives everything dynamically, but the
/// state progression is dungeon-specific (e.g. Spooky → Pressure → Horror → Combat).
/// </summary>
public class DungeonAtmosphere : MonoBehaviour, IComfortInfluence
{
    [Header("References (auto-found if empty)")]
    [SerializeField] private ComfortSystem comfortSystem;
    [SerializeField] private ComfortMusicBridge musicBridge;

    [Header("Dungeon Comfort Influence")]
    [Tooltip("Radius — set large enough to cover the entire dungeon")]
    [SerializeField] private float influenceRadius = 500f;

    [Tooltip("How strongly this influence weighs against other sources")]
    [SerializeField] private float influenceWeight = 1.5f;

    private DungeonConfig config;

    // ───────── IComfortInfluence ─────────

    public float ComfortValue => config != null ? config.dungeonComfortValue : 0.15f;
    public float Radius => influenceRadius;
    public float Weight => influenceWeight;
    public Vector3 Position => transform.position;
    public string Name => config != null ? config.dungeonName : "Dungeon";
    public bool UseFalloff => true;
    public bool IsDestroyed => this == null;

    private void Start()
    {
        if (DungeonManager.Instance == null || DungeonManager.Instance.ActiveConfig == null)
            return;

        config = DungeonManager.Instance.ActiveConfig;

        if (comfortSystem == null)
            comfortSystem = FindObjectOfType<ComfortSystem>();

        if (musicBridge == null)
            musicBridge = FindObjectOfType<ComfortMusicBridge>();

        // Register as a comfort influence — the low ComfortValue
        // drags comfort down, which raises tension naturally
        if (comfortSystem != null)
            comfortSystem.RegisterInfluence(this);

        // Tell the bridge to use this dungeon's threshold set
        if (musicBridge != null)
            musicBridge.EnterDungeonMode(config);
    }

    private void OnDestroy()
    {
        if (comfortSystem != null)
            comfortSystem.UnregisterInfluence(this);

        if (musicBridge != null)
            musicBridge.ExitDungeonMode();
    }
}