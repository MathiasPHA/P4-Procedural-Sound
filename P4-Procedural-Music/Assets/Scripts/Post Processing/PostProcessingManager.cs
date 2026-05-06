using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProceduralMusic.Core;
using ProceduralMusic.Bridge;

public class PostProcessingManager : MonoBehaviour
{
    [Header("Volume Reference")]
    public Volume globalVolume;

    [Header("Music")]
    public ProceduralMusicController musicController;
    public float musicTriggerSpookyThreshold = 0.2f;
    public float musicTriggerHorrorThreshold = 0.6f;
    public GameMusicState defaultMusicState = GameMusicState.Exploring;

    [Header("Master Control")]
    [Range(0f, 1f)] public float masterIntensity = 0f;

    [Header("Overlay Sprites")]
    public CanvasGroup overlayEyes;
    public CanvasGroup overlayTentacles;
    public CanvasGroup overlayTentacles2;
    public float overlayTentacleSpawn;
    public float overlayTentacleSpawn2;

    [Header("Fade In Settings")]
    [SerializeField] float minimumIntensity;
    [SerializeField] float maximumIntensity;

    [Header("Tentacle")]
    public TentacleAttack tentacleAttack;
    public float tentacleTriggerThreshold = 0.8f;

    private Vignette vignette;
    private FilmGrain filmGrain;
    private bool tentacleTriggered = false;
    private bool isReady = false;

    public static bool EntityIsActive { get; private set; } = false;

    void Start()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out filmGrain);
        }
        isReady = true;
    }

    public void SetMasterIntensity(float value)
    {
        masterIntensity = value;
        ApplyIntensity();
    }

    public void SetIntensity(float value)
    {
        masterIntensity = Mathf.Clamp01(value);
        ApplyIntensity();
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        ApplyIntensity();
    }

    void ApplyIntensity()
    {
        if (vignette != null) vignette.intensity.value = masterIntensity;
        if (filmGrain != null) filmGrain.intensity.value = masterIntensity;

        float alpha = Mathf.InverseLerp(minimumIntensity, maximumIntensity, masterIntensity);
        if (overlayEyes != null) overlayEyes.alpha = alpha;

        if (masterIntensity > overlayTentacleSpawn)
        {
            if (overlayTentacles != null) overlayTentacles.alpha = 1f;
        }
        else
        {
            if (overlayTentacles != null) overlayTentacles.alpha = 0f;
        }

        if (masterIntensity > overlayTentacleSpawn2)
        {
            if (overlayTentacles2 != null) overlayTentacles2.alpha = 1f;
        }
        else
        {
            if (overlayTentacles2 != null) overlayTentacles2.alpha = 0f;
        }


        EntityIsActive = masterIntensity >= musicTriggerSpookyThreshold;

        if (musicController != null)
        {
            if (masterIntensity >= musicTriggerHorrorThreshold)
                musicController.SetGameState(GameMusicState.Horror);
            else if (masterIntensity >= musicTriggerSpookyThreshold)
                musicController.SetGameState(GameMusicState.Spooky);
            else
                musicController.SetGameState(defaultMusicState);
        }

        // Tentacles trigger at higher threshold
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