using UnityEngine;
using MobSystem.Data;

namespace MobSystem
{
    /// <summary>
    /// Added to any mob whose MobData.isCatchable == true.
    ///
    /// SPAWN INTEGRATION:
    ///   Call CatchableMob.TryAddToInstance(go, mobData) wherever you Instantiate mob prefabs.
    ///   Safe to call on every mob — no-ops if isCatchable is false.
    ///
    /// On a successful net swing the mob is despawned and its catchResultItem
    /// is added to the player's inventory. If catchResultItem is null the mob
    /// is still despawned — it just leaves nothing behind.
    /// </summary>
    public class CatchableMob : MonoBehaviour
    {
        [HideInInspector] public MobData data;

        // Brief cooldown so rapid clicks don't double-fire before Destroy propagates.
        private const float SwingCooldown = 0.5f;
        private float _nextAttemptTime;

        // ── Static factory ──────────────────────────────────────────────────

        /// <summary>
        /// Attach CatchableMob to a freshly spawned mob. No-op if isCatchable is false.
        /// </summary>
        public static void TryAddToInstance(GameObject mobInstance, MobData mobData)
        {
            if (mobData == null || !mobData.isCatchable) return;
            var c = mobInstance.AddComponent<CatchableMob>();
            c.data = mobData;
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Called by ToolUseSystem when the player swings the Net at this mob.
        /// Always succeeds — gives catchResultItem to inventory and despawns the mob.
        /// </summary>
        public void AttemptCatch()
        {
            if (Time.time < _nextAttemptTime) return;
            _nextAttemptTime = Time.time + SwingCooldown;

            if (data.catchResultItem != null)
            {
                var inventory = InventorySystem.InventoryBootstrap.PlayerInventory;
                if (inventory != null)
                    inventory.AddItem(data.catchResultItem, 1);
                else
                    Debug.LogWarning("[CatchableMob] PlayerInventory is null — item not given.");
            }

            // Notify the spawn manager so _activeMobs and _typeCounts stay accurate.
            MobSpawnManager.Instance?.UntrackMob(GetComponent<MobController>());

            Debug.Log($"[CatchableMob] Caught {data.displayName}!");
            Destroy(gameObject);
        }
    }
}   