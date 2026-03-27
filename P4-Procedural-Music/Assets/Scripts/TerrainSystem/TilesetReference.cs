using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProceduralTerrain
{
    /// <summary>
    /// Maps tsx tile IDs to Unity TileBase assets imported by SuperTiled2Unity.
    /// Create via: Assets > Create > Procedural Terrain > Tileset Reference
    /// Then drag your imported tile assets into the entries list.
    /// </summary>
    [CreateAssetMenu(fileName = "TilesetRef", menuName = "Procedural Terrain/Tileset Reference")]
    public class TilesetReference : ScriptableObject
    {
        [System.Serializable]
        public struct TileEntry
        {
            [Tooltip("The tile ID from the .tsx file (e.g. 15 for W_Mid)")]
            public int tsxTileId;

            [Tooltip("The TileBase asset imported by SuperTiled2Unity")]
            public TileBase tile;
        }

        [Tooltip("Map each tsx tile ID to its imported Unity TileBase.")]
        public List<TileEntry> entries = new List<TileEntry>();

        [Tooltip("Fallback tile used when a tsx ID has no mapping (pink/error tile).")]
        public TileBase fallbackTile;

        private Dictionary<int, TileBase> _lookup;

        /// <summary>
        /// Get the Unity TileBase for a given tsx tile ID.
        /// Returns fallbackTile if not mapped.
        /// </summary>
        public TileBase GetTile(int tsxTileId)
        {
            if (_lookup == null)
                BuildLookup();

            if (_lookup.TryGetValue(tsxTileId, out var tile))
                return tile;

            if (fallbackTile != null)
                return fallbackTile;

            Debug.LogWarning($"[TilesetReference] No tile mapped for tsx ID {tsxTileId} and no fallback set.");
            return null;
        }

        /// <summary>
        /// Check if a tsx tile ID has a mapping.
        /// </summary>
        public bool HasTile(int tsxTileId)
        {
            if (_lookup == null)
                BuildLookup();
            return _lookup.ContainsKey(tsxTileId);
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<int, TileBase>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.tile != null)
                    _lookup[entry.tsxTileId] = entry.tile;
            }
        }

        private void OnValidate()
        {
            // Rebuild lookup when edited in Inspector
            _lookup = null;
        }
    }
}
