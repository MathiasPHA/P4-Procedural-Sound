using UnityEngine;
using InteractionSystem;

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

        DungeonManager.Instance.EnterDungeon(
            dungeonConfig,
            transform.position,
            player.transform.position
        );
    }
}
