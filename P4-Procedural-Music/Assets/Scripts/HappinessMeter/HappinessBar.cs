using UnityEngine;
using UnityEngine.UI;

public class HappinessBar : MonoBehaviour
{
    [Header("Happiness Settings")]
    [SerializeField] private float maxHappiness = 100f;
    [SerializeField] private float currentHappiness = 100f;
    [SerializeField] private float smoothSpeed = 5f;

    [Header("UI References")]
    [SerializeField] private Slider happinessSlider;
    [SerializeField] private Image comfortImage; // Assign a UI Image in the Inspector

    [Header("Comfort Sprites")]
    [SerializeField] private Sprite veryComfySprite;  // >= 80%
    [SerializeField] private Sprite comfySprite;       // >= 60%
    [SerializeField] private Sprite contentSprite;     // >= 40%
    [SerializeField] private Sprite uneasySprite;      // >= 20%
    [SerializeField] private Sprite horrifiedSprite;   // <  20%

    private float targetHappiness;

    public float CurrentHappiness => currentHappiness;
    public float MaxHappiness => maxHappiness;

    private void Start()
    {
        targetHappiness = currentHappiness;

        if (happinessSlider != null)
        {
            happinessSlider.maxValue = maxHappiness;
            happinessSlider.value = currentHappiness;
        }

        UpdateComfortImage();
    }

    private void Update()
    {
        currentHappiness = Mathf.Lerp(currentHappiness, targetHappiness, Time.deltaTime * smoothSpeed);
        currentHappiness = Mathf.Clamp(currentHappiness, 0f, maxHappiness);

        if (happinessSlider != null)
            happinessSlider.value = currentHappiness;

        UpdateComfortImage();
    }

    /// <summary>
    /// Called by ComfortSystem every frame with the total comfort delta for this tick.
    /// A positive value raises happiness; negative lowers it.
    /// Scaled by Time.deltaTime internally — pass a per-second rate.
    /// </summary>
    public void ApplyComfortDelta(float delta)
    {
        targetHappiness = Mathf.Clamp(targetHappiness + delta * Time.deltaTime, 0f, maxHappiness);
    }

    /// <summary>
    /// Apply an instant one-shot change to happiness (not time-scaled).
    /// Use for discrete events: mob hits, eating food, picking up items.
    /// Negative values reduce happiness, positive values restore it.
    /// </summary>
    public void ApplyInstantDelta(float delta)
    {
        targetHappiness = Mathf.Clamp(targetHappiness + delta, 0f, maxHappiness);
    }

    /// <summary>
    /// Sets happiness directly (e.g. for initialization or cutscenes).
    /// </summary>
    public void SetHappiness(float value)
    {
        targetHappiness = Mathf.Clamp(value, 0f, maxHappiness);
    }

    private void UpdateComfortImage()
    {
        if (comfortImage == null) return;

        float normalized = currentHappiness / maxHappiness;

        Sprite newSprite;

        if      (normalized >= 0.80f) newSprite = veryComfySprite;
        else if (normalized >= 0.60f) newSprite = comfySprite;
        else if (normalized >= 0.40f) newSprite = contentSprite;
        else if (normalized >= 0.20f) newSprite = uneasySprite;
        else                          newSprite = horrifiedSprite;

        // Only reassign if the sprite has actually changed, to avoid unnecessary dirty calls
        if (newSprite != null && comfortImage.sprite != newSprite)
            comfortImage.sprite = newSprite;
    }
}