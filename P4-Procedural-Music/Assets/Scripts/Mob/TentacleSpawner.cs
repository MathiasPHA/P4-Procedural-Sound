using UnityEngine;

/// <summary>
/// Handles nighttime tentacle spawning independently of PostProcessingManager.
///
/// TWO SPAWN CONDITIONS:
///   Day:   Mood → MoodIntensityDriver → intensity rises →
///          PostProcessingManager.ApplyIntensity() triggers tentacle at tentacleTriggerThreshold.
///          (This component does nothing during the day.)
///
///   Night: This component rolls a spawn chance every nightSpawnInterval seconds.
///          Mood and intensity are not required — night spawns happen regardless.
///
/// A global cooldown prevents attacks from chaining too rapidly.
///
/// SETUP:
///   Attach anywhere stable in the scene (e.g. alongside MobSpawnManager).
///   Wire TentacleAttack in the Inspector.
///   TimeReference auto-finds "Day Night System" if left empty.
///   PostProcessingManager is optional — used only to read masterIntensity for debug GUI.
/// </summary>
public class TentacleSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TentacleAttack tentacleAttack;
    [SerializeField] private TimeReference timeReference;
    [SerializeField] private PostProcessingManager postProcessing; // optional, for debug

    [Header("Night Window")]
    [Tooltip("Hour at which night begins (0–24).")]
    [SerializeField] private float nightStartHour = 20f;
    [Tooltip("Hour at which night ends (0–24).")]
    [SerializeField] private float nightEndHour = 6f;

    [Header("Night Spawning")]
    [Tooltip("Seconds between spawn attempt rolls at night.")]
    [SerializeField] private float nightSpawnInterval = 30f;
    [Tooltip("Probability of triggering an attack each interval during the night.")]
    [Range(0f, 1f)]
    [SerializeField] private float nightSpawnChance = 0.4f;

    [Header("Cooldown")]
    [Tooltip("Minimum seconds between any two tentacle attacks, regardless of condition. " +
             "Prevents chaining when both day and night conditions are borderline.")]
    [SerializeField] private float globalCooldown = 15f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    // ───────────────────────── Runtime ─────────────────────────

    private float _nightTimer;
    private float _cooldownTimer;

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Start()
    {
        if (timeReference == null)
        {
            var dn = GameObject.Find("Day Night System");
            if (dn != null)
                timeReference = dn.GetComponent<TimeReference>();
        }

        // Stagger first roll so it doesn't fire immediately on scene load
        _nightTimer = nightSpawnInterval;
    }

    private void Update()
    {
        if (tentacleAttack == null) return;

        _cooldownTimer -= Time.deltaTime;

        float hour   = timeReference != null ? timeReference.time : 12f;
        bool isNight = IsNightHour(hour);

        // ── Night tick ──
        if (isNight)
        {
            _nightTimer -= Time.deltaTime;
            if (_nightTimer <= 0f)
            {
                _nightTimer = nightSpawnInterval;

                if (_cooldownTimer <= 0f && Random.value < nightSpawnChance)
                    TriggerAttack();
            }
        }
        else
        {
            // Reset night timer during the day so the first night roll
            // doesn't fire immediately at dusk
            _nightTimer = nightSpawnInterval;
        }
    }

    // ───────────────────────── Spawn ─────────────────────────

    private void TriggerAttack()
    {
        _cooldownTimer = globalCooldown;
        tentacleAttack.Trigger(Random.insideUnitCircle.normalized);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private bool IsNightHour(float hour)
    {
        if (nightStartHour > nightEndHour)
            return hour >= nightStartHour || hour < nightEndHour;

        return hour >= nightStartHour && hour < nightEndHour;
    }

    // ───────────────────────── Debug GUI ─────────────────────────

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        float hour   = timeReference != null ? timeReference.time : 12f;
        bool isNight = IsNightHour(hour);

        float x = 10f, y = 200f;
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), "=== TENTACLE SPAWNER ==="); y += 22f;

        GUI.color = isNight ? new Color(0.5f, 0.5f, 1f) : new Color(1f, 0.9f, 0.3f);
        GUI.Label(new Rect(x, y, 300, 20), $"Phase: {(isNight ? "NIGHT" : "DAY")}"); y += 20f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20),
            $"Night timer: {_nightTimer:F1}s / {nightSpawnInterval:F1}s"); y += 20f;

        GUI.color = _cooldownTimer > 0f ? Color.red : Color.green;
        GUI.Label(new Rect(x, y, 300, 20),
            _cooldownTimer > 0f
                ? $"Cooldown: {_cooldownTimer:F1}s remaining"
                : "Cooldown: READY"); y += 20f;

        if (postProcessing != null)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 300, 20),
                $"Intensity: {postProcessing.masterIntensity:F2}");
        }

        GUI.color = Color.white;
    }
}
