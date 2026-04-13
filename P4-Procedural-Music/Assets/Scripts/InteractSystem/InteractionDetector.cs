using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace InteractionSystem
{
    /// <summary>
    /// Detects Interactable objects under the mouse cursor and exposes the current target.
    /// Attach to the Player GameObject.
    ///
    /// Does NOT handle clicking — ToolUseSystem checks CurrentTarget at the top
    /// of OnAttack and starts walk-to-interact if something is hovered.
    ///
    /// SETUP:
    ///   1. Attach to the Player
    ///   2. Set interactableLayer to the layers your Interactable objects are on
    ///   3. Assign promptUI (the InteractionPromptUI in the scene)
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractionPromptUI promptUI;
        [SerializeField] private Camera mainCamera;

        [Header("Detection")]
        [Tooltip("Layer(s) that Interactable objects live on.")]
        [SerializeField] private LayerMask interactableLayer;

        [Tooltip("Radius of the overlap check at mouse position.")]
        [SerializeField] private float detectionRadius = 0.2f;

        /// <summary>The currently hovered interactable, or null.</summary>
        public Interactable CurrentTarget { get; private set; }

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (promptUI == null)
                promptUI = FindObjectOfType<InteractionPromptUI>();
        }

        private void Update()
        {
            if (PauseManager.isPaused)
            {
                ClearTarget();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearTarget();
                return;
            }

            DetectUnderCursor();
            UpdatePrompt();
        }

        private void DetectUnderCursor()
        {
            Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            var hit = Physics2D.OverlapCircle(mouseWorld, detectionRadius, interactableLayer);

            if (hit != null)
            {
                var interactable = hit.GetComponent<Interactable>();
                if (interactable != null && interactable.CanInteract())
                {
                    CurrentTarget = interactable;
                    return;
                }
            }

            CurrentTarget = null;
        }

        private void UpdatePrompt()
        {
            if (promptUI == null) return;

            if (CurrentTarget != null)
                promptUI.Show(CurrentTarget.ActionVerb, CurrentTarget.PromptPosition);
            else
                promptUI.Hide();
        }

        private void ClearTarget()
        {
            CurrentTarget = null;
            if (promptUI != null)
                promptUI.Hide();
        }
    }
}