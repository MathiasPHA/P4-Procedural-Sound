using UnityEngine;

/// <summary>
/// Attach this to the Player.
/// It finds all ComfortSource objects in the scene, picks the CLOSEST one,
/// and sends a comfort delta to HappinessBar every frame.
/// </summary>
public class ComfortSystemV1 : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HappinessBar happinessBar;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 10f; // Max range at which objects have any effect

    [Header("Comfort Rate")]
    [SerializeField] private float comfortStrengthMultiplier = 20f;
    // Happiness change per second at point-blank range.
    // A good object at distance 0  => +20 happiness/sec
    // An evil object at distance 0 => -20 happiness/sec

    [Header("Idle Recovery")]
    [SerializeField] private float idleRecoveryRate = 2f;
    // When no object is in range, happiness slowly drifts back toward the centre (50).

    // Cached array – Unity reuses it each Physics2D.OverlapCircleNonAlloc call
    private Collider2D[] nearbyColliders = new Collider2D[32];

    // Exposed for UI / debugging
    public ComfortSource ActiveSource { get; private set; }
    public float ActiveComfortValue { get; private set; }

    private void Update()
    {
        ComfortSource closest = FindClosestComfortSource(out float closestDist);
        ActiveSource = closest;

        if (closest == null)
        {
            ActiveComfortValue = 0f;
            ApplyIdleRecovery();
            return;
        }

        // Normalise distance: 0 = right on top, 1 = at edge of detection radius
        float t = Mathf.Clamp01(closestDist / detectionRadius);

        // Inverse-square falloff so being close feels much stronger
        float influence = 1f - (t * t);

        // comfortValue on the source is positive (good) or negative (evil)
        float delta = closest.comfortValue * influence * comfortStrengthMultiplier;
        ActiveComfortValue = delta;

        happinessBar.ApplyComfortDelta(delta);
    }

    private ComfortSource FindClosestComfortSource(out float closestDistance)
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, detectionRadius, nearbyColliders);

        ComfortSource closest = null;
        closestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            ComfortSource source = nearbyColliders[i].GetComponent<ComfortSource>();
            if (source == null) continue;

            float dist = Vector2.Distance(transform.position, nearbyColliders[i].transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closest = source;
            }
        }

        return closest;
    }

    private void ApplyIdleRecovery()
    {
        // Nudge happiness toward 50 (centre) when nothing is nearby
        float centre = happinessBar.MaxHappiness * 0.5f;
        float direction = Mathf.Sign(centre - happinessBar.CurrentHappiness);
        happinessBar.ApplyComfortDelta(direction * idleRecoveryRate);
    }

    // Optional: draw the detection radius in the Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}