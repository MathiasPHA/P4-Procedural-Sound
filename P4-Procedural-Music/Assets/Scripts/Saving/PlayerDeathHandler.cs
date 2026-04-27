using System.Collections;
using UnityEngine;

/// <summary>
/// Handles the player's death sequence. Subscribes to
/// HappinessSystem.OnHappinessDepleted, then:
///   1. Zeroes the player's velocity
///   2. Disables gameplay scripts you assign
///   3. Plays an optional death sound
///   4. PERMADEATH: wipes the world's save folder
///   5. Fades in a death canvas
/// </summary>
public class PlayerDeathHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HappinessSystem happinessSystem;
    [SerializeField] private CanvasGroup deathCanvasGroup;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private MonoBehaviour[] componentsToDisable;

    [Header("Fade")]
    [SerializeField] private float delayBeforeFade = 0.4f;
    [SerializeField] private float fadeInDuration = 2.0f;
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip deathSound;
    [Range(0f, 1f)]
    [SerializeField] private float deathVolume = 1f;

    [Header("Permadeath")]
    [Tooltip("If true, the world's save folder is wiped when the player dies.")]
    [SerializeField] private bool deleteWorldOnDeath = true;

    [Header("Debug")]
    [Tooltip("Log every step of the death sequence to the console.")]
    [SerializeField] private bool verboseLogging = true;

    private bool hasDied;
    private Coroutine fadeRoutine;

    private void Start()
    {
        if (happinessSystem == null)
            happinessSystem = HappinessSystem.Instance;

        if (happinessSystem == null)
        {
            Debug.LogError("[PlayerDeathHandler] No HappinessSystem found — death will never trigger.");
            return;
        }

        happinessSystem.OnHappinessDepleted += HandleDeath;
        Log("Subscribed to HappinessSystem.OnHappinessDepleted.");

        if (deathCanvasGroup != null)
        {
            deathCanvasGroup.alpha = 0f;
            deathCanvasGroup.interactable = false;
            deathCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            Debug.LogWarning("[PlayerDeathHandler] No CanvasGroup assigned — fade will be skipped.");
        }
    }

    private void OnDestroy()
    {
        if (happinessSystem != null)
            happinessSystem.OnHappinessDepleted -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (hasDied)
        {
            Log("HandleDeath called but already dead — ignoring.");
            return;
        }
        hasDied = true;

        Log("=== DEATH SEQUENCE STARTED ===");

        // 1. Stop the player.
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero; // change to .velocity for Unity 2022 LTS
            playerRigidbody.angularVelocity = 0f;
            Log("Step 1: Player rigidbody zeroed.");
        }
        else
        {
            Debug.LogWarning("[PlayerDeathHandler] No Rigidbody2D assigned.");
        }

        // 2. Disable gameplay scripts.
        if (componentsToDisable != null && componentsToDisable.Length > 0)
        {
            int disabled = 0;
            foreach (var c in componentsToDisable)
            {
                if (c != null) { c.enabled = false; disabled++; }
            }
            Log($"Step 2: Disabled {disabled} gameplay scripts.");
        }
        else
        {
            Debug.LogWarning("[PlayerDeathHandler] componentsToDisable is empty — player won't actually freeze.");
        }

        // 3. Death sound.
        if (deathSound != null)
        {
            PlayOneShotAt(deathSound, transform.position, deathVolume);
            Log("Step 3: Death sound played.");
        }

        // 4. PERMADEATH — wipe the world.
        if (deleteWorldOnDeath)
        {
            if (SaveSystemManager.Instance != null)
            {
                Log("Step 4: Calling SaveSystemManager.DeleteCurrentWorld()...");
                bool deleted = SaveSystemManager.Instance.DeleteCurrentWorld();
                Log($"Step 4: DeleteCurrentWorld returned {deleted}.");
            }
            else
            {
                Debug.LogError("[PlayerDeathHandler] deleteWorldOnDeath is ON but SaveSystemManager.Instance is NULL — world NOT wiped!");
            }
        }
        else
        {
            Log("Step 4: Skipped (deleteWorldOnDeath = false).");
        }

        // 5. Fade in the death canvas.
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeCanvas(0f, 1f, fadeInDuration, delayBeforeFade,
                                                blocksRaycastsAfter: true,
                                                interactableAfter: true));
        Log("Step 5: Fade started.");
    }

    public void ResetForRespawn()
    {
        hasDied = false;

        if (componentsToDisable != null)
        {
            foreach (var c in componentsToDisable)
                if (c != null) c.enabled = true;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeCanvas(
            from: deathCanvasGroup != null ? deathCanvasGroup.alpha : 1f,
            to: 0f,
            duration: fadeOutDuration,
            delay: 0f,
            blocksRaycastsAfter: false,
            interactableAfter: false));
    }

    private IEnumerator FadeCanvas(float from, float to, float duration, float delay,
                                   bool blocksRaycastsAfter, bool interactableAfter)
    {
        if (deathCanvasGroup == null) yield break;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (to > from) deathCanvasGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            deathCanvasGroup.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        deathCanvasGroup.alpha = to;
        deathCanvasGroup.blocksRaycasts = blocksRaycastsAfter;
        deathCanvasGroup.interactable = interactableAfter;
        fadeRoutine = null;
    }

    private static void PlayOneShotAt(AudioClip clip, Vector3 position, float volume)
    {
        var go = new GameObject("OneShotAudio_Death");
        go.transform.position = position;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = Random.Range(0.92f, 1.08f);
        src.spatialBlend = 0f;
        src.Play();

        Object.Destroy(go, clip.length / Mathf.Max(0.01f, src.pitch) + 0.1f);
    }

    private void Log(string msg)
    {
        if (verboseLogging)
            Debug.Log($"[PlayerDeathHandler] {msg}");
    }
}