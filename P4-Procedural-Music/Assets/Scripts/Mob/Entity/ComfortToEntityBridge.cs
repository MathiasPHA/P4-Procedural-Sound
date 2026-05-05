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

    private ComfortSystem comfortSystem;

    void Start()
    {
        comfortSystem = FindObjectOfType<ComfortSystem>();

        if (comfortSystem == null)
            Debug.LogWarning("[EntityComfortTrigger] No ComfortSystem found!");
        if (postProcessingManager == null)
            Debug.LogWarning("[EntityComfortTrigger] No PostProcessingManager assigned!");
    }

    void Update()
{
    if (comfortSystem == null || postProcessingManager == null) return;
    if (IsDraining) return; // wait for entity drain to finish

    if (comfortSystem.Comfort < comfortThreshold)
    {
        postProcessingManager.SetIntensity(
            postProcessingManager.masterIntensity + intensityIncreaseSpeed * Time.deltaTime);
    }
}
}