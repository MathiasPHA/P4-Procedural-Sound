using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TradeSlot : MonoBehaviour
{
    public Image costIcon, rewardIcon;
    public TextMeshProUGUI costText, rewardText;
    public Button tradeButton;
    public GameObject lockedOverlay;

    private TradeOffer offer;
    private TradeUI ui;

    public void Setup(TradeOffer o, TradeUI tradeUI)
    {
        offer = o; ui = tradeUI;
        costIcon.sprite   = o.costIcon;
        rewardIcon.sprite = o.rewardIcon;
        costText.text     = $"{o.costAmount}x {o.costItemName}";
        rewardText.text   = $"{o.rewardAmount}x {o.rewardItemName}";

        bool locked = o.isLocked;
        tradeButton.interactable = !locked;
        lockedOverlay.SetActive(locked);

        tradeButton.onClick.AddListener(OnTrade);
    }

    void OnTrade()
    {
        bool success = InventorySystem.Data
            .TryTrade(offer.costItemName, offer.costAmount,
                      offer.rewardItemName, offer.rewardAmount);

        if (!success)
            Debug.Log("Not enough " + offer.costItemName);
    }
}