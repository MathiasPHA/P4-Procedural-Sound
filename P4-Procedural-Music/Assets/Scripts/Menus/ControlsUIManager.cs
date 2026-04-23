using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Controls panel in the pause menu.
/// Displays rebindable bindings from the Player action map and
/// lets the player reassign them at runtime. Overrides are saved
/// to PlayerPrefs and reloaded on Awake.
/// </summary>
public class ControlsUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private Transform bindingContainer;   // parent of all binding rows
    [SerializeField] private GameObject bindingRowPrefab;  // see BindingRow.cs

    [Header("Rebind overlay")]
    [SerializeField] private GameObject listeningOverlay;  // "Press any key..." overlay
    [SerializeField] private TextMeshProUGUI listeningLabel;

    private InputActionRebindingExtensions.RebindingOperation _currentRebind;
    private const string SaveKey = "InputOverrides";

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        LoadOverrides();
    }

    private void OnEnable()
    {
        BuildRows();
    }

    // ── Public API (called by pause menu buttons) ─────────────────────────────

    public void OpenPanel()
    {
        controlsPanel.SetActive(true);
        BuildRows();
    }

    public void ClosePanel()
    {
        CancelCurrentRebind();
        controlsPanel.SetActive(false);
    }

    public void ResetAll()
    {
        inputActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(SaveKey);
        BuildRows();
    }

    // ── Building the UI ───────────────────────────────────────────────────────

    private void BuildRows()
    {
        // Clear existing rows
        foreach (Transform child in bindingContainer)
            Destroy(child.gameObject);

        var playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);

        foreach (var action in playerMap.actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];

                // Skip composite parents (e.g. the "WASD" wrapper itself)
                // but show their individual parts (up/down/left/right)
                if (binding.isComposite) continue;

                // Skip gamepad bindings — keyboard/mouse only
                if (binding.path.Contains("Gamepad")) continue;

                string actionLabel = action.name;

                // For composite parts, label them "Move (Up)" etc.
                if (binding.isPartOfComposite)
                    actionLabel = $"{action.name}  <size=70%><alpha=#88>({binding.name})</size>";

                var row = Instantiate(bindingRowPrefab, bindingContainer);
                var rowCtrl = row.GetComponent<BindingRow>();
                int capturedIndex = i;
                rowCtrl.Setup(
                    actionLabel,
                    InputControlPath.ToHumanReadableString(
                        binding.effectivePath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice),
                    () => StartRebind(action, capturedIndex, rowCtrl)
                );
            }
        }
    }

    // ── Rebinding ─────────────────────────────────────────────────────────────

    private void StartRebind(InputAction action, int bindingIndex, BindingRow row)
    {
        CancelCurrentRebind();

        // Disable the action map while listening so no input fires during rebind
        action.actionMap.Disable();

        ShowListeningOverlay($"Rebinding \"{action.name}\" ...\nPress any key — Escape to cancel");

        _currentRebind = action
            .PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Gamepad>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op =>
            {
                HideListeningOverlay();
                action.actionMap.Enable();
                _currentRebind?.Dispose();
                _currentRebind = null;
                SaveOverrides();
                BuildRows();
            })
            .OnCancel(op =>
            {
                HideListeningOverlay();
                action.actionMap.Enable();
                _currentRebind?.Dispose();
                _currentRebind = null;
                BuildRows();
            })
            .Start();
    }

    private void CancelCurrentRebind()
    {
        _currentRebind?.Cancel();
        _currentRebind?.Dispose();
        _currentRebind = null;
        HideListeningOverlay();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void SaveOverrides()
    {
        var json = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void LoadOverrides()
    {
        if (PlayerPrefs.HasKey(SaveKey))
            inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(SaveKey));
    }

    // ── Overlay helpers ───────────────────────────────────────────────────────

    private void ShowListeningOverlay(string msg)
    {
        if (listeningOverlay == null) return;
        listeningLabel.text = msg;
        listeningOverlay.SetActive(true);
    }

    private void HideListeningOverlay()
    {
        if (listeningOverlay == null) return;
        listeningOverlay.SetActive(false);
    }

    private void OnDestroy() => CancelCurrentRebind();
}
