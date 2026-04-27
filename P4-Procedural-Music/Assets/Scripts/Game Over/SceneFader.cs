using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance;

    [SerializeField] private Image fadePanel;   // A full-screen black Image
    [SerializeField] private float fadeDuration = 1f;

    private void Awake()
    {
        // Singleton — persists across scenes
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Always fade IN when a scene loads
        StartCoroutine(FadeIn());
    }

    public void GoToScene(string sceneName)
    {
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        yield return StartCoroutine(FadeOut());         // Fade to black
        yield return SceneManager.LoadSceneAsync(sceneName); // Load scene
        // FadeIn runs automatically in Start() on the new scene
    }

    private IEnumerator FadeOut()
    {
        float t = 0f;
        Color c = fadePanel.color;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Clamp01(t / fadeDuration);
            fadePanel.color = c;
            yield return null;
        }
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        Color c = fadePanel.color;
        c.a = 1f;
        fadePanel.color = c;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            c.a = 1f - Mathf.Clamp01(t / fadeDuration);
            fadePanel.color = c;
            yield return null;
        }
    }
}