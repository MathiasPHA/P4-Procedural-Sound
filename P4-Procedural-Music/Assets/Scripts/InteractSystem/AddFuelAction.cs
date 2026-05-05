using UnityEngine;
using InteractionSystem;
using InventorySystem;
using InventorySystem.Data;

/// <summary>
/// On-interact action: checks the player's active hotbar slot for a fuel item
/// (ItemData.isFuel == true), consumes one unit, and feeds it to the campfire.
/// Pair with a WorldStationInteractable on the same GameObject.
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

        // Only read the active hotbar slot (the held item)
        InventorySlot activeSlot = inventory.Slots[inventory.ActiveHotbarIndex];
        if (activeSlot.IsEmpty) return;

        ItemData heldItem = activeSlot.ItemData;
        if (!heldItem.isFuel) return;

        fuelTarget.AddFuel(heldItem, 1);
        activeSlot.RemoveFromStack(1);
        inventory.NotifySlotChanged(inventory.ActiveHotbarIndex);
        inventory.NotifyChanged();

        if (fuelSound != null)
            AudioSource.PlayClipAtPoint(fuelSound, transform.position, fuelVolume);
    }
}