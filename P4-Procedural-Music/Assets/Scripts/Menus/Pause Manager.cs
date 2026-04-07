using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;
    public GameObject pauseMenuUI;
    public GameObject OptionsUI;
    public GameObject ExitUI;
    public MixerVolumeController volumeController;

    private float[] savedVolumes;

    private void OnPause(InputValue value)
    {
        if (isPaused) Resume();
        else Pause();
    }

    void Pause()
    {
        // Save current dB values
        savedVolumes = new float[volumeController.groups.Length];
        for (int i = 0; i < volumeController.groups.Length; i++)
        {
            var g = volumeController.groups[i];
            g.mixer.GetFloat(g.exposedParam, out savedVolumes[i]);
            g.mixer.SetFloat(g.exposedParam, -80f);
        }

        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void Resume()
    {
        if (OptionsUI.activeSelf || ExitUI.activeSelf)
        {
            OptionsUI.SetActive(false);
            ExitUI.SetActive(false);
        }

        // Restore from PlayerPrefs (picks up any settings changes)
        for (int i = 0; i < volumeController.groups.Length; i++)
        {
            var g = volumeController.groups[i];
            float vol = PlayerPrefs.GetFloat(g.exposedParam, g.defaultVolume);
            float dB = Mathf.Log10(Mathf.Max(vol, 0.001f)) * 20f;
            g.mixer.SetFloat(g.exposedParam, dB);
        }

        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }

    public void QuitGame() => Application.Quit();

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}