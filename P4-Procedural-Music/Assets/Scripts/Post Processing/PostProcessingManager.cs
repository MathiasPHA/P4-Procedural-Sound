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
    public Slider masterSlider;

    [Header("Overlay Sprite")]
    public CanvasGroup overlayCanvasGroup; 
    
    [Header("Fade In Settings")]
    [SerializeField] float minimumIntensity;
    [SerializeField] float maximumIntensity;

    private Vignette vignette;
    private FilmGrain filmGrain;

    void Start()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out filmGrain);
        }

        if (masterSlider != null)
        {
            masterSlider.minValue = 0f;
            masterSlider.maxValue = 1f;
            masterSlider.value = masterIntensity;
            masterSlider.onValueChanged.AddListener(SetMasterIntensity);
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
    }
}