using UnityEngine;
using UnityEngine.Rendering.Universal;

public class TorchFlicker : MonoBehaviour
{
    [Header("Light References")]
    [SerializeField] private Light2D torchLight;

    [Header("Intensity Settings")]
    [SerializeField] private float minIntensity = 0.8f;
    [SerializeField] private float maxIntensity = 1.5f;
    [SerializeField] private float baseIntensity = 1.0f;

    [Header("Flicker Speed")]
    [SerializeField] private float flickerSpeed = 3f;

    [Header("Outer Radius Variation")]
    [SerializeField] private bool varyRange = true;
    [SerializeField] private float minOuterRadius = 8f;
    [SerializeField] private float maxOuterRadius = 10f;
    [SerializeField] private float baseOuterRadius = 9f;

    [Header("Inner Radius Variation")]
    [SerializeField] private bool varyInnerRadius = true;
    [SerializeField] private float minInnerRadius = 3f;
    [SerializeField] private float maxInnerRadius = 5f;
    [SerializeField] private float baseInnerRadius = 4f;

    [Header("Advanced Settings")]
    [SerializeField] private bool usePerlinNoise = true;
    [SerializeField] private float noiseScale = 1f;
    [SerializeField] private bool addRandomSpikes = true;
    [SerializeField] private float spikeChance = 0.02f;

    private float randomOffset;
    private float spikeTimer;

    void Start()
    {
        if (torchLight == null)
        {
            torchLight = GetComponent<Light2D>();
        }

        if (torchLight == null)
        {
            Debug.LogError("No Light2D component found! Please assign a Light2D or attach this script to a GameObject with a Light2D component.");
            enabled = false;
            return;
        }

        randomOffset = Random.Range(0f, 100f);

        // Store base values from the light
        baseIntensity = torchLight.intensity;
        baseOuterRadius = torchLight.pointLightOuterRadius;
        baseInnerRadius = torchLight.pointLightInnerRadius;
    }

    void Update()
    {
        if (torchLight == null) return;

        float flicker = 0f;

        if (usePerlinNoise)
        {
            float noise = Mathf.PerlinNoise(
                (Time.time * flickerSpeed + randomOffset) * noiseScale,
                randomOffset
            );

            flicker = (noise - 0.5f) * 2f;
        }
        else
        {
            flicker = Mathf.Sin(Time.time * flickerSpeed + randomOffset);
        }

        if (addRandomSpikes && Random.value < spikeChance)
        {
            spikeTimer = 0.1f;
        }

        if (spikeTimer > 0)
        {
            flicker += Random.Range(-0.5f, -0.3f);
            spikeTimer -= Time.deltaTime;
        }

        // Apply intensity variation
        float targetIntensity = baseIntensity + flicker * (maxIntensity - minIntensity) * 0.5f;
        torchLight.intensity = Mathf.Clamp(targetIntensity, minIntensity, maxIntensity);

        // Apply outer radius variation
        if (varyRange)
        {
            float targetOuter = baseOuterRadius + flicker * (maxOuterRadius - minOuterRadius) * 0.3f;
            torchLight.pointLightOuterRadius = Mathf.Clamp(targetOuter, minOuterRadius, maxOuterRadius);
        }

        // Apply inner radius variation (tied to the same flicker so they move together)
        if (varyInnerRadius)
        {
            float targetInner = baseInnerRadius + flicker * (maxInnerRadius - minInnerRadius) * 0.3f;
            targetInner = Mathf.Clamp(targetInner, minInnerRadius, maxInnerRadius);

            // Ensure inner never exceeds outer
            targetInner = Mathf.Min(targetInner, torchLight.pointLightOuterRadius);
            torchLight.pointLightInnerRadius = targetInner;
        }
    }

    public void SetFlickerSpeed(float speed)
    {
        flickerSpeed = speed;
    }

    public void SetIntensityRange(float min, float max)
    {
        minIntensity = min;
        maxIntensity = max;
    }

    public void EnableFlicker(bool enable)
    {
        enabled = enable;
        if (!enable)
        {
            torchLight.intensity = baseIntensity;
            torchLight.pointLightOuterRadius = baseOuterRadius;
            torchLight.pointLightInnerRadius = baseInnerRadius;
        }
    }
}