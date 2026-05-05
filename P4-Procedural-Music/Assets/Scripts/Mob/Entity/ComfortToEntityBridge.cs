using UnityEngine;

public class ComfortToEntityBridge : MonoBehaviour
{
    [Header("References")]
    public PostProcessingManager postProcessingManager;

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

        if (comfortSystem.Comfort < comfortThreshold)
        {
            // Map how far below the threshold comfort is to a target intensity
            float targetIntensity = Mathf.InverseLerp(comfortThreshold, 0f, comfortSystem.Comfort);

            postProcessingManager.SetIntensity(
                Mathf.MoveTowards(postProcessingManager.masterIntensity,
                    targetIntensity, intensityIncreaseSpeed * Time.deltaTime));
        }
    }
}