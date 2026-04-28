#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using InventorySystem.Data;

namespace InventorySystem.Editor
{
    [CustomEditor(typeof(ItemDatabase))]
    public class ItemDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();
            
            ItemDatabase database = (ItemDatabase)target;
            
            // Add space before button
            EditorGUILayout.Space(10);
            
            // Big button to auto-populate
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f); // Light blue
            if (GUILayout.Button("Auto-Populate From Project", GUILayout.Height(40)))
            {
                AutoPopulateDatabase(database);
            }
            GUI.backgroundColor = Color.white;
            
            // Help box
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Click the button above to automatically find and add all ItemData assets in your project.",
                MessageType.Info
            );
        }
        
        private void AutoPopulateDatabase(ItemDatabase database)
        {
            // Get the serialized property for the allItems list
            SerializedObject so = new SerializedObject(database);
            SerializedProperty itemsProperty = so.FindProperty("allItems");
            
            // Clear existing items
            itemsProperty.ClearArray();
            
            // Find all ItemData assets in the project
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            
            int addedCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                
                if (item != null)
                {
                    // Add to array
                    itemsProperty.InsertArrayElementAtIndex(itemsProperty.arraySize);
                    SerializedProperty element = itemsProperty.GetArrayElementAtIndex(itemsProperty.arraySize - 1);
                    element.objectReferenceValue = item;
                    addedCount++;
                }
            }
            
            // Apply changes
            so.ApplyModifiedProperties();
            
            // Mark dirty and save
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            
            // Show result
            Debug.Log($"[ItemDatabase] Auto-populated {addedCount} ItemData assets.");
            EditorUtility.DisplayDialog(
                "Auto-Populate Complete",
                $"Successfully added {addedCount} ItemData assets to the database.",
                "OK"
            );
        }
    }
}
#endif