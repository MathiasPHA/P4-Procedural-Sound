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

    [Header("Audio")]
    public MixerVolumeController volumeController;

    [Header("UI Managers (for Escape priority chain)")]
    [Tooltip("When Escape is pressed and the inventory is open, close it instead of pausing.")]
    [SerializeField] private InventoryUIManager inventoryUI;

    [Tooltip("When Escape is pressed and the crafting panel is open, close it instead of pausing.")]
    [SerializeField] private CraftingUIManager craftingUI;

    private float[] savedVolumes;

    /// <summary>
    /// Poll Escape directly in Update instead of going through the Input System's
    /// action binding. This bypasses action map switching — when the inventory
    /// opens and DisableMovement() is called, the Pause action can become
    /// unavailable. Polling Keyboard.current works regardless of action state.
    ///
    /// Runs with Time.timeScale = 0 because this script uses Update (not
    /// FixedUpdate) and Keyboard.current is timescale-independent.
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
    ///   2. If inventory/crafting is open → close them (we skip pause)
    ///   3. Otherwise → toggle pause menu
    /// </summary>
    private void HandleEscape()
    {
        // 1. Placement mode — PlacementSystem.Update() handles Escape itself.
        //    Skip pausing so the pause menu doesn't pop up over the ghost.
        if (PlacementSystem.Instance != null && PlacementSystem.Instance.IsPlacing)
            return;

        // 2. Inventory/crafting open — close them instead of pausing.
        bool inventoryOpen = inventoryUI != null && inventoryUI.IsInventoryOpen;
        bool craftingOpen = craftingUI != null && craftingUI.IsOpen;

        if (inventoryOpen || craftingOpen)
        {
            if (inventoryOpen)
                inventoryUI.ToggleInventory();

            if (craftingOpen)
                craftingUI.ClosePanel();

            return;
        }

        // 3. Default — toggle pause
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