using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.Data;
using InventorySystem;

public class TradeSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image costIcon;
    public Image rewardIcon;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI rewardText;
    public Button tradeButton;

    private TradeOffer offer;

    public void Setup(TradeOffer o)
    {
        offer = o;

        costIcon.sprite   = o.CostIcon;
        rewardIcon.sprite = o.RewardIcon;
        costText.text     = $"{o.costAmount}x {o.CostName}";
        rewardText.text   = $"{o.rewardAmount}x {o.RewardName}";

        tradeButton.onClick.RemoveAllListeners();
        tradeButton.onClick.AddListener(OnTrade);

        RefreshAffordability();

        InventoryBootstrap.PlayerInventory.OnInventoryChanged += RefreshAffordability;
    }

    void OnDestroy()
    {
        if (InventoryBootstrap.PlayerInventory != null)
            InventoryBootstrap.PlayerInventory.OnInventoryChanged -= RefreshAffordability;
    }

    void RefreshAffordability()
    {
        bool canAfford = InventoryBootstrap.PlayerInventory.HasItem(
            offer.CostId, offer.costAmount);
        bool available = canAfford && !offer.isLocked;

        tradeButton.interactable = available;

        costText.color = canAfford
            ? Color.white
            : new Color(1f, 0.4f, 0.4f);
    }

    void OnTrade()
    {
        if (!InventoryBootstrap.PlayerInventory.HasItem(offer.CostId, offer.costAmount))
        {
            Debug.Log($"[GnomeTrade] Not enough {offer.CostName}");
            return;
        }

        bool removed = InventoryBootstrap.PlayerInventory.RemoveItem(
            offer.CostId, offer.costAmount);

        if (!removed)
        {
            Debug.LogWarning("[GnomeTrade] RemoveItem failed unexpectedly.");
            return;
        }

        int overflow = InventoryBootstrap.PlayerInventory.AddItem(
            offer.rewardItem, offer.rewardAmount);

        if (overflow > 0)
            Debug.LogWarning(
                $"[GnomeTrade] Inventory full — {overflow}x {offer.RewardName} lost!");
        else
            Debug.Log(
                $"[GnomeTrade] Traded {offer.costAmount}x {offer.CostName}" +
                $" for {offer.rewardAmount}x {offer.RewardName}");
    }
}