using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.Data;

public class TradeSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image costIcon;
    public Image rewardIcon;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI rewardText;
    public Button tradeButton;
    public GameObject lockedOverlay;

    private TradeOffer offer;

    public void Setup(TradeOffer o)
    {
        offer = o;

        costIcon.sprite   = o.CostIcon;
        rewardIcon.sprite = o.RewardIcon;
        costText.text     = $"{o.costAmount}x {o.CostName}";
        rewardText.text   = $"{o.rewardAmount}x {o.RewardName}";

        if (lockedOverlay != null)
            lockedOverlay.SetActive(o.isLocked);

        tradeButton.onClick.RemoveAllListeners();
        tradeButton.onClick.AddListener(OnTrade);

        RefreshAffordability();

        // Live affordability — updates as inventory changes
        Inventory.Instance.OnInventoryChanged += RefreshAffordability;
    }

    void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= RefreshAffordability;
    }

    void RefreshAffordability()
    {
        bool canAfford = Inventory.Instance.HasItem(
            offer.CostId, offer.costAmount);
        bool available = canAfford && !offer.isLocked;

        tradeButton.interactable = available;

        // Red tint when the player can't afford it
        costText.color = canAfford
            ? Color.white
            : new Color(1f, 0.4f, 0.4f);
    }

    void OnTrade()
    {
        if (!Inventory.Instance.HasItem(offer.CostId, offer.costAmount))
        {
            Debug.Log($"[GnomeTrade] Not enough {offer.CostName}");
            return;
        }

        bool removed = Inventory.Instance.RemoveItem(
            offer.CostId, offer.costAmount);

        if (!removed)
        {
            Debug.LogWarning("[GnomeTrade] RemoveItem failed unexpectedly.");
            return;
        }

        int overflow = Inventory.Instance.AddItem(
            offer.rewardItem, offer.rewardAmount);

        if (overflow > 0)
            Debug.LogWarning(
                $"[GnomeTrade] Inventory full — {overflow}x " +
                $"{offer.RewardName} lost!");
        else
            Debug.Log(
                $"[GnomeTrade] Traded {offer.costAmount}x {offer.CostName}" +
                $" for {offer.rewardAmount}x {offer.RewardName}");
    }
}