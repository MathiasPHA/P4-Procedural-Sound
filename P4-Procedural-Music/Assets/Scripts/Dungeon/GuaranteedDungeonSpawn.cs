/// <summary>
/// Marker component added at runtime to dungeon objects spawned from a
/// DungeonSpawnEntry with guaranteedSpawn = true.
/// Tells DungeonObjectSpawner.RemoveOverlapping() to never cull this object,
/// so bosses and unique NPCs always appear regardless of seed or collider overlap.
/// </summary>
public class GuaranteedDungeonSpawn : UnityEngine.MonoBehaviour { }
