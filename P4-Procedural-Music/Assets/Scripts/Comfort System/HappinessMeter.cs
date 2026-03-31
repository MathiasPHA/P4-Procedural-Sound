using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects a UI Slider and a comfort sprite to the ComfortSystem's happiness value.
/// Attach this to the same GameObject as your Slider, or assign references in the inspector.
/// </summary>
public class HappinessMeter : MonoBehaviour
{
    [Tooltip("The ComfortSystem on the player. Auto-finds if left empty.")]
    [SerializeField] private ComfortSystem comfortSystem;

    [Header("Slider")]
    [Tooltip("The UI Slider to display happiness. Auto-finds on this GameObject if left empty.")]
    [SerializeField] private Slider happinessSlider;

    [Header("Comfort Image")]
    [Tooltip("UI Image that displays a sprite reflecting the player's happiness level")]
    [SerializeField] private Image comfortImage;

    [Header("Comfort Sprites")]
    [SerializeField] private Sprite veryComfySprite;   // >= 80%
    [SerializeField] private Sprite comfySprite;        // >= 60%
    [SerializeField] private Sprite contentSprite;      // >= 40%
    [SerializeField] private Sprite uneasySprite;       // >= 20%
    [SerializeField] private Sprite horrifiedSprite;    // <  20%

    private void Start()
    {
        if (comfortSystem == null)
            comfortSystem = FindObjectOfType<ComfortSystem>();

        if (happinessSlider == null)
            happinessSlider = GetComponent<Slider>();

        // Make sure the slider can't be dragged by the player
        if (happinessSlider != null)
            happinessSlider.interactable = false;

        UpdateComfortImage();
    }

    private void Update()
    {
        if (comfortSystem == null) return;

        if (happinessSlider != null)
            happinessSlider.value = comfortSystem.Happiness;

        UpdateComfortImage();
    }

    private void UpdateComfortImage()
    {
        if (comfortImage == null || comfortSystem == null) return;

        float happiness = comfortSystem.Happiness;

        Sprite newSprite;

        if (happiness >= 0.80f) newSprite = veryComfySprite;
        else if (happiness >= 0.60f) newSprite = comfySprite;
        else if (happiness >= 0.40f) newSprite = contentSprite;
        else if (happiness >= 0.20f) newSprite = uneasySprite;
        else newSprite = horrifiedSprite;

        if (newSprite != null && comfortImage.sprite != newSprite)
            comfortImage.sprite = newSprite;
    }
}