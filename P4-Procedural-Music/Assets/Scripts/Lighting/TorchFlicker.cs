using UnityEngine;

public class TorchFlicker : MonoBehaviour
{
    [Header("Light References")]
    [SerializeField] private Light torchLight;

    [Header("Intensity Settings")]
    [SerializeField] private float minIntensity = 0.8f;
    [SerializeField] private float maxIntensity = 1.5f;
    [SerializeField] private float baseIntensity = 1.0f;

    [Header("Flicker Speed")]
    [SerializeField] private float flickerSpeed = 3f;

    [Header("Range Variation")]
    [SerializeField] private bool varyRange = true;
    [SerializeField] private float minRange = 8f;
    [SerializeField] private float maxRange = 10f;
    [SerializeField] private float baseRange = 9f;

    [Header("Advanced Settings")]
    [SerializeField] private bool usePerlinNoise = true;
    [SerializeField] private float noiseScale = 1f;
    [SerializeField] private bool addRandomSpikes = true;
    [SerializeField] private float spikeChance = 0.02f;

    private float randomOffset;
    private float spikeTimer;

    void Start()
    {
        // Get light component if not assigned
        if (torchLight == null)
        {
            torchLight = GetComponent<Light>();
        }

        if (torchLight == null)
        {
            Debug.LogError("No Light component found! Please assign a Light or attach this script to a GameObject with a Light component.");
            enabled = false;
            return;
        }

        // Random offset so multiple torches don't flicker in sync
        randomOffset = Random.Range(0f, 100f);

        // Store base values
        baseIntensity = torchLight.intensity;
        baseRange = torchLight.range;
    }

    void Update()
    {
        if (torchLight == null) return;

        float flicker = 0f;

        if (usePerlinNoise)
        {
            // Perlin noise for smooth, natural flickering
            float noise = Mathf.PerlinNoise(
                (Time.time * flickerSpeed + randomOffset) * noiseScale,
                randomOffset
            );

            // Remap from 0-1 to -1 to 1 for variation
            flicker = (noise - 0.5f) * 2f;
        }
        else
        {
            // Simple sine wave flickering
            flicker = Mathf.Sin(Time.time * flickerSpeed + randomOffset);
        }

        // Add random spikes for more realism (like wind gusts)
        if (addRandomSpikes && Random.value < spikeChance)
        {
            spikeTimer = 0.1f;
        }

        if (spikeTimer > 0)
        {
            flicker += Random.Range(-0.5f, -0.3f); // Sudden dip
            spikeTimer -= Time.deltaTime;
        }

        // Apply intensity variation
        float targetIntensity = baseIntensity + flicker * (maxIntensity - minIntensity) * 0.5f;
        torchLight.intensity = Mathf.Clamp(targetIntensity, minIntensity, maxIntensity);

        // Apply range variation if enabled
        if (varyRange)
        {
            float targetRange = baseRange + flicker * (maxRange - minRange) * 0.3f;
            torchLight.range = Mathf.Clamp(targetRange, minRange, maxRange);
        }
    }

    // Optional: Methods to control the torch programmatically
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
            // Reset to base values when disabled
            torchLight.intensity = baseIntensity;
            torchLight.range = baseRange;
        }
    }
}