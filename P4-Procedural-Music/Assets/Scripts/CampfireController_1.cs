using UnityEngine;
using UnityEngine.Rendering.Universal;
using InventorySystem.Data;

/// <summary>
/// Controls a campfire's burn state based on accumulated fuel.
/// Fuel is provided via ItemData ScriptableObjects with isFuel = true.
///
/// Burn levels:
///   Big    – fuelLevel > bigThreshold
///   Medium – fuelLevel > mediumThreshold
///   Low    – fuelLevel > 0
///   Out    – fuelLevel <= 0
/// </summary>
public class CampfireController : MonoBehaviour
{
    public enum BurnState { Out, Low, Medium, Big }

    [Header("Burn Thresholds")]
    public float bigThreshold    = 60f;
    public float mediumThreshold = 25f;
    public float maxFuel         = 100f;

    [Header("Burn Rate (fuel units / second)")]
    public float burnRateBig    = 3f;
    public float burnRateMedium = 2f;
    public float burnRateLow    = 1f;

    [Header("Particle Systems")]
    public ParticleSystem flameParticlesBig;
    public ParticleSystem flameParticlesMedium;
    public ParticleSystem flameParticlesLow;
    public ParticleSystem smokeParticles;

    [Header("Light (2D)")]
    [Tooltip("Assign the Light2D on the campfire. TorchFlicker should also be on the same GameObject.")]
    public Light2D fireLight;

    [Tooltip("TorchFlicker script — its intensity range will be adjusted per burn state.")]
    public TorchFlicker torchFlicker;

    [Tooltip("Min/Max flicker intensity while Big")]
    public float flickerMinBig    = 1.2f;
    public float flickerMaxBig    = 2.2f;

    [Tooltip("Min/Max flicker intensity while Medium")]
    public float flickerMinMedium = 0.8f;
    public float flickerMaxMedium = 1.4f;

    [Tooltip("Min/Max flicker intensity while Low")]
    public float flickerMinLow    = 0.3f;
    public float flickerMaxLow    = 0.7f;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip   crackleClip;
    public AudioClip   extinguishClip;

    [Header("Runtime State (read-only)")]
    [SerializeField, Min(0f)] private float currentFuel = 0f;
    [SerializeField]          private BurnState currentBurnState = BurnState.Out;

    public float     CurrentFuel      => currentFuel;
    public BurnState CurrentBurnState => currentBurnState;

    private void Start()
    {
        // Auto-grab TorchFlicker if not assigned
        if (torchFlicker == null)
            torchFlicker = GetComponent<TorchFlicker>();

        ApplyBurnState(EvaluateBurnState(), instant: true);
    }

    private void Update()
    {
        ConsumeFuel();
        BurnState newState = EvaluateBurnState();
        if (newState != currentBurnState)
            ApplyBurnState(newState, instant: false);
    }

    // ──────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────

    public void AddFuel(ItemData item, int quantity = 1)
    {
        if (item == null)
        {
            Debug.LogWarning("[Campfire] AddFuel called with null ItemData.");
            return;
        }

        if (!item.isFuel)
        {
            Debug.LogWarning($"[Campfire] '{item.displayName}' is not a fuel item (isFuel = false).");
            return;
        }

        float added = item.burnFuelValue * quantity;
        currentFuel = Mathf.Clamp(currentFuel + added, 0f, maxFuel);
        Debug.Log($"[Campfire] Added {quantity}x {item.displayName} (+{added} fuel). Total: {currentFuel:F1}/{maxFuel}");
    }

    public void Extinguish()
    {
        currentFuel = 0f;
        ApplyBurnState(BurnState.Out, instant: false);
    }

    public void SetFuel(float amount)
    {
        currentFuel = Mathf.Clamp(amount, 0f, maxFuel);
    }

    // ──────────────────────────────────────────────────────────────
    // Internal
    // ──────────────────────────────────────────────────────────────

    private void ConsumeFuel()
    {
        if (currentFuel <= 0f) return;

        float rate = currentBurnState switch
        {
            BurnState.Big    => burnRateBig,
            BurnState.Medium => burnRateMedium,
            BurnState.Low    => burnRateLow,
            _                => 0f
        };

        currentFuel = Mathf.Max(0f, currentFuel - rate * Time.deltaTime);
    }

    private BurnState EvaluateBurnState()
    {
        if (currentFuel <= 0f)             return BurnState.Out;
        if (currentFuel > bigThreshold)    return BurnState.Big;
        if (currentFuel > mediumThreshold) return BurnState.Medium;
        return BurnState.Low;
    }

    private void ApplyBurnState(BurnState newState, bool instant)
    {
        currentBurnState = newState;

        SetParticles(newState);
        ApplyLightSettings(newState);
        HandleAudio(newState);

        Debug.Log($"[Campfire] State changed → {newState}  (fuel: {currentFuel:F1})");
    }

    private void ApplyLightSettings(BurnState state)
    {
        if (state == BurnState.Out)
        {
            // Disable flicker and turn off the light
            if (torchFlicker != null) torchFlicker.EnableFlicker(false);
            if (fireLight    != null) fireLight.intensity = 0f;
            return;
        }

        // Enable flicker and set intensity range based on burn state
        if (torchFlicker != null)
        {
            torchFlicker.EnableFlicker(true);

            switch (state)
            {
                case BurnState.Big:
                    torchFlicker.SetIntensityRange(flickerMinBig, flickerMaxBig);
                    break;
                case BurnState.Medium:
                    torchFlicker.SetIntensityRange(flickerMinMedium, flickerMaxMedium);
                    break;
                case BurnState.Low:
                    torchFlicker.SetIntensityRange(flickerMinLow, flickerMaxLow);
                    break;
            }
        }
    }

    private void SetParticles(BurnState state)
    {
        SetPS(flameParticlesBig,    state == BurnState.Big);
        SetPS(flameParticlesMedium, state == BurnState.Medium);
        SetPS(flameParticlesLow,    state == BurnState.Low);
        SetPS(smokeParticles,       state == BurnState.Out);
    }

    private static void SetPS(ParticleSystem ps, bool shouldPlay)
    {
        if (ps == null) return;
        if (shouldPlay  && !ps.isPlaying) ps.Play();
        if (!shouldPlay &&  ps.isPlaying) ps.Stop();
    }

    private void HandleAudio(BurnState state)
    {
        if (audioSource == null) return;

        if (state == BurnState.Out)
        {
            if (audioSource.isPlaying) audioSource.Stop();
            if (extinguishClip != null) audioSource.PlayOneShot(extinguishClip);
        }
        else
        {
            if (!audioSource.isPlaying && crackleClip != null)
            {
                audioSource.clip = crackleClip;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
    }
}