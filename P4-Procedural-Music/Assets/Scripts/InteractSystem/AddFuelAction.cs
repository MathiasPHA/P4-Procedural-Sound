using UnityEngine;
using InteractionSystem;
using InventorySystem;
using InventorySystem.Data;

/// <summary>
/// On-interact action: if the player's equipped item is fuel
/// (ItemData.isFuel == true), consume one unit and feed it to this campfire.
/// Pluggable replacement for CampfireInteractable — pair with a
/// <see cref="WorldStationInteractable"/>.
///
/// SETUP:
///   1. Attach to the same GameObject as a CampfireComfortSource
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
    [Tooltip("The campfire comfort source that receives fuel. Auto-found on this GameObject.")]
    [SerializeField] private CampfireComfortSource fuelTarget;

    [Header("Feedback")]
    [Tooltip("Optional sound played when fuel is successfully added.")]
    [SerializeField] private AudioClip fuelSound;
    [Range(0f, 1f)]
    [SerializeField] private float fuelVolume = 0.6f;

    private void Awake()
    {
        if (fuelTarget == null)
            fuelTarget = GetComponent<CampfireComfortSource>();
        // Intentionally no error log — see class-doc note about ghost previews.
    }

    public void Execute(PlayerStateManager player)
    {
        if (fuelTarget == null)
        {
            Debug.LogWarning($"[AddFuelAction] No CampfireComfortSource on {name} — can't fuel.");
            return;
        }

        var inventory = InventoryBootstrap.PlayerInventory;
        if (inventory == null) return;

        var equipped = inventory.EquippedItem;
        if (equipped == null)
        {
            Debug.Log("[AddFuelAction] Nothing equipped — can't fuel campfire.");
            return;
        }

        var data = equipped.Data;
        if (!data.isFuel || data.burnFuelValue <= 0f)
        {
            Debug.Log($"[AddFuelAction] '{data.displayName}' isn't fuel.");
            return;
        }

        inventory.RemoveItem(data.id, 1);
        fuelTarget.AddFuel(data.burnFuelValue);

        if (fuelSound != null)
            AudioSource.PlayClipAtPoint(fuelSound, transform.position, fuelVolume);

        Debug.Log($"[AddFuelAction] Fed {data.displayName} (+{data.burnFuelValue:F0}s) to campfire.");
    }
}