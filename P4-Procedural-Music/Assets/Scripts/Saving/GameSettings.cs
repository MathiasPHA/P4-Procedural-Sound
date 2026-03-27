using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lightweight persistent object that carries world selection across scenes.
/// Place in the main menu scene. Do NOT place in the game scene.
/// </summary>
public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    [Header("Default values (overridden at runtime by main menu)")]
    public string worldName = "default";
    public int seed = 12345;

    [Tooltip("Name of the game scene to load when StartGame() is called.")]
    public string gameSceneName = "Game";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Call this from your save slot UI before loading the game scene.
    /// </summary>
    public void StartGame(string world, int seed)
    {
        this.worldName = world;
        this.seed = seed;
        SceneManager.LoadScene(gameSceneName);
    }
}
