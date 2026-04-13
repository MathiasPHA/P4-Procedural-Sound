using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using InventorySystem.Data;
using InventorySystem.Harvesting;
using InteractionSystem;
using MobSystem;

namespace InventorySystem.Tools
{
    public class ToolUseSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStateManager playerStateManager;
        [SerializeField] private InteractionDetector interactionDetector;

         [Header("Wrong Tool Feedback")]
        [SerializeField] private ResponseOptions wrongToolFeedback;


        [Header("Tool Database")]
        [SerializeField] private List<ToolData> toolDatabase = new();

        [Header("Detection")]
        [SerializeField] private LayerMask resourceLayer;
        [SerializeField] private LayerMask mobLayer;
        [SerializeField] private float interactRadius = 1.5f;
        [SerializeField] private Vector2 detectionOffset = new Vector2(0.5f, 0f);

        [Header("Hand Harvesting")]
        [Min(1)][SerializeField] private int handDamage = 1;
        [Min(0.1f)][SerializeField] private float handCooldown = 0.8f;

        [Header("Mob Combat")]
        [Min(1)][SerializeField] private int handMobDamage = 1;
        [Min(0.1f)][SerializeField] private float handMobCooldown = 0.6f;

        private Dictionary<string, ToolData> _toolLookup = new();
        private Inventory _inventory;
        private float _cooldownTimer;

        private void Start()
        {
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

            if (interactionDetector == null)
            {
                interactionDetector = GetComponent<InteractionDetector>();
            }

             if (wrongToolFeedback == null)
                wrongToolFeedback = GetComponentInChildren<ResponseOptions>(true);
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        // ───────────── Input System Callbacks ─────────────

        private void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (_cooldownTimer > 0f) return;
            if (PauseManager.isPaused) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // ── Interaction priority: if hovering an interactable, walk to it ──
            if (interactionDetector != null && interactionDetector.CurrentTarget != null)
            {
                var target = interactionDetector.CurrentTarget;
                playerStateManager.moveToInteractState.SetTarget(target);
                playerStateManager.SwitchState(playerStateManager.moveToInteractState);
                return;
            }

            // ── Otherwise: existing tool/combat logic ──
            if (TryHitMob()) return;
            TryUseTool();
        }

        private void OnUseItem(InputValue value)
        {
            if (!value.isPressed) return;
            if (_cooldownTimer > 0f) return;
            if (PauseManager.isPaused) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            TryConsumeHotbarItem();
        }

        // ───────────── Consumable ─────────────

        private bool TryConsumeHotbarItem()
        {
            if (_inventory == null) return false;

            int hotbarIndex = _inventory.ActiveHotbarIndex;
            var slot = _inventory.Slots[hotbarIndex];

            if (slot.IsEmpty || slot.ItemData.category != ItemCategory.Consumable)
                return false;

            _inventory.UseSlot(hotbarIndex);
            return true;
        }

        // ───────────── Mob Combat ─────────────

        private bool TryHitMob()
        {
            if (mobLayer.value == 0) return false;

            Vector2 facingDir = GetFacingDirection();
            if (facingDir == Vector2.zero) return false;

            Vector2 origin = (Vector2)transform.position
                             + facingDir * detectionOffset.x
                             + Vector2.up * detectionOffset.y;

            var hit = Physics2D.OverlapCircle(origin, interactRadius, mobLayer);
            if (hit == null) return false;

            var mob = hit.GetComponent<MobController>();
            if (mob == null) return false;

            var equippedInstance = _inventory != null ? _inventory.EquippedItem : null;
            ToolData toolData = null;

            if (equippedInstance != null && equippedInstance.Data.category == ItemCategory.Tool)
                _toolLookup.TryGetValue(equippedInstance.Data.id, out toolData);

            if (toolData != null)
            {
                playerStateManager.animationQue = $"Harvest{toolData.toolType}";
                playerStateManager.StartHarvest();
                mob.TakeDamage(toolData.damage, transform.position);

                if (equippedInstance.Data.hasInstanceState && toolData.durabilityCost > 0)
                {
                    bool broke = equippedInstance.ReduceDurability(toolData.durabilityCost);
                    if (broke)
                    {
                        _inventory.RemoveItem(equippedInstance.Data.id, 1);
                    }
                    else
                    {
                        _inventory.NotifySlotChanged(_inventory.EquippedSlotIndex);
                        _inventory.NotifyChanged();
                    }
                }
                _cooldownTimer = toolData.cooldown;
            }
            else
            {
                playerStateManager.animationQue = "HarvestHand";
                playerStateManager.StartHarvest();
                mob.TakeDamage(handMobDamage, transform.position);
                _cooldownTimer = handMobCooldown;
            }

            return true;
        }

