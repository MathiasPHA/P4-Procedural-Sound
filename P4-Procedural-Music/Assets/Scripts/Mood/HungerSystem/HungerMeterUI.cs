using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the player's hunger as a UI slider with color tinting.
///
///   Green  → well-fed (above satiated threshold)
///   Orange → normal range
///   Red    → hungry / starving
///
/// SETUP:
///   1. Attach to the hunger bar GameObject (same object as the Slider).
///   2. Assign fillImage to the slider's Fill rect Image component.
///   3. HungerSystem auto-finds if left empty.
/// </summary>
public class HungerMeterUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The UI Slider for hunger. Auto-finds on this GameObject if left empty.")]
    [SerializeField] private Slider hungerSlider;

    [Tooltip("The Image component on the slider's Fill rect. " +
             "Used for color tinting. Leave empty to skip tinting.")]
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color fullColor    = new Color(0.3f, 0.85f, 0.3f);  // green
    [SerializeField] private Color normalColor  = new Color(0.9f, 0.65f, 0.2f);  // orange
    [SerializeField] private Color starvingColor = new Color(0.9f, 0.2f, 0.2f);  // red

    private HungerSystem hungerSystem;

    private void Start()
    {
        hungerSystem = HungerSystem.Instance;
        if (hungerSystem == null)
            hungerSystem = FindObjectOfType<HungerSystem>();

        if (hungerSlider == null)
            hungerSlider = GetComponent<Slider>();

        if (hungerSlider != null)
            hungerSlider.interactable = false;
    }

    private void Update()
    {
        if (hungerSystem == null) return;

        float hunger = hungerSystem.Hunger;

        if (hungerSlider != null)
            hungerSlider.value = hunger;

        if (fillImage != null)
        {
            // Two-stage lerp: starving→normal→full
            if (hunger < 0.5f)
                fillImage.color = Color.Lerp(starvingColor, normalColor, hunger / 0.5f);
            else
                fillImage.color = Color.Lerp(normalColor, fullColor, (hunger - 0.5f) / 0.5f);
        }
    }
}
