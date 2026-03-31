using System;
using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Serializable data for a single player-placed structure.
    /// Stored by PlacedStructureManager in world_structures.json.
    /// </summary>
    [Serializable]
    public class StructureSaveData
    {
        [Tooltip("The item ID from PlaceableData.item.id — used to look up the prefab on load.")]
        public string itemId;

        [Tooltip("World position where the structure was placed.")]
        public float posX;
        public float posY;

        public StructureSaveData() { }

        public StructureSaveData(string itemId, Vector3 worldPos)
        {
            this.itemId = itemId;
            this.posX = worldPos.x;
            this.posY = worldPos.y;
        }

        public Vector3 GetPosition() => new Vector3(posX, posY, 0f);
    }
}
