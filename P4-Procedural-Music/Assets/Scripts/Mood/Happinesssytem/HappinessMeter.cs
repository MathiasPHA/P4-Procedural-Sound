using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects the happiness UI to the new MoodSystem / HappinessSystem.
///
///   Slider → HappinessSystem.Happiness  (the bar the player watches drain/fill)
///   Smiley → MoodSystem.CurrentTier     (tells the player which direction the bar is trending)
///
/// The smiley now reflects MOOD, not happiness. This is intentional — it gives
/// the player advance warning: "my smiley is frowning, I should eat / find shelter
/// before my happiness bar starts draining."
/// </summary>
public class HappinessMeter : MonoBehaviour
{
    [Header("Slider")]
    [Tooltip("The UI Slider to display happiness. Auto-finds on this GameObject if left empty.")]
    [SerializeField] private Slider happinessSlider;

    [Header("Mood Smiley")]
    [Tooltip("UI Image that displays a sprite reflecting the player's current mood tier")]
    [SerializeField] private Image moodImage;

    [Header("Mood Sprites (mapped to MoodTier)")]
    [SerializeField] private Sprite veryComfySprite;     // MoodTier.Elated
    [SerializeField] private Sprite comfySprite;    // MoodTier.Content
    [SerializeField] private Sprite contentSprite;    // MoodTier.Neutral
    [SerializeField] private Sprite uneasySprite;     // MoodTier.Uneasy
    [SerializeField] private Sprite horrifiedSprite;  // MoodTier.Miserable

    private HappinessSystem happinessSystem;
    private MoodSystem moodSystem;

    private void Start()
    {
        happinessSystem = HappinessSystem.Instance;
        if (happinessSystem == null)
            happinessSystem = FindObjectOfType<HappinessSystem>();

        moodSystem = MoodSystem.Instance;
        if (moodSystem == null)
            moodSystem = FindObjectOfType<MoodSystem>();

        if (happinessSlider == null)
            happinessSlider = GetComponent<Slider>();

        if (happinessSlider != null)
            happinessSlider.interactable = false;

        UpdateMoodImage();
    }

    private void Update()
    {
        if (happinessSystem != null && happinessSlider != null)
            happinessSlider.value = happinessSystem.Happiness;

        UpdateMoodImage();
    }

    private void UpdateMoodImage()
    {
        if (moodImage == null || moodSystem == null) return;

        Sprite newSprite = moodSystem.CurrentTier switch
        {
            MoodTier.Elated    => veryComfySprite,
            MoodTier.Content   => comfySprite,
            MoodTier.Neutral   => contentSprite,
            MoodTier.Uneasy    => uneasySprite,
            MoodTier.Miserable => horrifiedSprite,
            _                  => null
        };

        if (newSprite != null && moodImage.sprite != newSprite)
            moodImage.sprite = newSprite;
    }
}