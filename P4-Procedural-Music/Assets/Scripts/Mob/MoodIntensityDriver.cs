using UnityEngine;

/// <summary>
/// During the day, watches MoodSystem.CurrentTier. If the player is Miserable
/// (or below the configured tier), it slowly pushes PostProcessingManager.masterIntensity
/// upward. When mood recovers, intensity falls back down.
///
/// At night this driver suppresses itself entirely — night intensity is handled
/// by other systems (e.g. TentacleSpawner).
///
/// SETUP:
///   Attach to the same GameObject as PostProcessingManager.
///   Wire PostProcessingManager and optionally TimeReference in the Inspector.
///   TimeReference auto-finds "Day Night System" if left empty.
/// </summary>
public class MoodIntensityDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PostProcessingManager postProcessing;
    [SerializeField] private TimeReference timeReference;

    [Header("Night Window")]
    [Tooltip("Hour at which night begins (0–24). Intensity driver goes inactive.")]
    [SerializeField] private float nightStartHour = 20f;
    [Tooltip("Hour at which night ends (0–24). Intensity driver becomes active again.")]
    [SerializeField] private float nightEndHour = 6f;

    [Header("Mood Threshold")]
    [Tooltip("Tiers AT OR BELOW this value will drive intensity upward during the day. " +
             "Default: Miserable only. Set to Uneasy to trigger earlier.")]
    [SerializeField] private MoodTier activationTier = MoodTier.Miserable;

    [Header("Intensity Tuning")]
    [Tooltip("How fast intensity rises when player is miserable (units/sec). " +
             "0.05 = ~20 seconds to reach max from zero.")]
    [SerializeField] private float riseSpeed = 0.05f;

    [Tooltip("How fast intensity falls when mood recovers (units/sec). " +
             "Faster than rise so recovery feels rewarding.")]
    [SerializeField] private float fallSpeed = 0.08f;

    [Tooltip("Maximum intensity this driver will push to. " +
             "Leaves PostProcessingManager's tentacleTriggerThreshold in control of the actual spawn.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxDrivenIntensity = 0.9f;

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Start()
    {
        if (postProcessing == null)
            postProcessing = GetComponent<PostProcessingManager>();

        if (timeReference == null)
        {
            var dn = GameObject.Find("Day Night System");
            if (dn != null)
                timeReference = dn.GetComponent<TimeReference>();
        }
    }

    private void Update()
    {
        if (postProcessing == null || MoodSystem.Instance == null) return;

        float hour   = timeReference != null ? timeReference.time : 12f;
        bool isNight = IsNightHour(hour);

        // Suppress entirely at night — night intensity is owned by other systems
        if (isNight) return;

        bool miserableEnough = (int)MoodSystem.Instance.CurrentTier <= (int)activationTier;

        float current = postProcessing.masterIntensity;
        float target  = miserableEnough ? maxDrivenIntensity : 0f;
        float speed   = miserableEnough ? riseSpeed : fallSpeed;

        float next = Mathf.MoveTowards(current, target, speed * Time.deltaTime);

        // When rising: only push intensity up, never fight systems holding it higher
        // When falling: always move toward zero so we don't freeze intensity at day's end
        if (miserableEnough)
            next = Mathf.Max(current, next);

        postProcessing.SetMasterIntensity(Mathf.Clamp01(next));
    }

    // ───────────────────────── Helpers ─────────────────────────

    private bool IsNightHour(float hour)
    {
        // Handles wrap-around (e.g. 20:00 → 06:00 crosses midnight)
        if (nightStartHour > nightEndHour)
            return hour >= nightStartHour || hour < nightEndHour;

        return hour >= nightStartHour && hour < nightEndHour;
    }
}
