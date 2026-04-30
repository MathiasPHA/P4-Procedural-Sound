using UnityEngine;
using UnityEngine.InputSystem;
using InventorySystem.Building;
using InventorySystem.UI;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;

    [Header("Pause Menus")]
    public GameObject pauseMenuUI;
    public GameObject OptionsUI;
    public GameObject ExitUI;

    [Header("Settings Sub-panels (closed on Resume)")]
    [SerializeField] private GameObject settingsPopup;
    [SerializeField] private GameObject controlsUI;
    [SerializeField] private GameObject soundUI;
    [SerializeField] private GameObject mainMenuUI;

    [Header("Audio")]
    public MixerVolumeController volumeController;

    [Header("UI Coordinator (for Escape priority chain)")]
    [Tooltip("UICoordinator that owns open/close state for inventory + crafting panels.")]
    [SerializeField] private UICoordinator uiCoordinator;

    private float[] savedVolumes;

    /// <summary>
    /// Poll Escape directly in Update instead of going through the Input System's
    /// action binding. This bypasses action map switching — when the inventory
    /// opens and DisableMovement() is called, the Pause action can become
    /// unavailable. Polling Keyboard.current works regardless of action state.
    /// </summary>
    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        HandleEscape();
    }

    /// <summary>
    /// Escape key priority chain:
    ///   1. If placing a structure  → PlacementSystem cancels placement (we skip pause)
    ///   2. If inventory/crafting is open → close both via coordinator (we skip pause)
    ///   3. Otherwise → toggle pause menu
    /// </summary>
    private void HandleEscape()
    {
        // 1. Placement mode — PlacementSystem.Update() handles Escape itself.
        //    Skip pausing so the pause menu doesn't pop up over the ghost.
        if (PlacementSystem.Instance != null && PlacementSystem.Instance.IsPlacing)
            return;

        // 2. Panels open — close them together via coordinator instead of pausing.
        if (uiCoordinator != null && uiCoordinator.ArePanelsOpen)
        {
            uiCoordinator.ClosePanels();
            return;
        }

        // 3. Default — toggle pause
        if (isPaused) Resume();
        else Pause();
    }

    void Pause()
    {
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
        if (settingsPopup != null) settingsPopup.SetActive(false);
        if (controlsUI != null)    controlsUI.SetActive(false);
        if (soundUI != null)       soundUI.SetActive(false);
        if (mainMenuUI != null)    mainMenuUI.SetActive(false);
        if (OptionsUI.activeSelf)  OptionsUI.SetActive(false);
        if (ExitUI.activeSelf)     ExitUI.SetActive(false);

        for (int i = 0; i < volumeController.groups.Length; i++)
        {
            var g = volumeController.groups[i];
            float vol = PlayerPrefs.GetFloat(g.exposedParam, g.defaultVolume);
            float dB  = Mathf.Log10(Mathf.Max(vol, 0.001f)) * 20f;
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