using System.Collections.Generic;
using UnityEngine;

namespace MobSystem.Data
{
    /// <summary>
    /// Central registry that maps mob string IDs to their MobData ScriptableObjects.
    /// Used by MobSpawnManager for lookups and future save/load support.
    ///
    /// CREATE: Right-click → Create → Mobs → Mob Database
    ///
    /// Populate by dragging all MobData assets into the inspector list,
    /// or use the "Auto-Populate From Project" context menu in the editor.
    /// </summary>
    [CreateAssetMenu(fileName = "MobDatabase", menuName = "Mobs/Mob Database")]
    public class MobDatabase : ScriptableObject
    {
        [SerializeField] private List<MobData> allMobs = new List<MobData>();

        private Dictionary<string, MobData> _lookup;

        /// <summary>
        /// Build the fast lookup dictionary. Call once at startup
        /// (e.g. from MobSpawnManager.Start or a bootstrap script).
        /// </summary>
        public void Initialise()
        {
            _lookup = new Dictionary<string, MobData>(allMobs.Count);

            foreach (var mob in allMobs)
            {
                if (mob == null) continue;

                if (_lookup.ContainsKey(mob.id))
                {
                    Debug.LogWarning($"[MobDatabase] Duplicate mob id '{mob.id}' — skipping {mob.name}");
                    continue;
                }

                _lookup[mob.id] = mob;
            }

            Debug.Log($"[MobDatabase] Initialised with {_lookup.Count} mob types.");
        }

        public MobData GetById(string id)
        {
            if (_lookup == null)
            {
                Debug.LogError("[MobDatabase] Not initialised. Call Initialise() first.");
                return null;
            }

            _lookup.TryGetValue(id, out var data);
            return data;
        }

        public bool TryGetById(string id, out MobData data)
        {
            if (_lookup == null)
            {
                Debug.LogError("[MobDatabase] Not initialised. Call Initialise() first.");
                data = null;
                return false;
            }

            return _lookup.TryGetValue(id, out data);
        }

        public IReadOnlyList<MobData> AllMobs => allMobs;

#if UNITY_EDITOR
        /// <summary>
        /// Finds every MobData asset in the project and populates allMobs.
        /// Click "Auto-Populate From Project" in the Inspector context menu.
        /// </summary>
        [ContextMenu("Auto-Populate From Project")]
        private void AutoPopulate()
        {
            allMobs.Clear();

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:MobData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var mob = UnityEditor.AssetDatabase.LoadAssetAtPath<MobData>(path);
                if (mob != null)
                    allMobs.Add(mob);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[MobDatabase] Auto-populated {allMobs.Count} mob types.");
        }
#endif
    }
}
