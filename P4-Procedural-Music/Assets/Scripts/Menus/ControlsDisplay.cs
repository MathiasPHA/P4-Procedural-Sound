using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Reads the Player action map and selected UI actions, displaying each
/// binding as a text row. Groups duplicate composite parts into one row.
/// No rebinding — display only.
/// </summary>
public class ControlsDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform rowContainer;

    [Header("Style")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color labelColor = Color.black;
    [SerializeField] private int fontSize = 18;
    [SerializeField] private float rowHeight = 45f;

    // UI actions to include by name
    private static readonly string[] IncludedUIActions = { "Toggle Inventory", "Pause" };

    private void OnEnable()
    {
        BuildRows();
    }

    private void BuildRows()
    {
        foreach (Transform child in rowContainer)
            Destroy(child.gameObject);

        var grouped = new Dictionary<string, List<string>>();
        var order = new List<string>();

        // ── Player map ────────────────────────────────────────────────────────
        var playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
        foreach (var action in playerMap.actions)
            ProcessAction(action, grouped, order);

        // ── Selected UI actions ───────────────────────────────────────────────
        var uiMap = inputActions.FindActionMap("UI", throwIfNotFound: true);
        foreach (var actionName in IncludedUIActions)
        {
            var action = uiMap.FindAction(actionName);
            if (action != null)
                ProcessAction(action, grouped, order);
        }

        // ── Build rows ────────────────────────────────────────────────────────
        foreach (var rowKey in order)
        {
            string keys = string.Join(" / ", grouped[rowKey]);
            CreateRow(rowKey, keys);
        }
    }

    private void ProcessAction(InputAction action, Dictionary<string, List<string>> grouped, List<string> order)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite) continue;
            if (binding.path.Contains("Gamepad")) continue;

            string rowKey = binding.isPartOfComposite
                ? $"{action.name} ({binding.name})"
                : action.name;

            string keyName = InputControlPath.ToHumanReadableString(
                binding.effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);

            if (!grouped.ContainsKey(rowKey))
            {
                grouped[rowKey] = new List<string>();
                order.Add(rowKey);
            }

            if (!string.IsNullOrEmpty(keyName) && !grouped[rowKey].Contains(keyName))
                grouped[rowKey].Add(keyName);
        }
    }

    private void CreateRow(string actionName, string keyName)
    {
        var row = new GameObject("Row_" + actionName);
        row.transform.SetParent(rowContainer, false);

        var rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, rowHeight);

        var hlg = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 10;
        hlg.padding = new RectOffset(10, 10, 0, 0);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var rowLE = row.AddComponent<UnityEngine.UI.LayoutElement>();
        rowLE.minHeight = rowHeight;
        rowLE.preferredHeight = rowHeight;

        // Action name label
        var labelGO = new GameObject("ActionLabel");
        labelGO.transform.SetParent(row.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = actionName;
        labelTMP.fontSize = fontSize;
        labelTMP.color = labelColor;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        if (font != null) labelTMP.font = font;
        var labelLE = labelGO.AddComponent<UnityEngine.UI.LayoutElement>();
        labelLE.flexibleWidth = 1;

        // Key name label
        var keyGO = new GameObject("KeyLabel");
        keyGO.transform.SetParent(row.transform, false);
        var keyTMP = keyGO.AddComponent<TextMeshProUGUI>();
        keyTMP.text = string.IsNullOrEmpty(keyName) ? "—" : keyName;
        keyTMP.fontSize = fontSize;
        keyTMP.color = labelColor;
        keyTMP.alignment = TextAlignmentOptions.MidlineRight;
        if (font != null) keyTMP.font = font;
        var keyLE = keyGO.AddComponent<UnityEngine.UI.LayoutElement>();
        keyLE.minWidth = 150;
        keyLE.preferredWidth = 150;
    }
}