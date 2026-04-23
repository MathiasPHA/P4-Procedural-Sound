namespace ProceduralTerrain
{
    /// <summary>
    /// Implemented by MonoBehaviours on placed-structure prefabs that need to
    /// persist per-instance runtime state across save/load (e.g. campfire fuel,
    /// drying rack contents, furnace progress).
    ///
    /// The save pipeline in <see cref="PlacedStructureManager"/> handles this
    /// transparently:
    ///   - On save: SerializeState() is called and the returned JSON is stored
    ///     in <see cref="StructureSaveData.stateJson"/>.
    ///   - On load: DeserializeState(json) is called immediately after the
    ///     structure is instantiated, before its first Update tick.
    ///
    /// Stateless structures (walls, signs, etc.) do NOT implement this — they
    /// just persist position and item ID like before.
    ///
    /// IMPLEMENTATION PATTERN:
    ///   Use a private [Serializable] data class and JsonUtility.
    ///   Return "" from SerializeState if there's nothing meaningful to persist.
    ///   DeserializeState should handle null/empty input gracefully.
    /// </summary>
    public interface IPersistentStructureState
    {
        /// <summary>Serialize this structure's runtime state to a JSON string.</summary>
        string SerializeState();

        /// <summary>Restore runtime state from a JSON string produced by SerializeState.</summary>
        void DeserializeState(string json);
    }
}
