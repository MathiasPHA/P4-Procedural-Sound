using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProceduralMusic.Bridge;

/// <summary>
/// Persistent loading-screen singleton. Fades a full-screen black canvas in,
/// fades the procedural music's master volume out, swaps scenes, then reverses
/// the fades on the other side.
///
/// Usage:
///   SceneTransition.Instance.LoadScene("Dungeon");
///
/// GameSettings.StartGame routes through this automatically when present.
///
/// Setup: place one prefab in your boot/main-menu scene. The prefab carries
/// its own Screen Space - Overlay Canvas with a full-screen black Image and
/// a CanvasGroup. Sort Order should be high (e.g. 9999) so it sits on top
/// of every other UI.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("References")]
    [Tooltip("Full-screen black overlay. Alpha 0 = visible game, alpha 1 = fully covered.")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("Timing")]
    [SerializeField] private float fadeOutDuration = 0.6f;
    [SerializeField] private float fadeInDuration = 0.8f;
    [Tooltip("Held black for this long after the new scene loads, before fading back in. Useful to mask scene-warmup hitches.")]
    [SerializeField] private float postLoadHold = 0.15f;

    [Header("Music")]
    [Tooltip("If true, lerps ProceduralMusicController's MasterVolume to 0 alongside the fade-out, then back to its original value on fade-in.")]
    [SerializeField] private bool fadeMusic = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private bool _isTransitioning;
    private float _restoreMusicVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }
        else
        {
            Debug.LogError("[SceneTransition] No fadeCanvasGroup assigned — fade will not render.");
        }
    }

    /// <summary>
    /// Public entry point. Triggers the full out-load-in cycle.
    /// Safe to call multiple times — re-entry is ignored while a transition is running.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (_isTransitioning)
        {
            Log($"LoadScene({sceneName}) ignored — transition already in progress.");
            return;
        }
        StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        _isTransitioning = true;
        Log($"=== Transition to '{sceneName}' START ===");

        // --- Fade out (black in, music out) ---
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.interactable = true;
        }

        // Capture current music volume so we can restore it after the load.
        if (fadeMusic && ProceduralMusicController.Instance != null)
        {
            _restoreMusicVolume = ProceduralMusicController.Instance.MasterVolume;
        }

        yield return Fade(0f, 1f, fadeOutDuration, fadeMusicTo: 0f, fadeMusicFrom: _restoreMusicVolume);

        // --- Load the new scene ---
        Log($"Loading scene '{sceneName}'...");
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        // Brief hold on black so the new scene's first-frame hitches don't show.
        if (postLoadHold > 0f) yield return new WaitForSeconds(postLoadHold);

        // --- Fade in (black out, music back to its prior level) ---
        // ProceduralMusicController is DontDestroyOnLoad in this project, so the
        // same instance carries across — its MasterVolume needs restoring, not its
        // playback re-started. Whatever ComfortMusicBridge / game state pushes will
        // take over normally on the other side.
        yield return Fade(1f, 0f, fadeInDuration, fadeMusicTo: _restoreMusicVolume, fadeMusicFrom: 0f);

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }

        _isTransitioning = false;
        Log($"=== Transition to '{sceneName}' END ===");
    }

    private IEnumerator Fade(float alphaFrom, float alphaTo, float duration,
                             float fadeMusicFrom, float fadeMusicTo)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // unscaled so a paused game can still fade
            float k = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);

            if (fadeCanvasGroup != null)
                fadeCanvasGroup.alpha = Mathf.Lerp(alphaFrom, alphaTo, k);

            if (fadeMusic && ProceduralMusicController.Instance != null)
                ProceduralMusicController.Instance.MasterVolume = Mathf.Lerp(fadeMusicFrom, fadeMusicTo, k);

            yield return null;
        }

        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = alphaTo;
        if (fadeMusic && ProceduralMusicController.Instance != null)
            ProceduralMusicController.Instance.MasterVolume = fadeMusicTo;
    }

    private void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"[SceneTransition] {msg}");
    }
}
