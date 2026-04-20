using UnityEngine;
using InventorySystem.Data;

[CreateAssetMenu(menuName = "Gnome/Trade Offer")]
public class TradeOffer : ScriptableObject
{
    [Header("Cost — what the player gives")]
    public ItemData costItem;
    public int costAmount = 1;

    [Header("Reward — what the player receives")]
    public ItemData rewardItem;
    public int rewardAmount = 1;

    [Header("Availability")]
    public bool isLocked = false;
    public string lockHint = "";

    // Convenience helpers
    public string CostId => costItem != null ? costItem.id : "";
    public string CostName => costItem != null ? costItem.name : "Unknown";
    public Sprite CostIcon => costItem != null ? costItem.icon : null;

    public string RewardId => rewardItem != null ? rewardItem.id : "";
    public string RewardName => rewardItem != null ? rewardItem.name : "Unknown";
    public Sprite RewardIcon => rewardItem != null ? rewardItem.icon : null;
}