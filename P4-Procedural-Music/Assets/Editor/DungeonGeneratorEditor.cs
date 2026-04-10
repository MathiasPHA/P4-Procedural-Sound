#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Custom inspector for DungeonGenerator.
/// Adds an "Auto-Populate from Tileset" button that loads tile sub-assets
/// from a SuperTiled2Unity .tsx import and fills the tileset array by ID.
/// Same pattern as TilesetReferenceEditor in the overworld.
/// </summary>
[CustomEditor(typeof(DungeonGenerator))]
public class DungeonGeneratorEditor : Editor
{
    private Object _tsxAsset;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Tileset Auto-Populate", EditorStyles.boldLabel);

        _tsxAsset = EditorGUILayout.ObjectField(
            "Tileset (.tsx asset)",
            _tsxAsset,
            typeof(Object),
            false);

        EditorGUILayout.HelpBox(
            "Drag your imported Dungeon/Forest Tileset (.tsx) asset here.\n" +
            "SuperTiled2Unity stores tiles as sub-assets inside it.\n" +
            "This extracts all tiles and maps them by ID automatically.",
            MessageType.Info);

        if (GUILayout.Button("Auto-Populate from Tileset"))
        {
            if (_tsxAsset == null)
            {
                EditorUtility.DisplayDialog("No Asset",
                    "Drag your .tsx tileset asset into the field above first.", "OK");
                return;
            }

            AutoPopulate();
        }
    }

    private void AutoPopulate()
    {
        var generator = (DungeonGenerator)target;
        string path = AssetDatabase.GetAssetPath(_tsxAsset);

        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("Error", "Could not find asset path.", "OK");
            return;
        }

        // Load all sub-assets from the .tsx file
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

        int maxId = 0;
        int matched = 0;

        // First pass: find the highest tile ID to size the array
        foreach (var asset in allAssets)
        {
            if (asset is TileBase)
            {
                Match match = Regex.Match(asset.name, @"(\d+)$");
                if (match.Success)
                {
                    int id = int.Parse(match.Groups[1].Value);
                    if (id > maxId) maxId = id;
                }
            }
        }

        if (maxId == 0)
        {
            EditorUtility.DisplayDialog("No Tiles Found",
                "No TileBase sub-assets with numeric IDs found.\n\n" +
                "Expected names like 'Forest_Tileset.Tile.1' or 'Dungeon_Tileset_15'.", "OK");
            return;
        }

        // Create array sized to fit the highest ID
        TileBase[] tiles = new TileBase[maxId + 1];

        // Second pass: populate the array
        foreach (var asset in allAssets)
        {
            if (asset is TileBase tile)
            {
                Match match = Regex.Match(asset.name, @"(\d+)$");
                if (match.Success)
                {
                    int id = int.Parse(match.Groups[1].Value);
                    tiles[id] = tile;
                    matched++;
                    Debug.Log($"[DungeonGenerator] Mapped tile ID {id} → {asset.name}");
                }
                else
                {
                    Debug.LogWarning($"[DungeonGenerator] Could not extract ID from '{asset.name}'");
                }
            }
        }

        // Apply to the serialized property
        Undo.RecordObject(generator, "Auto-Populate Dungeon Tileset");
        SerializedObject so = new SerializedObject(generator);
        SerializedProperty tilesetProp = so.FindProperty("tileset");

        tilesetProp.arraySize = tiles.Length;
        for (int i = 0; i < tiles.Length; i++)
        {
            tilesetProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(generator);

        EditorUtility.DisplayDialog("Done",
            $"Mapped {matched} tiles (array size: {tiles.Length}).\n\n" +
            "Check the Console for the full mapping.", "OK");
    }
}
#endif
