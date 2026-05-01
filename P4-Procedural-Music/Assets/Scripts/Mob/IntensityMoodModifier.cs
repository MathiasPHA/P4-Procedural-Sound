using UnityEngine;

/// <summary>
/// Registers on MoodSystem as a negative modifier while PostProcessingManager
/// masterIntensity is above the activation threshold.
///
/// NIGHT ONLY: This modifier is inactive during the day. The day/night asymmetry
/// is intentional — daytime horror doesn't spiral (the player can recover),
/// but nighttime attacks genuinely make things worse.
///
/// SETUP:
///   Attach to the same GameObject as PostProcessingManager.
///   Wire PostProcessingManager in the Inspector.
///   TimeReference auto-finds "Day Night System" if left empty.
/// </summary>
public class IntensityMoodModifier : MonoBehaviour, IMoodModifier
{
    [Header("References")]
    [SerializeField] private PostProcessingManager postProcessing;
    [SerializeField] private TimeReference timeReference;

    [Header("Night Window")]
    [Tooltip("Hour at which night begins (0–24).")]
    [SerializeField] private float nightStartHour = 20f;
    [Tooltip("Hour at which night ends (0–24).")]
    [SerializeField] private float nightEndHour = 6f;

    [Header("Tuning")]
    [Tooltip("Intensity must exceed this before mood is affected.")]
    [Range(0f, 1f)]
    [SerializeField] private float activationThreshold = 0.2f;

    [Tooltip("Mood drain rate at maximum intensity (units/sec). Keep negative.")]
    [SerializeField] private float maxDrainRate = -0.08f;

    // ───────────────────────── IMoodModifier ─────────────────────────

    public string ModifierName => "Night Intensity";

    public bool IsActive
    {
        get
        {
            if (postProcessing == null) return false;
            if (postProcessing.masterIntensity <= activationThreshold) return false;

            // Only drain mood at night
            float hour = timeReference != null ? timeReference.time : 0f;
            return IsNightHour(hour);
        }
    }

    public float MoodRate
    {
        get
        {
            if (!IsActive) return 0f;

            // Scale drain linearly: 0 at threshold → maxDrainRate at intensity 1.0
            float t = Mathf.InverseLerp(activationThreshold, 1f,
                                         postProcessing.masterIntensity);
            return Mathf.Lerp(0f, maxDrainRate, t);
        }
    }

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

    private void OnEnable()
    {
        if (MoodSystem.Instance != null)
            MoodSystem.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (MoodSystem.Instance != null)
            MoodSystem.Instance.Unregister(this);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private bool IsNightHour(float hour)
    {
        if (nightStartHour > nightEndHour)
            return hour >= nightStartHour || hour < nightEndHour;

        return hour >= nightStartHour && hour < nightEndHour;
    }
}
