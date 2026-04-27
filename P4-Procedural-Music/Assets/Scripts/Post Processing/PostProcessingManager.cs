using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessingManager : MonoBehaviour
{
    [Header("Volume Reference")]
    public Volume globalVolume;

    [Header("Master Control")]
    [Range(0f, 1f)] public float masterIntensity = 0f;

    [Header("Overlay Sprite")]
    public CanvasGroup overlayCanvasGroup; 
    
    [Header("Fade In Settings")]
    [SerializeField] float minimumIntensity;
    [SerializeField] float maximumIntensity;

    [Header("Tentacle")]
    public TentacleAttack tentacleAttack;
    public float tentacleTriggerThreshold = 0.1f;

    private Vignette vignette;
    private FilmGrain filmGrain;
    private bool tentacleTriggered = false;

    void Start()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out filmGrain);
        }
    }

    public void SetMasterIntensity(float value)
    {
        masterIntensity = value;
        ApplyIntensity();
    }

    void OnValidate() => ApplyIntensity();

    void ApplyIntensity()
{
    if (vignette != null)   vignette.intensity.value   = masterIntensity;
    if (filmGrain != null)  filmGrain.intensity.value  = masterIntensity;
    overlayCanvasGroup.alpha = Mathf.InverseLerp(minimumIntensity, maximumIntensity, masterIntensity);

    Debug.Log($"Intensity: {masterIntensity} | TentacleAssigned: {tentacleAttack != null} | Triggered: {tentacleTriggered}");

    if (tentacleAttack != null)
        {
        if (masterIntensity >= tentacleTriggerThreshold && !tentacleTriggered)
        {
            tentacleTriggered = true;
            tentacleAttack.Trigger(Random.insideUnitCircle.normalized);
        }

        if (masterIntensity < tentacleTriggerThreshold)
            tentacleTriggered = false;
        }
    }
}
