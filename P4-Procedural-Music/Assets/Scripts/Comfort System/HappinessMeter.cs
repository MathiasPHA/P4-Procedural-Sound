using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects a UI Slider to the ComfortSystem's happiness value.
/// Attach this to the same GameObject as your Slider, or assign the slider in the inspector.
/// </summary>
public class HappinessMeter : MonoBehaviour
{
    [Tooltip("The ComfortSystem on the player. Auto-finds if left empty.")]
    [SerializeField] private ComfortSystem comfortSystem;

    [Tooltip("The UI Slider to display happiness. Auto-finds on this GameObject if left empty.")]
    [SerializeField] private Slider happinessSlider;

    private void Start()
    {
        if (comfortSystem == null)
            comfortSystem = FindObjectOfType<ComfortSystem>();

        if (happinessSlider == null)
            happinessSlider = GetComponent<Slider>();

        // Make sure the slider can't be dragged by the player
        if (happinessSlider != null)
            happinessSlider.interactable = false;
    }

    private void Update()
    {
        if (comfortSystem != null && happinessSlider != null)
            happinessSlider.value = comfortSystem.Happiness;
    }
}
