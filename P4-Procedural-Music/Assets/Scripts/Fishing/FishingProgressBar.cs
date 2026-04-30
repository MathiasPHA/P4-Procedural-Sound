using UnityEngine;

namespace FishingSystem
{
    /// <summary>
    /// Drives the vertical fill indicator on the right-hand bar of the fishing
    /// UI. The fill child's local Y is lerped between minPos and maxPos based
    /// on the current fishing progress (fishingPoints / targetPoints).
    ///
    /// Replaces ConsoleProgressBar.cs:
    ///   - No Debug.Log progress prints.
    ///   - No GameObject.Find — receives the FishingMinigame reference via BeginSession.
    ///   - Min/max Y are inspector-tunable instead of hardcoded.
    /// </summary>
    public class FishingProgressBar : MonoBehaviour
    {
        [Header("Fill")]
        [Tooltip("Child transform whose local Y is moved to indicate progress. " +
                 "Defaults to the first child if left empty.")]
        [SerializeField] private Transform fillTransform;

        [Header("Fill Range (local Y)")]
        [SerializeField] private float minY = -8.25f;
        [SerializeField] private float maxY = 8.25f;

        private FishingMinigame _minigame;
        private bool _active;

        private void Awake()
        {
            if (fillTransform == null && transform.childCount > 0)
                fillTransform = transform.GetChild(0);
        }

        public void BeginSession(FishingMinigame minigame)
        {
            _minigame = minigame;
            _active = true;
            // Snap to empty at session start so the previous fill doesn't flash.
            SetFill(0f);
        }

        public void EndSession()
        {
            _active = false;
            _minigame = null;
        }

        private void Update()
        {
            if (!_active || _minigame == null || !_minigame.IsActive) return;
            if (_minigame.targetPoints <= 0f) return;

            float progress = Mathf.Clamp01(_minigame.fishingPoints / _minigame.targetPoints);
            SetFill(progress);
        }

        private void SetFill(float progress01)
        {
            if (fillTransform == null) return;
            float y = Mathf.Lerp(minY, maxY, progress01);
            Vector3 p = fillTransform.localPosition;
            fillTransform.localPosition = new Vector3(p.x, y, p.z);
        }
    }
}