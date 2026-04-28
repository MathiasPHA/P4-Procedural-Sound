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

        [Tooltip("Optional serialized per-instance state (e.g. campfire fuel). " +
                 "Empty for stateless structures. Written/read via IPersistentStructureState.")]
        public string stateJson = "";

        [Tooltip("Persisted HP. 0 = unset (load at full HP from PlaceableData.maxHealth). " +
                 "Backward compatible with saves predating the structure HP system.")]
        public int health = 0;

        public StructureSaveData() { }

        public StructureSaveData(string itemId, Vector3 worldPos)
        {
            this.itemId = itemId;
            this.posX = worldPos.x;
            this.posY = worldPos.y;
        }

        public StructureSaveData(string itemId, Vector3 worldPos, string stateJson)
        {
            this.itemId = itemId;
            this.posX = worldPos.x;
            this.posY = worldPos.y;
            this.stateJson = stateJson ?? "";
        }

        public StructureSaveData(string itemId, Vector3 worldPos, string stateJson, int health)
        {
            this.itemId = itemId;
            this.posX = worldPos.x;
            this.posY = worldPos.y;
            this.stateJson = stateJson ?? "";
            this.health = health;
        }

        public Vector3 GetPosition() => new Vector3(posX, posY, 0f);
    }
}