        // ───────────── Resource Harvesting ─────────────

        /// <summary>
        /// Public entry point for the interaction system. Hits a specific resource
        /// without needing OverlapCircle detection — the player is already in range.
        /// </summary>
        public void HarvestResource(HarvestableResource resource)
        {
            if (resource == null || resource.IsDepleted) return;
            if (_cooldownTimer > 0f) return;
            if (_inventory == null) return;

            Vector2 hitDir = ((Vector2)resource.transform.position - (Vector2)transform.position).normalized;

            if (resource.RequiredToolType == ToolType.None)
            {
                playerStateManager.animationQue = "HarvestHand";
                playerStateManager.StartHarvest();
                resource.TakeDamage(handDamage, hitDir);
                _cooldownTimer = handCooldown;
                return;
            }

            var equippedInstance = _inventory.EquippedItem;

            // No tool equipped at all
            if (equippedInstance == null || equippedInstance.Data.category != ItemCategory.Tool)
            {
                wrongToolFeedback?.TryShowWrongToolMessage("Hand", resource.RequiredToolType.ToString());
                return;
            }

            if (!_toolLookup.TryGetValue(equippedInstance.Data.id, out var toolData))
                return;

            // Wrong tool type equipped
            if (resource.RequiredToolType != toolData.toolType)
            {
                wrongToolFeedback?.TryShowWrongToolMessage(
                    toolData.toolType.ToString(),
                    resource.RequiredToolType.ToString()
                );
                return;
            }

            playerStateManager.animationQue = $"Harvest{toolData.toolType}";
            playerStateManager.StartHarvest();
            resource.TakeDamage(toolData.damage, hitDir);

            if (equippedInstance.Data.hasInstanceState && toolData.durabilityCost > 0)
            {
                bool broke = equippedInstance.ReduceDurability(toolData.durabilityCost);
                if (broke)
                    _inventory.RemoveItem(equippedInstance.Data.id, 1);
                else
                {
                    _inventory.NotifySlotChanged(_inventory.EquippedSlotIndex);
                    _inventory.NotifyChanged();
                }
            }

            _cooldownTimer = toolData.cooldown;
        }

        private void TryUseTool()
        {
            if (_inventory == null) return;

            Vector2 facingDir = GetFacingDirection();
            if (facingDir == Vector2.zero) return;

            Vector2 origin = (Vector2)transform.position + facingDir * detectionOffset.x + Vector2.up * detectionOffset.y;
            var hit = Physics2D.OverlapCircle(origin, interactRadius, resourceLayer);

            if (hit == null) return;

            var resource = hit.GetComponent<HarvestableResource>();
            if (resource == null || resource.IsDepleted) return;

            if (resource.RequiredToolType == ToolType.None)
            {
                playerStateManager.animationQue = "HarvestHand";
                playerStateManager.StartHarvest();
                Vector2 hitDir = ((Vector2)resource.transform.position - (Vector2)transform.position).normalized;
                resource.TakeDamage(handDamage, hitDir);
                _cooldownTimer = handCooldown;
                return;
            }

            var equippedInstance = _inventory.EquippedItem;
            if (equippedInstance == null || equippedInstance.Data.category != ItemCategory.Tool)
                return;

            if (!_toolLookup.TryGetValue(equippedInstance.Data.id, out var toolData))
                return;

            if (resource.RequiredToolType != toolData.toolType) return;

            playerStateManager.animationQue = $"Harvest{toolData.toolType}";
            playerStateManager.StartHarvest();

            Vector2 toolHitDir = ((Vector2)resource.transform.position - (Vector2)transform.position).normalized;
            resource.TakeDamage(toolData.damage, toolHitDir);

            if (equippedInstance.Data.hasInstanceState && toolData.durabilityCost > 0)
            {
                bool broke = equippedInstance.ReduceDurability(toolData.durabilityCost);
                if (broke)
                    _inventory.RemoveItem(equippedInstance.Data.id, 1);
                else
                {
                    _inventory.NotifySlotChanged(_inventory.EquippedSlotIndex);
                    _inventory.NotifyChanged();
                }
            }

            _cooldownTimer = toolData.cooldown;
        }

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
            Vector2 origin = (Vector2)transform.position + dir * detectionOffset.x + Vector2.up * detectionOffset.y;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(origin, interactRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, (Vector3)origin);
        }
#endif
    }
}