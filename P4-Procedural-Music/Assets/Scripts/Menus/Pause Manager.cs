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
        // 0. Fishing — PlayerFishingState handles Escape itself (cancels the
        //    minigame). Skip pause so the menu doesn't pop up at the same time.
        if (FishingSystem.FishingManager.Instance != null &&
            FishingSystem.FishingManager.Instance.IsFishing)
            return;

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
        // Close all sub-panels so they don't reappear next pause
        if (settingsPopup != null) settingsPopup.SetActive(false);
        if (controlsUI != null) controlsUI.SetActive(false);
        if (soundUI != null) soundUI.SetActive(false);
        if (mainMenuUI != null) mainMenuUI.SetActive(false);
        if (OptionsUI.activeSelf) OptionsUI.SetActive(false);
        if (ExitUI.activeSelf) ExitUI.SetActive(false);

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