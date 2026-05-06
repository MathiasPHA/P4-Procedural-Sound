using UnityEngine;

public class ComfortToEntityBridge : MonoBehaviour
{
    [Header("References")]
    public PostProcessingManager postProcessingManager;
    public static bool IsDraining { get; set; } = false;

    [Header("Thresholds")]
    [Tooltip("Below this comfort level the entity starts appearing")]
    [Range(0f, 1f)] public float comfortThreshold = 0.3f;

    [Header("Intensity Settings")]
    public float intensityIncreaseSpeed = 0.2f;
    public float intensityDecreaseSpeed = 0.1f;

    private ComfortSystem comfortSystem;

    void Start()
    {
        comfortSystem = FindObjectOfType<ComfortSystem>();

        if (comfortSystem == null)
            Debug.LogWarning("[EntityComfortTrigger] No ComfortSystem found!");
        if (postProcessingManager == null)
            Debug.LogWarning("[EntityComfortTrigger] No PostProcessingManager assigned!");
    }

    [Header("Death Boost")]
public float comfortBoostDuration = 30f; // how long the boost lasts in seconds

private float boostTimer = 0f;
private bool isBoosted = false;

public void TriggerComfortBoost()
{
    isBoosted = true;
    boostTimer = comfortBoostDuration;
}

void Update()
{
    if (comfortSystem == null || postProcessingManager == null) return;
    if (IsDraining) return;

    // Tick boost timer
    if (isBoosted)
    {
        boostTimer -= Time.deltaTime;
        if (boostTimer <= 0f)
            isBoosted = false;
    }

    if (comfortSystem.Comfort < comfortThreshold && !isBoosted)
    {
        postProcessingManager.SetIntensity(
            postProcessingManager.masterIntensity + intensityIncreaseSpeed * Time.deltaTime);
    }
    else
    {
        postProcessingManager.SetIntensity(
            postProcessingManager.masterIntensity - intensityDecreaseSpeed * Time.deltaTime);
    }
}
}