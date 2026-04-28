using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Reads the Player action map and displays each binding as a text row.
/// Groups duplicate composite parts (e.g. WASD + Arrows) into one row.
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

    private void OnEnable()
    {
        BuildRows();
    }

    private void BuildRows()
    {
        foreach (Transform child in rowContainer)
            Destroy(child.gameObject);

        var playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);

        // Key: "ActionName (partName)" or "ActionName", Value: list of key strings
        var grouped = new Dictionary<string, List<string>>();
        var order = new List<string>();

        foreach (var action in playerMap.actions)
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

        foreach (var rowKey in order)
        {
            string keys = string.Join(" / ", grouped[rowKey]);
            CreateRow(rowKey, keys);
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