using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Reflection;

/// <summary>
/// Sets Collider Type on all SuperTile assets in Forest_Tileset.
/// Uses reflection since SuperTile is not the built-in Tile class.
/// Place this script in an Editor folder.
/// </summary>
public class WaterTileColliderFixer : AssetPostprocessor
{
    [MenuItem("Tools/Fix Water Tile Colliders")]
    private static void ManualFix()
    {
        string[] guids = AssetDatabase.FindAssets("Forest_Tileset", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".tsx")) continue;

            FixColliderTypes(path);
            return;
        }
        Debug.LogWarning("[WaterTileColliderFixer] Could not find Forest_Tileset.tsx");
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (!path.Contains("Forest_Tileset")) continue;
            string assetPath = path;
            EditorApplication.delayCall += () => FixColliderTypes(assetPath);
        }
    }

    private static void FixColliderTypes(string path)
    {
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        bool changed = false;

        foreach (Object asset in subAssets)
        {
            if (!(asset is TileBase)) continue;

            var type = asset.GetType();

            // Log all fields and properties to find the collider type
            // Try common field/property names
            string[] possibleNames = {
                "m_ColliderType", "colliderType", "m_colliderType",
                "ColliderType", "m_TileColliderType"
            };

            // Check fields (including private)
            bool found = false;
            foreach (string name in possibleNames)
            {
                var field = type.GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                if (field != null)
                {
                    var currentValue = field.GetValue(asset);
                    Debug.Log($"[WaterTileColliderFixer] Found field '{name}' on {asset.name} = {currentValue} (type: {field.FieldType})");

                    // Set to Grid (enum value 2)
                    if (field.FieldType == typeof(Tile.ColliderType))
                    {
                        field.SetValue(asset, Tile.ColliderType.Sprite);
                    }
                    else
                    {
                        field.SetValue(asset, 1); // Sprite = 1
                    }

                    EditorUtility.SetDirty(asset);
                    changed = true;
                    found = true;
                    break;
                }
            }

            // Check properties
            if (!found)
            {
                foreach (string name in possibleNames)
                {
                    var prop = type.GetProperty(name,
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                    if (prop != null && prop.CanWrite)
                    {
                        var currentValue = prop.GetValue(asset);
                        Debug.Log($"[WaterTileColliderFixer] Found property '{name}' on {asset.name} = {currentValue}");

                        if (prop.PropertyType == typeof(Tile.ColliderType))
                        {
                            prop.SetValue(asset, Tile.ColliderType.Sprite);
                        }
                        else
                        {
                            prop.SetValue(asset, 1);
                        }

                        EditorUtility.SetDirty(asset);
                        changed = true;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                // Dump all members so we can find the right one
                Debug.Log($"[WaterTileColliderFixer] Could not find collider field on {asset.name}. Dumping all members of {type.FullName}:");
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                {
                    Debug.Log($"  Field: {f.Name} ({f.FieldType})");
                }
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                {
                    Debug.Log($"  Property: {p.Name} ({p.PropertyType})");
                }
                break; // Only dump once
            }
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[WaterTileColliderFixer] Tiles updated successfully.");
        }
    }
}