using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Controls panel. Builds binding rows entirely in code
/// so no BindingRow prefab is needed.
/// </summary>
public class ControlsUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private Transform bindingContainer;

    [Header("Rebind Overlay")]
    [SerializeField] private GameObject listeningOverlay;
    [SerializeField] private TextMeshProUGUI listeningLabel;

    [Header("Row Style")]
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Color buttonColor = new Color(1f, 0.85f, 0.5f, 1f);
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private int fontSize = 16;
    [SerializeField] private float rowHeight = 40f;

    private InputActionRebindingExtensions.RebindingOperation _currentRebind;
    private const string SaveKey = "InputOverrides";

    private void Awake() => LoadOverrides();
    private void OnEnable() => BuildRows();

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
                if (binding.isComposite) continue;
                if (binding.path.Contains("Gamepad")) continue;

                string label = binding.isPartOfComposite
                    ? $"{action.name} ({binding.name})"
                    : action.name;

                string keyName = InputControlPath.ToHumanReadableString(
                    binding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);

                int capturedIndex = i;
                CreateRow(label, keyName, action, capturedIndex);
            }
        }
    }

    private void CreateRow(string actionName, string keyName, InputAction action, int bindingIndex)
    {
        // Row container
        var row = new GameObject("Row_" + actionName);
        row.transform.SetParent(bindingContainer, false);

        var rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, rowHeight);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 8;
        hlg.padding = new RectOffset(5, 5, 0, 0);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = rowHeight;
        rowLE.preferredHeight = rowHeight;

        // Action label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(row.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = actionName;
        labelTMP.fontSize = fontSize;
        labelTMP.color = textColor;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1;

        // Key button
        var btnGO = new GameObject("KeyButton");
        btnGO.transform.SetParent(row.transform, false);
        var btnImage = btnGO.AddComponent<Image>();
        if (buttonSprite != null) btnImage.sprite = buttonSprite;
        btnImage.color = buttonColor;
        var btn = btnGO.AddComponent<Button>();
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.minWidth = 100;
        btnLE.preferredWidth = 100;

        // Key label inside button
        var keyLabelGO = new GameObject("KeyLabel");
        keyLabelGO.transform.SetParent(btnGO.transform, false);
        var keyRect = keyLabelGO.AddComponent<RectTransform>();
        keyRect.anchorMin = Vector2.zero;
        keyRect.anchorMax = Vector2.one;
        keyRect.offsetMin = Vector2.zero;
        keyRect.offsetMax = Vector2.zero;
        var keyTMP = keyLabelGO.AddComponent<TextMeshProUGUI>();
        keyTMP.text = string.IsNullOrEmpty(keyName) ? "—" : keyName;
        keyTMP.fontSize = fontSize - 2;
        keyTMP.color = textColor;
        keyTMP.alignment = TextAlignmentOptions.Center;

        // Wire button click
        btn.onClick.AddListener(() => StartRebind(action, bindingIndex, keyTMP));
    }

    private void StartRebind(InputAction action, int bindingIndex, TextMeshProUGUI keyLabel)
    {
        CancelCurrentRebind();
        action.actionMap.Disable();
        ShowListeningOverlay($"Rebinding \"{action.name}\"...\nPress any key — Escape to cancel");

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
                keyLabel.text = InputControlPath.ToHumanReadableString(
                    action.bindings[bindingIndex].effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
                SaveOverrides();
            })
            .OnCancel(op =>
            {
                HideListeningOverlay();
                action.actionMap.Enable();
                _currentRebind?.Dispose();
                _currentRebind = null;
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

    private void SaveOverrides()
    {
        PlayerPrefs.SetString(SaveKey, inputActions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    private void LoadOverrides()
    {
        if (PlayerPrefs.HasKey(SaveKey))
            inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(SaveKey));
    }

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
