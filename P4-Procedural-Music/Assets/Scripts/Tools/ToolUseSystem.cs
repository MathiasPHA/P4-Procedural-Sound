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

        [Header("Tool Database")]
        [SerializeField] private List<ToolData> toolDatabase = new();

        [Header("Detection")]
        [SerializeField] private LayerMask resourceLayer;
        [SerializeField] private LayerMask mobLayer;
        [Tooltip("Layer for placed structures (campfires, workbenches, etc.). " +
                 "Only tools with structureDamage > 0 can damage things on this layer.")]
        [SerializeField] private LayerMask structureLayer;
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
        private UnityEngine.Rendering.Universal.Light2D _playerLight2D;

        private float _drainTimer = 0f;
        private bool _drainingDurability = false;
        private ToolData _equippedToolData = null;

        [Header("Torch Light Fade")]
        [SerializeField] private float maxLightIntensity = 1f;   // intensity at full durability
        [SerializeField] private float minLightIntensity = 0.1f; // intensity at 0 durability
        [Header("Wrong Tool Feedback")]
        [SerializeField] private ResponseOptions wrongToolFeedback;

        [Header("Instrument Audio")]
        [SerializeField] private AudioSource instrumentAudioSource;

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

            // Grab the Light2D directly on this GameObject and make sure it starts off
            _playerLight2D = GetComponent<UnityEngine.Rendering.Universal.Light2D>();
            if (_playerLight2D == null)
                Debug.LogWarning("[ToolUseSystem] No Light2D found on Player!");
            else
                _playerLight2D.enabled = false;

            // Subscribe to equip/unequip event
            _inventory.OnEquippedChanged += OnEquippedChanged;

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

            if (instrumentAudioSource == null)
            {
                instrumentAudioSource = GetComponent<AudioSource>();
                if (instrumentAudioSource == null)
                    instrumentAudioSource = gameObject.AddComponent<AudioSource>();
            }

            instrumentAudioSource.playOnAwake = false;
            instrumentAudioSource.spatialBlend = 0f;

            if (wrongToolFeedback == null)
                wrongToolFeedback = GetComponentInChildren<ResponseOptions>(true);
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            // ── Passive durability drain ──
            if (_drainingDurability && _equippedToolData != null && _inventory != null)
            {
                _drainTimer -= Time.deltaTime;
                if (_drainTimer <= 0f)
                {
                    _drainTimer = _equippedToolData.drainInterval;

                    var equipped = _inventory.EquippedItem;
                    if (equipped != null && equipped.Data.hasInstanceState)
                    {
                        bool broke = equipped.ReduceDurability(_equippedToolData.drainAmount);
                        if (broke)
                        {
                            Debug.Log($"[ToolUseSystem] '{equipped.Data.id}' broke from passive drain — removing.");
                            _drainingDurability = false;
                            _equippedToolData = null;
                            if (_playerLight2D != null) _playerLight2D.enabled = false;
                            _inventory.RemoveItem(equipped.Data.id, 1);
                        }
                        else
                        {
                            // Scale light intensity with remaining durability
                            if (_playerLight2D != null)
                            {
                                float t = equipped.DurabilityNormalized;
                                _playerLight2D.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, t);
                            }

                            _inventory.NotifySlotChanged(_inventory.EquippedSlotIndex);
                            _inventory.NotifyChanged();
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnEquippedChanged -= OnEquippedChanged;
        }

        private void OnEquippedChanged(int equippedSlotIndex)
        {
            var item = _inventory.EquippedItem;
            Debug.Log($"[ToolUseSystem] OnEquippedChanged fired — slot={equippedSlotIndex}, item={item?.Data?.id ?? "none"}");

            bool holdingTorch = (item != null && item.Data.id == "Torch");

            // Toggle light
            if (_playerLight2D != null)
            {
                _playerLight2D.enabled = holdingTorch;
                if (holdingTorch)
                {
                    // Restore intensity from the torch's actual current durability
                    float t = item.DurabilityNormalized;
                    _playerLight2D.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, t);
                }
            }

            // Look up ToolData for the newly equipped item
            _equippedToolData = null;
            if (item != null && _toolLookup.TryGetValue(item.Data.id, out var toolData))
                _equippedToolData = toolData;

            if (item != null)
                Debug.Log($"[ToolUseSystem] ToolData lookup for '{item.Data.id}': {(_equippedToolData != null ? "FOUND" : "NOT FOUND — check Tool Database list in Inspector")}");

            // Start passive drain only if the ToolData says so
            if (_equippedToolData != null && _equippedToolData.drainsOverTime)
            {
                _drainingDurability = true;
                _drainTimer = _equippedToolData.drainInterval;
                Debug.Log($"[ToolUseSystem] Drain started — interval={_equippedToolData.drainInterval}s, amount={_equippedToolData.drainAmount}");
            }
            else
            {
                _drainingDurability = false;
                _drainTimer = 0f;
                if (_equippedToolData != null)
                    Debug.Log($"[ToolUseSystem] ToolData found but drainsOverTime=false — no passive drain.");
            }
        }

        // ───────────── Input System Callbacks ─────────────

        private void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (_cooldownTimer > 0f) return;
            if (PauseManager.isPaused) return;

            // Block all click interactions during structure placement —
            // the click belongs to PlacementSystem for placing the ghost.
            if (InventorySystem.Building.PlacementSystem.Instance != null &&
                InventorySystem.Building.PlacementSystem.Instance.IsPlacing) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // ── Structure damage takes priority over interaction priority ──
            // If the equipped tool has structureDamage > 0 (e.g. Hammer) AND a
            // structure is in attack range, damage it directly. This bypasses
            // the InteractionDetector check below so the player can demolish
            // a campfire even though CampfireInteractable would normally
            // intercept the click and route to fueling.
            if (TryHitStructure()) return;

            // ── Interaction priority: if hovering an interactable, walk to it ──
            if (interactionDetector != null && interactionDetector.CurrentTarget != null)
            {
                var target = interactionDetector.CurrentTarget;
                playerStateManager.moveToInteractState.SetTarget(target);
                playerStateManager.SwitchState(playerStateManager.moveToInteractState);
                return;
            }

            if (TryPlayInstrument()) return;

            // ── Otherwise: existing tool/combat logic ──
            if (TryHitMob()) return;
            TryUseTool();
        }

        private void OnUseItem(InputValue value)
        {
            if (!value.isPressed) return;
            if (_cooldownTimer > 0f) return;
            if (PauseManager.isPaused) return;

            // Block item use during placement — right-click is used by
            // PlacementSystem to cancel the ghost.
            if (InventorySystem.Building.PlacementSystem.Instance != null &&
                InventorySystem.Building.PlacementSystem.Instance.IsPlacing) return;

            // ── Campfire fueling: check BEFORE UI block so hotbar UI doesn't intercept ──
            // Use GetComponent because InteractionDetector may return HarvestInteractable first
            // when multiple Interactable components exist on the same GameObject.
            if (interactionDetector != null && interactionDetector.CurrentTarget != null)
            {
                var campfire = interactionDetector.CurrentTarget.GetComponent<CampfireInteractable>();
                if (campfire != null)
                {
                    playerStateManager.moveToInteractState.SetTarget(campfire);
                    playerStateManager.SwitchState(playerStateManager.moveToInteractState);
                    return;
                }
            }

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

            if (toolData != null && toolData.isInstrument)
                return false;

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
        /// Public entry point used by MobInteractable → MoveToInteractState.
        /// The player has already walked into range; just apply damage to the
        /// specific mob passed in. Mirrors HarvestResource's pattern.
        /// </summary>
        public void AttackMob(MobController mob)
        {
            if (mob == null || mob.CurrentHealth <= 0) return;
            if (_cooldownTimer > 0f) return;
            if (_inventory == null) return;

            var equippedInstance = _inventory.EquippedItem;
            ToolData toolData = null;

            if (equippedInstance != null && equippedInstance.Data.category == ItemCategory.Tool)
                _toolLookup.TryGetValue(equippedInstance.Data.id, out toolData);

            // Can't attack with an instrument equipped
            if (toolData != null && toolData.isInstrument) return;

            // ── Net: attempt catch instead of dealing damage ──
            if (toolData != null && toolData.toolType == ToolType.Net)
            {
                TryCatchMob(mob, toolData, equippedInstance);
                return;
            }

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
        }

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
            if (equippedInstance == null || equippedInstance.Data.category != ItemCategory.Tool)
            {
                wrongToolFeedback?.TryShowWrongToolMessage("Hand", resource.RequiredToolType.ToString());
                playerStateManager.SwitchState(playerStateManager.playerShrugState);
                return;

            }

            if (!_toolLookup.TryGetValue(equippedInstance.Data.id, out var toolData))
                return;

            if (toolData.isInstrument)
            {
                TryPlayInstrument(toolData);
                return;
            }

            // Wrong tool type equipped
            if (resource.RequiredToolType != toolData.toolType)
            {
                wrongToolFeedback?.TryShowWrongToolMessage(
                    toolData.toolType.ToString(),
                    resource.RequiredToolType.ToString()
                );
                playerStateManager.SwitchState(playerStateManager.playerShrugState);
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

        // ───────────── Bug Catching ─────────────

            private bool TryCatchMob(MobController mob, ToolData toolData, ItemInstance equippedInstance)        {
            var catchable = mob.GetComponent<MobSystem.CatchableMob>();

            if (catchable == null)
            {
                // Mob isn't catchable — shrug so the player gets clear feedback.
                playerStateManager.SwitchState(playerStateManager.playerShrugState);
                _cooldownTimer = toolData.cooldown;
                return true;
            }

            playerStateManager.animationQue = "HarvestNet";
            playerStateManager.StartHarvest();

            catchable.AttemptCatch();

            // Durability cost — same as every other tool swing.
            if (equippedInstance != null
                && equippedInstance.Data.hasInstanceState
                && toolData.durabilityCost > 0)
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
            return true;
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

            if (toolData.isInstrument)
            {
                TryPlayInstrument(toolData);
                return;
            }

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

        // ───────────── Structure Damage (Hammer) ─────────────

        /// <summary>
        /// If the equipped tool has structureDamage > 0 and a PlacedStructure
        /// is in attack range, deal that damage and consume the click.
        /// Returns true if a structure was hit (caller should not run further
        /// click logic). Returns false otherwise — including when the equipped
        /// tool can't damage structures, so the normal interact/mob/resource
        /// flow proceeds unchanged.
        /// </summary>
        private bool TryHitStructure()
        {
            if (structureLayer.value == 0) return false;
            if (_inventory == null) return false;

            // Bare hands and non-tool items can't damage structures.
            var equippedInstance = _inventory.EquippedItem;
            if (equippedInstance == null || equippedInstance.Data.category != ItemCategory.Tool)
                return false;

            if (!_toolLookup.TryGetValue(equippedInstance.Data.id, out var toolData))
                return false;

            // Instruments don't damage anything.
            if (toolData.isInstrument) return false;

            // Only tools that explicitly opt in can damage structures.
            if (toolData.structureDamage <= 0) return false;

            Vector2 facingDir = GetFacingDirection();
            if (facingDir == Vector2.zero) return false;

            Vector2 origin = (Vector2)transform.position
                             + facingDir * detectionOffset.x
                             + Vector2.up * detectionOffset.y;

            var hit = Physics2D.OverlapCircle(origin, interactRadius, structureLayer);
            if (hit == null) return false;

            // GetComponentInParent handles cases where the collider is on a
            // child sprite/collider GameObject rather than the root.
            var structure = hit.GetComponentInParent<InventorySystem.Building.PlacedStructure>();
            if (structure == null || structure.IsDestroyed) return false;

            Vector2 hitDir = ((Vector2)structure.transform.position - (Vector2)transform.position).normalized;

            playerStateManager.animationQue = $"Harvest{toolData.toolType}";
            playerStateManager.StartHarvest();
            structure.TakeDamage(toolData.structureDamage, hitDir);

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
            return true;
        }

        // ───────────── Instrument ─────────────

        private bool TryPlayInstrument()
        {
            if (_equippedToolData == null || !_equippedToolData.isInstrument)
                return false;

            return TryPlayInstrument(_equippedToolData);
        }

        private bool TryPlayInstrument(ToolData toolData)
        {
            if (toolData == null || !toolData.isInstrument)
                return false;

            if (instrumentAudioSource == null)
                return false;

            if (toolData.instrumentSounds == null || toolData.instrumentSounds.Count == 0)
            {
                Debug.LogWarning($"[ToolUseSystem] '{toolData.item?.id ?? "Unknown"}' is marked as an instrument but has no instrument sounds assigned.");
                _cooldownTimer = toolData.cooldown;
                return true;
            }

            AudioClip clip = toolData.instrumentSounds[Random.Range(0, toolData.instrumentSounds.Count)];
            if (clip != null)
            {
                instrumentAudioSource.pitch = 1f + Random.Range(-toolData.instrumentPitchVariation, toolData.instrumentPitchVariation);
                instrumentAudioSource.PlayOneShot(clip, toolData.instrumentVolume);
            }

            _cooldownTimer = toolData.cooldown;
            return true;
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