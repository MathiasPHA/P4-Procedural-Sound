using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Data
{
    /// <summary>
    /// Central registry that maps item string IDs to their ItemData ScriptableObjects.
    /// Used for save/load resolution and any lookup-by-id need.
    /// 
    /// Populate either by dragging all ItemData assets into the inspector list,
    /// or by loading them from a Resources folder / Addressables at startup.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemData> allItems = new();

        private Dictionary<string, ItemData> _lookup;

        /// <summary>
        /// Must be called once at game startup (e.g. from a bootstrap MonoBehaviour)
        /// to build the fast lookup dictionary.
        /// </summary>
        public void Initialise()
        {
            _lookup = new Dictionary<string, ItemData>(allItems.Count);

            foreach (var item in allItems)
            {
                if (item == null) continue;

                if (_lookup.ContainsKey(item.id))
                {
                    Debug.LogWarning($"[ItemDatabase] Duplicate item id '{item.id}' — skipping {item.name}");
                    continue;
                }

                _lookup[item.id] = item;
            }

            Debug.Log($"[ItemDatabase] Initialised with {_lookup.Count} items.");
        }

        public ItemData GetById(string id)
        {
            if (_lookup == null)
            {
                Debug.LogError("[ItemDatabase] Not initialised. Call Initialise() first.");
                return null;
            }

            _lookup.TryGetValue(id, out var data);
            return data;
        }

        public bool TryGetById(string id, out ItemData data)
        {
            if (_lookup == null)
            {
                Debug.LogError("[ItemDatabase] Not initialised. Call Initialise() first.");
                data = null;
                return false;
            }

            return _lookup.TryGetValue(id, out data);
        }

        /// <summary>
        /// Resolves a save-data struct back into a full runtime ItemInstance.
        /// Returns null if the item id is not found (e.g. item was removed between versions).
        /// </summary>
        public ItemInstance ResolveInstance(ItemInstanceSaveData saveData)
        {
            if (!TryGetById(saveData.itemId, out var data)) return null;

            var instance = new ItemInstance(data);

            // Restore mutable state
            if (data.hasInstanceState)
            {
                // Use repair to set durability (clamped to current max, safe across balance changes)
                int delta = saveData.currentDurability - instance.CurrentDurability;
                if (delta > 0) instance.Repair(delta);
                else if (delta < 0) instance.ReduceDurability(-delta);
            }

            return instance;
        }

        public IReadOnlyList<ItemData> AllItems => allItems;

#if UNITY_EDITOR
        /// <summary>
        /// Finds every ItemData asset in the project and populates allItems automatically.
        /// Click "Auto-Populate From Project" in the Inspector to run this.
        /// </summary>
        [ContextMenu("Auto-Populate From Project")]
        private void AutoPopulate()
        {
            allItems.Clear();

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null)
                    allItems.Add(item);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[ItemDatabase] Auto-populated {allItems.Count} items.");
        }
#endif
    }
}