using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HappinessBar : MonoBehaviour
{
    [Header("Happiness Settings")]
    [SerializeField] private float maxHappiness = 100f;
    [SerializeField] private float currentHappiness = 100f;
    [SerializeField] private float smoothSpeed = 5f; // How fast the bar animates

    [Header("UI References")]
    [SerializeField] private Slider happinessSlider;
    [SerializeField] private TextMeshProUGUI comfortLabel; // Assign a TMP text in the UI

    // The target the bar smoothly moves toward
    private float targetHappiness;

    // Public read-only access for other scripts
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

        UpdateComfortLabel();
    }

    private void Update()
    {
        // Smoothly animate the slider toward the target value
        currentHappiness = Mathf.Lerp(currentHappiness, targetHappiness, Time.deltaTime * smoothSpeed);
        currentHappiness = Mathf.Clamp(currentHappiness, 0f, maxHappiness);

        if (happinessSlider != null)
            happinessSlider.value = currentHappiness;

        UpdateComfortLabel();
    }

    /// <summary>
    /// Called by ComfortSystem every frame with the total comfort delta for this tick.
    /// A positive value raises happiness; negative lowers it.
    /// </summary>
    public void ApplyComfortDelta(float delta)
    {
        targetHappiness = Mathf.Clamp(targetHappiness + delta * Time.deltaTime, 0f, maxHappiness);
    }

    /// <summary>
    /// Sets happiness directly (e.g. for initialization or cutscenes).
    /// </summary>
    public void SetHappiness(float value)
    {
        targetHappiness = Mathf.Clamp(value, 0f, maxHappiness);
    }

    private void UpdateComfortLabel()
    {
        if (comfortLabel == null) return;

        float normalized = currentHappiness / maxHappiness; // 0.0 – 1.0

        string label;
        Color labelColor;

        if (normalized >= 0.80f)
        {
            label = "Very Comfy";
            labelColor = new Color(0.2f, 0.85f, 0.4f); // bright green
        }
        else if (normalized >= 0.60f)
        {
            label = "Comfy";
            labelColor = new Color(0.6f, 0.9f, 0.3f); // yellow-green
        }
        else if (normalized >= 0.40f)
        {
            label = "Content";
            labelColor = new Color(1f, 0.85f, 0.2f); // yellow
        }
        else if (normalized >= 0.20f)
        {
            label = "Uneasy";
            labelColor = new Color(1f, 0.5f, 0.1f); // orange
        }
        else
        {
            label = "Horrified";
            labelColor = new Color(0.9f, 0.1f, 0.1f); // red
        }

        comfortLabel.text = label;
        comfortLabel.color = labelColor;
    }
}