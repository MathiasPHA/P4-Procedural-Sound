using UnityEngine;

/// <summary>
/// Generic comfort influence source. Attach to any world object that should
/// affect the player's comfort level (campfires, enemies, shelters, etc.).
///
/// Automatically registers/unregisters with the ComfortSystem.
/// </summary>
public class ComfortInfluenceSource : MonoBehaviour, IComfortInfluence
{
    [Header("Influence Settings")]
    [Tooltip("What comfort value this pushes toward (0 = terrifying, 1 = cozy)")]
    [Range(0f, 1f)]
    [SerializeField] private float comfortValue = 0.8f;

    [Tooltip("How strongly this source affects comfort (0 = negligible, 1 = dominant)")]
    [Range(0f, 1f)]
    [SerializeField] private float weight = 1f;

    [Tooltip("Radius in world units")]
    [SerializeField] private float radius = 10f;

    [Tooltip("Display name for debug")]
    [SerializeField] private string influenceName = "Influence";

    // ── IComfortInfluence ──
    public float ComfortValue => comfortValue;
    public float Weight => weight;
    public float Radius => radius;
    public Vector3 Position => transform.position;
    public string Name => influenceName;
    public bool IsDestroyed => this == null;

    // ── Reference to the comfort system ──
    private ComfortSystem comfortSystem;

    private void OnEnable()
    {
        // Find the comfort system (assumed to be on the player)
        if (comfortSystem == null)
            comfortSystem = FindObjectOfType<ComfortSystem>();

        comfortSystem?.RegisterInfluence(this);
    }

    private void OnDisable()
    {
        comfortSystem?.UnregisterInfluence(this);
    }

    // ── Runtime API ──

    /// <summary>Change the comfort value at runtime (e.g. campfire dying out).</summary>
    public void SetComfortValue(float value) => comfortValue = Mathf.Clamp01(value);

    /// <summary>Change the influence weight at runtime.</summary>
    public void SetWeight(float w) => weight = Mathf.Clamp01(w);

    /// <summary>Change the radius at runtime.</summary>
    public void SetRadius(float r) => radius = Mathf.Max(0f, r);

    // ── Editor Gizmo ──

    private void OnDrawGizmosSelected()
    {
        // Green for comforting, red for threatening
        Gizmos.color = Color.Lerp(Color.red, Color.green, comfortValue);
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.25f);
        Gizmos.DrawSphere(transform.position, radius);

        Gizmos.color = Color.Lerp(Color.red, Color.green, comfortValue);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}


/// <summary>
/// Campfire comfort source that fades as fuel burns out.
/// </summary>
public class CampfireComfortSource : ComfortInfluenceSource
{
    [Header("Campfire")]
    [SerializeField] private float maxFuelSeconds = 120f;
    [SerializeField] private float fuelRemaining;

    [Tooltip("Comfort value when fully fueled")]
    [SerializeField] private float maxComfort = 0.95f;

    [Tooltip("Comfort value when nearly burnt out")]
    [SerializeField] private float minComfort = 0.55f;

    private void Start()
    {
        fuelRemaining = maxFuelSeconds;
    }

    private void Update()
    {
        if (fuelRemaining > 0f)
        {
            fuelRemaining -= Time.deltaTime;
            float fuelRatio = Mathf.Clamp01(fuelRemaining / maxFuelSeconds);

            // Comfort decreases as fire dies
            SetComfortValue(Mathf.Lerp(minComfort, maxComfort, fuelRatio));

            // Radius shrinks slightly as fire gets smaller
            SetRadius(Mathf.Lerp(5f, 12f, fuelRatio));
        }
        else
        {
            // Fire is out — becomes neutral
            SetComfortValue(0.5f);
            SetWeight(0f);
        }
    }

    /// <summary>Add fuel to the campfire.</summary>
    public void AddFuel(float seconds)
    {
        fuelRemaining = Mathf.Min(fuelRemaining + seconds, maxFuelSeconds);
        SetWeight(1f); // re-enable if it was out
    }
}


/// <summary>
/// Enemy comfort source — pushes comfort down when the player is nearby.
/// Can scale threat based on enemy state (patrolling vs aggressive).
/// </summary>
public class EnemyComfortSource : ComfortInfluenceSource
{
    public enum EnemyState { Idle, Patrolling, Alerted, Attacking }

    [Header("Enemy Threat")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrolling;

    private void Start()
    {
        UpdateThreatProfile();
    }

    public void SetState(EnemyState state)
    {
        currentState = state;
        UpdateThreatProfile();
    }

    private void UpdateThreatProfile()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                SetComfortValue(0.35f);  // mildly unsettling
                SetWeight(0.4f);
                SetRadius(8f);
                break;

            case EnemyState.Patrolling:
                SetComfortValue(0.2f);   // threatening
                SetWeight(0.6f);
                SetRadius(12f);
                break;

            case EnemyState.Alerted:
                SetComfortValue(0.1f);   // very threatening
                SetWeight(0.8f);
                SetRadius(18f);
                break;

            case EnemyState.Attacking:
                SetComfortValue(0.0f);   // maximum threat
                SetWeight(1.0f);
                SetRadius(25f);
                break;
        }
    }
}
