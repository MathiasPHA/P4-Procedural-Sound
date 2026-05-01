using UnityEngine;
using UnityEditor;

public class NullReferenceScanner
{
    [MenuItem("Tools/Scan Null Serialized References")]
    static void Scan()
    {
        int issues = 0;

        var objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (var go in objects)
        {
            if (go.hideFlags != HideFlags.None) continue;

            var components = go.GetComponents<Component>();

            foreach (var comp in components)
            {
                if (comp == null)
                {
                    Debug.LogError($"Missing script on {GetPath(go)}", go);
                    issues++;
                    continue;
                }

                SerializedObject so = new SerializedObject(comp);
                var prop = so.GetIterator();

                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
                        {
                            Debug.LogError($"Broken reference in {comp.GetType().Name} on {GetPath(go)} → {prop.name}", go);
                            issues++;
                        }
                    }
                }
            }
        }

        Debug.Log($"Scan complete. Issues found: {issues}");
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        while (go.transform.parent != null)
        {
            go = go.transform.parent.gameObject;
            path = go.name + "/" + path;
        }
        return path;
    }
}