#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProceduralTerrain.Editor
{
    [CustomEditor(typeof(TilesetReference))]
    public class TilesetReferenceEditor : UnityEditor.Editor
    {
        private UnityEngine.Object _tsxAsset;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Auto-Populate", EditorStyles.boldLabel);

            _tsxAsset = EditorGUILayout.ObjectField(
                "Tileset (.tsx asset)",
                _tsxAsset,
                typeof(UnityEngine.Object),
                false);

            EditorGUILayout.HelpBox(
                "Drag your imported Forest_Tileset (.tsx) asset here.\n" +
                "SuperTiled2Unity stores tiles as sub-assets inside it.\n" +
                "This will extract all tiles and map them by ID automatically.",
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
            var tilesetRef = (TilesetReference)target;
            string path = AssetDatabase.GetAssetPath(_tsxAsset);

            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Error", "Could not find asset path.", "OK");
                return;
            }

            // Load ALL sub-assets at this path (this is how SuperTiled2Unity stores tiles)
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

            var newEntries = new List<TilesetReference.TileEntry>();
            int matched = 0;

            Debug.Log($"[TilesetRef] Found {allAssets.Length} assets at '{path}'");

            foreach (var asset in allAssets)
            {
                Debug.Log($"[TilesetRef] Asset: '{asset.name}' Type: {asset.GetType().Name}");

                // Only grab TileBase assets
                if (asset is TileBase tile)
                {
                    string assetName = asset.name;

                    // Try multiple naming patterns:
                    // "Forest_Tileset.Tile.15", "Tile.15", "tile_15", "15"
                    Match match = Regex.Match(assetName, @"\.Tile\.(\d+)$");
                    if (!match.Success)
                        match = Regex.Match(assetName, @"Tile\.(\d+)$");
                    if (!match.Success)
                        match = Regex.Match(assetName, @"[_.](\d+)$");
                    if (!match.Success)
                        match = Regex.Match(assetName, @"(\d+)$");

                    if (match.Success)
                    {
                        int tileId = int.Parse(match.Groups[1].Value);
                        newEntries.Add(new TilesetReference.TileEntry
                        {
                            tsxTileId = tileId,
                            tile = tile,
                        });
                        matched++;
                        Debug.Log($"[TilesetRef] Mapped '{assetName}' -> ID {tileId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[TilesetRef] Could not extract ID from '{assetName}'");
                    }
                }
            }

            if (matched == 0)
            {
                // Fallback: try project-wide search for TileBase assets
                Debug.Log("[TilesetRef] No sub-assets found, trying project-wide search...");
                string[] guids = AssetDatabase.FindAssets("t:TileBase");
                foreach (string guid in guids)
                {
                    string tilePath = AssetDatabase.GUIDToAssetPath(guid);
                    // Load all assets at each path (handles sub-assets too)
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(tilePath);
                    foreach (var a in assets)
                    {
                        if (a is TileBase t)
                        {
                            string assetName = t.name;
                            Match match = Regex.Match(assetName, @"(\d+)");
                            if (match.Success)
                            {
                                int tileId = int.Parse(match.Groups[1].Value);
                                newEntries.Add(new TilesetReference.TileEntry
                                {
                                    tsxTileId = tileId,
                                    tile = t,
                                });
                                matched++;
                                Debug.Log($"[TilesetRef] Mapped '{assetName}' -> ID {tileId}");
                            }
                        }
                    }
                }
            }

            if (matched == 0)
            {
                EditorUtility.DisplayDialog("No Tiles Found",
                    "Could not find any TileBase assets.\n\n" +
                    "Check the Console — it logs every asset it found\n" +
                    "so we can see the exact naming format.", "OK");
                return;
            }

            newEntries.Sort((a, b) => a.tsxTileId.CompareTo(b.tsxTileId));

            Undo.RecordObject(tilesetRef, "Auto-Populate Tileset Reference");
            tilesetRef.entries = newEntries;
            EditorUtility.SetDirty(tilesetRef);

            EditorUtility.DisplayDialog("Done",
                $"Mapped {matched} tiles successfully.\n\n" +
                "Check the entries list and Console to verify.", "OK");
        }
    }
}
#endif