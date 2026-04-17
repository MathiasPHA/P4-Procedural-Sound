using UnityEngine;

[CreateAssetMenu(menuName = "Gnome/Trade Offer")]
public class TradeOffer : ScriptableObject
{
    [Header("Cost")]
    public string costItemName;
    public Sprite costIcon;
    public int costAmount;

    [Header("Reward")]
    public string rewardItemName;
    public Sprite rewardIcon;
    public int rewardAmount;

    [Header("Meta")]
    public bool isLocked = false;
    public string lockHint = "";
}