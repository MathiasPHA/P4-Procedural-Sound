using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using InventorySystem.Data;
using InventorySystem.Harvesting;

namespace InventorySystem.Tools
{
    /// <summary>
    /// Handles tool usage: detects harvestable resources in the player's
    /// facing direction and applies damage with the equipped tool on left-click.
    ///
    /// Reads the player's facing direction from PlayerStateManager.playerDir
    /// and the equipped item from the Inventory.
    ///
    /// SETUP:
    ///   1. Attach to the Player GameObject
    ///   2. Create ToolData assets for each tool and add them to the toolDatabase list
    ///   3. Set the resource layer mask to the layer your trees/rocks are on
    ///   4. Assign the PlayerStateManager reference
    /// </summary>
    public class ToolUseSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStateManager playerStateManager;

        [Header("Tool Database")]
        [Tooltip("All tool definitions. The system builds a lookup by item ID.")]
        [SerializeField] private List<ToolData> toolDatabase = new();

        [Header("Detection")]
        [Tooltip("Layer(s) that harvestable resources are on.")]
        [SerializeField] private LayerMask resourceLayer;

        [Tooltip("Radius of the interaction circle in front of the player.")]
        [SerializeField] private float interactRadius = 1.5f;

        [Tooltip("Offset from player center to the detection point.")]
        [SerializeField] private float detectionOffset = 0.5f;

        // Runtime
        private Dictionary<string, ToolData> _toolLookup = new();
        private Inventory _inventory;
        private float _cooldownTimer;

        private void Start()
        {
            // Build lookup
            foreach (var tool in toolDatabase)
            {
                if (tool == null || tool.item == null) continue;

                if (_toolLookup.ContainsKey(tool.item.id))
                {
                    Debug.LogWarning($"[ToolUseSystem] Duplicate tool for item '{tool.item.id}'.");
                    continue;
                }

                _toolLookup[tool.item.id] = tool;
            }

            _inventory = InventoryBootstrap.PlayerInventory;

            if (playerStateManager == null)
            {
                playerStateManager = GetComponent<PlayerStateManager>();
                if (playerStateManager == null)
                    Debug.LogError("[ToolUseSystem] PlayerStateManager not found!");
            }

            Debug.Log($"[ToolUseSystem] Init: {_toolLookup.Count} tools registered, " +
                      $"inventory={(_inventory != null ? "OK" : "NULL")}, " +
                      $"resourceLayer={resourceLayer.value}");
        }

        private void Update()
        {
            // Tick cooldown
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
                return;
            }

            // Don't process tool input when over UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    Debug.Log("[ToolUse] Click blocked — pointer is over UI element.");
                return;
            }

            // Check for left-click
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            TryUseTool();
        }

        private void TryUseTool()
        {
            if (_inventory == null)
            {
                Debug.LogWarning("[ToolUse] No inventory found.");
                return;
            }

            // Get equipped item
            var equippedInstance = _inventory.EquippedItem;
            if (equippedInstance == null)
            {
                Debug.Log("[ToolUse] No item equipped.");
                return;
            }

            if (equippedInstance.Data.category != ItemCategory.Tool)
            {
                Debug.Log($"[ToolUse] Equipped item '{equippedInstance.Data.displayName}' is not a Tool.");
                return;
            }

            // Look up tool data
            if (!_toolLookup.TryGetValue(equippedInstance.Data.id, out var toolData))
            {
                Debug.LogWarning($"[ToolUse] No ToolData found for '{equippedInstance.Data.id}'. " +
                                 "Is it in the Tool Database list?");
                return;
            }

            // Get facing direction from PlayerStateManager
            Vector2 facingDir = GetFacingDirection();
            if (facingDir == Vector2.zero)
            {
                Debug.Log($"[ToolUse] No facing direction. playerDir = '{playerStateManager?.playerDir}'");
                return;
            }

            // Detect harvestable resources in front of the player
            Vector2 origin = (Vector2)transform.position + facingDir * detectionOffset;
            var hit = Physics2D.OverlapCircle(origin, interactRadius, resourceLayer);

            if (hit == null)
            {
                Debug.Log($"[ToolUse] No resource found. Origin={origin}, Range={interactRadius}, " +
                          $"Layer={resourceLayer.value}, Dir={facingDir}");
                return;
            }

            var resource = hit.GetComponent<HarvestableResource>();
            if (resource == null || resource.IsDepleted)
            {
                Debug.Log($"[ToolUse] Hit '{hit.name}' but no HarvestableResource component (or depleted).");
                return;
            }

            // Check tool type matches
            if (resource.RequiredToolType != toolData.toolType)
            {
                Debug.Log($"[ToolUse] {toolData.toolType} can't harvest " +
                          $"this {resource.RequiredToolType} resource.");
                return;
            }

            // Swing — apply damage
            Vector2 hitDir = ((Vector2)resource.transform.position - (Vector2)transform.position).normalized;
            resource.TakeDamage(toolData.damage, hitDir);

            // Reduce durability
            if (equippedInstance.Data.hasInstanceState && toolData.durabilityCost > 0)
            {
                bool broke = equippedInstance.ReduceDurability(toolData.durabilityCost);

                if (broke)
                {
                    Debug.Log($"[ToolUse] {equippedInstance.Data.displayName} broke!");
                    // Remove the broken tool from inventory
                    _inventory.RemoveItem(equippedInstance.Data.id, 1);
                }
                else
                {
                    // Notify the specific slot so the durability bar updates
                    _inventory.NotifySlotChanged(_inventory.EquippedSlotIndex);
                    _inventory.NotifyChanged();
                }
            }

            // Start cooldown
            _cooldownTimer = toolData.cooldown;
        }

        /// <summary>
        /// Converts PlayerStateManager.playerDir string into a Vector2.
        /// </summary>
        private Vector2 GetFacingDirection()
        {
            if (playerStateManager == null) return Vector2.zero;

            return playerStateManager.playerDir switch
            {
                "Left" => Vector2.left,
                "Right" => Vector2.right,
                "Up" => Vector2.up,
                "Down" => Vector2.down,
                _ => Vector2.zero
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (playerStateManager == null) return;

            Vector2 dir = GetFacingDirection();
            if (dir == Vector2.zero) dir = Vector2.right;

            Vector2 origin = (Vector2)transform.position + dir * detectionOffset;

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(origin, interactRadius);

            // Show facing direction
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, (Vector3)origin);
        }
#endif
    }
}