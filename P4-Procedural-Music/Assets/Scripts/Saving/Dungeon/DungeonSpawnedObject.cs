using UnityEngine;

/// <summary>
/// Attached to each object spawned by DungeonObjectSpawner.
/// Tracks the spawn index for delta-save persistence.
/// When destroyed (harvested, picked up, etc.), registers
/// its index with DungeonDeltaTracker so it stays gone next visit.
/// </summary>
public class DungeonSpawnedObject : MonoBehaviour
{
    public int SpawnIndex { get; set; } = -1;

    private void OnDestroy()
    {
        // Don't track during scene unload (exiting dungeon)
        if (SpawnIndex < 0) return;
        if (!gameObject.scene.isLoaded) return;

        DungeonDeltaTracker.RecordRemoval(SpawnIndex);
    }
}
