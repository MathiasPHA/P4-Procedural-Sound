using System;
using UnityEngine;
using InventorySystem.Data;
using ProceduralTerrain;

/// <summary>
/// Controls a campfire's burn state based on accumulated fuel.
/// Fuel is provided via ItemData ScriptableObjects with isFuel = true.
///
/// Burn levels:
///   Big    – fuelLevel > bigThreshold
///   Medium – fuelLevel > mediumThreshold
///   Low    – fuelLevel > 0
///   Out    – fuelLevel <= 0
///
/// Persists currentFuel across save/load via IPersistentStructureState.
/// </summary>
public class CampfireController : MonoBehaviour, IPersistentStructureState
{
    public enum BurnState { Out, Low, Medium, Big }

    [Header("Burn Thresholds")]
    public float bigThreshold = 60f;
    public float mediumThreshold = 25f;
    public float maxFuel = 100f;

    [Header("Burn Rate (fuel units / second)")]
    public float burnRateBig = 3f;
    public float burnRateMedium = 2f;
    public float burnRateLow = 1f;

    [Header("Light (2D)")]
    [Tooltip("The child GameObject that holds the Light2D and TorchFlicker (e.g. the 'Light' child).")]
    public GameObject lightChild;

    [Tooltip("TorchFlicker script — its intensity range will be adjusted per burn state.")]
    public TorchFlicker torchFlicker;

    [Tooltip("Min/Max flicker intensity while Big")]
    public float flickerMinBig = 1.2f;
    public float flickerMaxBig = 2.2f;

    [Tooltip("Min/Max flicker intensity while Medium")]
    public float flickerMinMedium = 0.8f;
    public float flickerMaxMedium = 1.4f;

    [Tooltip("Min/Max flicker intensity while Low")]
    public float flickerMinLow = 0.3f;
    public float flickerMaxLow = 0.7f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip extinguishClip;

    [Header("Runtime State (read-only)")]
    [SerializeField, Min(0f)] private float currentFuel = 0f;
    [SerializeField] private BurnState currentBurnState = BurnState.Out;

    public float CurrentFuel => currentFuel;
    public BurnState CurrentBurnState => currentBurnState;

    private void Start()
    {
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
            BurnState.Big => burnRateBig,
            BurnState.Medium => burnRateMedium,
            BurnState.Low => burnRateLow,
            _ => 0f
        };

        currentFuel = Mathf.Max(0f, currentFuel - rate * Time.deltaTime);
    }

    private BurnState EvaluateBurnState()
    {
        if (currentFuel <= 0f) return BurnState.Out;
        if (currentFuel > bigThreshold) return BurnState.Big;
        if (currentFuel > mediumThreshold) return BurnState.Medium;
        return BurnState.Low;
    }

    private void ApplyBurnState(BurnState newState, bool instant)
    {
        currentBurnState = newState;

        ApplyLightSettings(newState);

        if (!instant && newState == BurnState.Out && audioSource != null && extinguishClip != null)
            audioSource.PlayOneShot(extinguishClip);

        Debug.Log($"[Campfire] State changed → {newState}  (fuel: {currentFuel:F1})");
    }

    private void ApplyLightSettings(BurnState state)
    {
        if (state == BurnState.Out)
        {
            if (lightChild != null) lightChild.SetActive(false);
            return;
        }

        if (lightChild != null) lightChild.SetActive(true);

        if (torchFlicker != null)
        {
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

    // ──────────────────────────────────────────────────────────────
    // Persistent state (IPersistentStructureState)
    // ──────────────────────────────────────────────────────────────

    [Serializable]
    private class SaveState
    {
        public float currentFuel;
    }

    /// <summary>
    /// Called by PlacedStructureManager during save. Captures currentFuel;
    /// burn state is re-derived on load so it stays consistent with thresholds
    /// even if those thresholds are later retuned in the Inspector.
    /// </summary>
    public string SerializeState()
    {
        var s = new SaveState { currentFuel = currentFuel };
        return JsonUtility.ToJson(s);
    }

    /// <summary>
    /// Called by PlacedStructureManager after Instantiate, before Start.
    /// Restores fuel and applies the resulting burn state instantly
    /// (no extinguish sound on load).
    /// </summary>
    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var s = JsonUtility.FromJson<SaveState>(json);
            if (s == null) return;

            currentFuel = Mathf.Clamp(s.currentFuel, 0f, maxFuel);
            ApplyBurnState(EvaluateBurnState(), instant: true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Campfire] Failed to deserialize state: {e.Message}");
        }
    }
}