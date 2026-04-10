#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using InventorySystem.Data;

/// <summary>
/// Custom inspector for CampfireController.
/// Adds a live fuel bar, quick-add fuel buttons, and an extinguish button.
/// Drop this file inside an Editor/ folder (or it will be stripped automatically
/// by the #if UNITY_EDITOR guard above).
/// </summary>
[CustomEditor(typeof(CampfireController))]
public class CampfireEditor : Editor
{
    // ── ScriptableObject slot for quick-adding fuel in play mode ──
    private ItemData _testFuelItem;
    private int      _testQuantity = 1;

    public override void OnInspectorGUI()
    {
        CampfireController campfire = (CampfireController)target;

        // Draw the default inspector first
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("── Runtime Debug ──", EditorStyles.boldLabel);

        // Fuel bar
        if (Application.isPlaying)
        {
            float ratio = campfire.CurrentFuel / Mathf.Max(campfire.maxFuel, 1f);
            Rect barRect = GUILayoutUtility.GetRect(18, 20, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(barRect, ratio,
                $"Fuel: {campfire.CurrentFuel:F1} / {campfire.maxFuel}  [{campfire.CurrentBurnState}]");

            EditorGUILayout.Space(6);

            // Quick-add fuel
            EditorGUILayout.LabelField("Quick Add Fuel", EditorStyles.miniBoldLabel);
            _testFuelItem = (ItemData)EditorGUILayout.ObjectField("Fuel Item (ItemData)", _testFuelItem,
                typeof(ItemData), false);
            _testQuantity = EditorGUILayout.IntSlider("Quantity", _testQuantity, 1, 20);

            using (new EditorGUI.DisabledScope(_testFuelItem == null || !_testFuelItem.isFuel))
            {
                string label = _testFuelItem == null ? "—"
                    : !_testFuelItem.isFuel ? $"{_testFuelItem.displayName} (not a fuel!)"
                    : _testFuelItem.displayName;
                if (GUILayout.Button($"Add {_testQuantity}x {label}"))
                    campfire.AddFuel(_testFuelItem, _testQuantity);
            }

            EditorGUILayout.Space(4);

            // Preset quick buttons
            EditorGUILayout.LabelField("Set Fuel Directly", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Full"))      campfire.SetFuel(campfire.maxFuel);
            if (GUILayout.Button("Big"))       campfire.SetFuel(campfire.bigThreshold + 5f);
            if (GUILayout.Button("Medium"))    campfire.SetFuel(campfire.mediumThreshold + 5f);
            if (GUILayout.Button("Low"))       campfire.SetFuel(5f);
            if (GUILayout.Button("Extinguish")) campfire.Extinguish();
            EditorGUILayout.EndHorizontal();

            // Force repaint every frame while playing so the bar updates
            Repaint();
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the debug controls.", MessageType.Info);
        }
    }
}
#endif
