using UnityEngine;
using InteractionSystem;

/// <summary>
/// Spawned in the last room of a generated dungeon.
/// Player interacts to return to the overworld.
/// </summary>
public class DungeonExit : Interactable
{
    public override void Interact(PlayerStateManager player)
    {
        if (DungeonManager.Instance == null)
        {
            Debug.LogError("[DungeonExit] DungeonManager not found!");
            return;
        }

        SaveSystemManager.Instance?.SaveModifiedChunks();

        DungeonManager.Instance.ExitDungeon();
    }
}