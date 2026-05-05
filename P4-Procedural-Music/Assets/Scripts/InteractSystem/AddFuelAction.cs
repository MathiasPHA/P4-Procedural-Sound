using UnityEngine;
using InteractionSystem;
using InventorySystem;
using InventorySystem.Data;

/// <summary>
/// On-interact action: scans the player's inventory for a fuel item
/// (ItemData.isFuel == true), consumes one unit, and feeds it to this
/// campfire. Pluggable replacement for CampfireInteractable — pair with
/// a <see cref="WorldStationInteractable"/>.
///
/// Targets <see cref="CampfireController"/> as the canonical fuel sink.
/// CampfireController owns the burn-state lifecycle (light flicker, audio,
/// burn-rate transitions) and persists fuel across save/load via
/// IPersistentStructureState — so this is the right component to feed.
///
/// AUTO-PICK BEHAVIOR:
///   The player doesn't need to select fuel on the hotbar. Clicking the
///   campfire scans the entire inventory and picks the lowest-burn-value
///   fuel available — so wood is consumed before coal, sticks before logs,
///   and so on. This matches how most survival games handle furnaces and
///   prevents the player from accidentally wasting high-value fuel.
///
///   If multiple stacks of the same item exist, the first stack found is
///   the one we decrement from (RemoveItem handles this internally).
///
/// SETUP:
///   1. Attach to the same GameObject as a CampfireController
///   2. Also attach a WorldStationInteractable (the router)
///   3. Fuel target auto-found via GetComponent
///
/// Note: Awake intentionally does NOT error when the fuel target is missing.
/// The placement ghost preview instantiates this prefab too, and ghosts
/// don't have functional components by design. Validation is deferred to
/// Execute, which only runs on a real placed structure.
/// </summary>
public class AddFuelAction : MonoBehaviour, IWorldInteractAction
{
    [Header("Target")]
    [Tooltip("The campfire controller that receives fuel. Auto-found on this GameObject.")]
    [SerializeField] private CampfireController fuelTarget;

    [Header("Feedback")]
    [Tooltip("Optional sound played when fuel is successfully added.")]
    [SerializeField] private AudioClip fuelSound;
    [Range(0f, 1f)]
    [SerializeField] private float fuelVolume = 0.6f;

    private void Awake()
    {
        if (fuelTarget == null)
            fuelTarget = GetComponent<CampfireController>();
        // Intentionally no error log — see class-doc note about ghost previews.
    }

    public void Execute(PlayerStateManager player)
    {
        if (fuelTarget == null)
        {
            Debug.LogWarning($"[AddFuelAction] No CampfireController on {name} — can't fuel.");
            return;
        }

        var inventory = InventoryBootstrap.PlayerInventory;
        if (inventory == null) return;

        // Scan the entire inventory for the lowest-burn-value fuel item.
        // Lowest-first means players don't accidentally burn coal when wood
        // is available — the campfire eats the cheap stuff first.
        ItemData chosen = FindLowestValueFuel(inventory);

        if (chosen == null)
        {
            Debug.Log("[AddFuelAction] No fuel in inventory — can't fuel campfire.");
            return;
        }

        fuelTarget.AddFuel(chosen, 1);
        inventory.RemoveItem(chosen.id, 1);

        if (fuelSound != null)
            AudioSource.PlayClipAtPoint(fuelSound, transform.position, fuelVolume);
    }

    /// <summary>
    /// Walks all inventory slots and returns the ItemData of the fuel item
    /// with the smallest positive burnFuelValue. Returns null if no fuel
    /// is present. Stacks of the same item only count once — the comparison
    /// is per-ItemData, not per-slot.
    /// </summary>
    private static ItemData FindLowestValueFuel(Inventory inventory)
    {
        ItemData best = null;
        var slots = inventory.Slots;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty) continue;

            var data = slot.ItemData;
            if (data == null) continue;
            if (!data.isFuel) continue;
            if (data.burnFuelValue <= 0f) continue;

            if (best == null || data.burnFuelValue < best.burnFuelValue)
                best = data;
        }

        return best;
    }
}