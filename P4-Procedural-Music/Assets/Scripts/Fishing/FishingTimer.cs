using UnityEngine;
using UnityEngine.UI;

namespace FishingSystem
{
    /// <summary>
    /// Radial countdown shown during a fishing session. Reads TimeLeft / InitialTime
    /// from the FishingMinigame and depletes a UI Image (configured as
    /// Type=Filled, FillMethod=Radial360) from full to empty.
    ///
    /// Color is interpolated through three stops as time runs out — green
    /// while plenty remains, yellow in the middle, red when nearly out.
    /// Tweak the stops in the inspector if you want a different curve.
    ///
    /// SETUP:
    ///   1. Add a child "TimerCanvas" to the FishingSystem prefab — Canvas component
    ///      set to Render Mode = World Space, with a small Rect Transform and any
    ///      sorting layer that draws above the underwater scene.
    ///   2. Add a child Image under it. Set Source Image to UI/Skin/Knob.psd
    ///      (built into Unity) or any circular sprite you have.
    ///      Set Image Type = Filled, Fill Method = Radial 360, Fill Origin = Top,
    ///      Clockwise = checked.
    ///   3. Attach this script to the canvas (or anywhere on the prefab) and drag
    ///      the FishingMinigame and the Image into the inspector slots.
    ///   4. Assign FishingTimer to FishingManager's "Timer" field (added below).
    /// </summary>
    public class FishingTimer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FishingMinigame minigame;
        [Tooltip("UI Image set to Type=Filled, FillMethod=Radial360. fillAmount is driven 0–1 each frame.")]
        [SerializeField] private Image fillImage;

        [Header("Color Stops (high time -> low time)")]
        [SerializeField] private Color colorFull   = new Color(0.30f, 0.85f, 0.30f); // green
        [SerializeField] private Color colorMid    = new Color(0.95f, 0.85f, 0.20f); // yellow
        [SerializeField] private Color colorLow    = new Color(0.90f, 0.20f, 0.20f); // red

        [Header("Display")]
        [Tooltip("Optional root to hide entirely between sessions. Leave blank to keep the timer visible always (it will just sit at 0).")]
        [SerializeField] private GameObject visualRoot;

        private bool _active;

        public void BeginSession(FishingMinigame mg)
        {
            minigame = mg;
            _active  = true;
            if (visualRoot != null) visualRoot.SetActive(true);
            UpdateVisual(1f); // start full
        }

        public void EndSession()
        {
            _active = false;
            if (visualRoot != null) visualRoot.SetActive(false);
        }

        private void Update()
        {
            if (!_active || minigame == null || !minigame.IsActive) return;
            if (minigame.InitialTime <= 0f) return;

            float t01 = Mathf.Clamp01(minigame.TimeLeft / minigame.InitialTime);
            UpdateVisual(t01);
        }

        private void UpdateVisual(float t01)
        {
            if (fillImage == null) return;

            fillImage.fillAmount = t01;

            // Three-stop gradient: 1.0 → 0.5 = green→yellow, 0.5 → 0.0 = yellow→red.
            Color c = t01 > 0.5f
                ? Color.Lerp(colorMid,  colorFull, (t01 - 0.5f) * 2f)
                : Color.Lerp(colorLow,  colorMid,  t01 * 2f);
            fillImage.color = c;
        }
    }
}
