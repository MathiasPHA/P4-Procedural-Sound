using InteractionSystem;
using ProceduralTerrain;
using UnityEngine;

/// <summary>
/// Attach to a cave entrance structure in the overworld.
/// Extends your Interactable base class so it works with
/// InteractionDetector, hover prompts, and PlayerMoveToInteractState.
/// </summary>
public class DungeonEntrance : Interactable
{
    [Header("Dungeon")]
    [SerializeField] private DungeonConfig dungeonConfig;

    public override void Interact(PlayerStateManager player)
    {
        if (DungeonManager.Instance == null)
        {
            Debug.LogError("[DungeonEntrance] DungeonManager not found!");
            return;
        }

        if (dungeonConfig == null)
        {
            Debug.LogError("[DungeonEntrance] No DungeonConfig assigned!");
            return;
        }

        // Flush picked-up object state before the scene unloads —
        // overworld chunks won't go through UnloadChunk during a dungeon transition.
        if (ChunkManager.Instance != null)
        {
            Debug.Log("[DungeonEntrance] Flushing chunk object state before dungeon entry...");
            ChunkManager.Instance.FlushObjectState();
            Debug.Log("[DungeonEntrance] FlushObjectState complete.");
        }
        else
        {
            Debug.LogWarning("[DungeonEntrance] ChunkManager.Instance is null — object state NOT flushed!");
        }

        if (SaveSystemManager.Instance != null)
        {
            Debug.Log("[DungeonEntrance] Calling SaveModifiedChunks...");
            SaveSystemManager.Instance.SaveModifiedChunks();
            Debug.Log("[DungeonEntrance] SaveModifiedChunks complete.");
        }
        else
        {
            Debug.LogWarning("[DungeonEntrance] SaveSystemManager.Instance is null — world NOT saved!");
        }

        DungeonManager.Instance.EnterDungeon(
            dungeonConfig,
            transform.position,
            player.transform.position
        );
    }
}