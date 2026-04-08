using UnityEngine;
using TMPro;

namespace InteractionSystem
{
    /// <summary>
    /// Floating prompt that appears above interactable objects showing the action verb.
    /// Uses a screen-space overlay Canvas so it's always readable regardless of zoom.
    ///
    /// SETUP:
    ///   1. Create a Canvas (Screen Space - Overlay) named "InteractionPromptCanvas"
    ///   2. Add a child with TextMeshProUGUI — assign it to promptText
    ///   3. Optionally add a background Image behind the text for readability
    ///   4. Attach this script to the Canvas root
    ///   5. Wire promptText in Inspector
    ///   6. Drag this into InteractionDetector's promptUI field
    ///
    /// The prompt follows the interactable's PromptPosition converted to screen space.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private RectTransform promptRoot;
        [SerializeField] private Camera mainCamera;

        [Header("Display")]
        [Tooltip("Format string. {0} is replaced with the action verb.")]
        [SerializeField] private string format = "{0}";

        [Tooltip("Screen-space Y offset above the calculated position (pixels).")]
        [SerializeField] private float screenYOffset = 20f;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (promptRoot == null && promptText != null)
                promptRoot = promptText.rectTransform;

            Hide();
        }

        /// <summary>Show the prompt with the given verb at the given world position.</summary>
        public void Show(string actionVerb, Vector3 worldPosition)
        {
            if (promptText == null) return;

            promptText.text = string.Format(format, actionVerb);
            promptRoot.gameObject.SetActive(true);

            // Convert world position to screen position
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPosition);

            // If behind camera, hide
            if (screenPos.z < 0)
            {
                promptRoot.gameObject.SetActive(false);
                return;
            }

            screenPos.y += screenYOffset;
            promptRoot.position = screenPos;
        }

        /// <summary>Hide the prompt.</summary>
        public void Hide()
        {
            if (promptRoot != null)
                promptRoot.gameObject.SetActive(false);
        }
    }
}
