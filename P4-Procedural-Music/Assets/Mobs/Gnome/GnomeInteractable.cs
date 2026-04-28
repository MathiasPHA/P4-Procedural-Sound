using UnityEngine;
using InteractionSystem;

public class GnomeInteractable : Interactable
{
    public string gnomeName = "";
    public TradeOffer[] tradeOffers;
    public TradeOffer MyOffer;

    private Animator anim;

    void Awake()
    {
        // TryGetComponent won't throw if Animator is missing
        TryGetComponent(out anim);
        GetNamed();
        GetOffer();
    }

    public override void Interact(PlayerStateManager player)
    {
        TradeUI.Instance.OpenTrade(this);
    }

    void GetNamed()
    {
        if (string.IsNullOrEmpty(gnomeName))
        {
            var nameIndex = Random.Range(0, 20);
            switch (nameIndex)
            {
                case 0:
                    gnomeName = "Glimmer";
                    break;
                case 1:
                    gnomeName = "Pip";
                    break;
                case 2:
                    gnomeName = "Nixie";
                    break;
                case 3:
                    gnomeName = "Thistle";
                    break;
                case 4:
                    gnomeName = "Bramble";
                    break;
                case 5:
                    gnomeName = "Fizzle";
                    break;
                case 6:
                    gnomeName = "Tinker";
                    break;
                case 7:
                    gnomeName = "Dabble";
                    break;
                case 8:
                    gnomeName = "Wizzle";
                    break;
                case 9:
                    gnomeName = "Sprocket";
                    break;
                case 10:
                    gnomeName = "Obama";
                    break;
                case 11:
                    gnomeName = "Puddle";
                    break;
                case 12:
                    gnomeName = "Snickerdoodle";
                    break;
                case 13:
                    gnomeName = "Stogger";
                    break;
                case 14:
                    gnomeName = "Bumble";
                    break;
                case 15:
                    gnomeName = "Doodle";
                    break;
                case 16:
                    gnomeName = "Whisker";
                    break;
                case 17:
                    gnomeName = "Sprout";
                    break;
                case 18:
                    gnomeName = "Big Milk";
                    break;
                case 19:
                    gnomeName = "Gearheart";
                    break;
            }
        }
    }

    void GetOffer()
    {
        if (tradeOffers != null && tradeOffers.Length > 0)
        {
            MyOffer = tradeOffers[Random.Range(0, tradeOffers.Length)];
        }
        else
        {
            Debug.LogWarning($"Gnome {gnomeName} has no trade offers assigned.");
        }
    }

    public override bool CanInteract() => true;

    public void StartTalking() => anim?.SetBool("isTalking", true);
    public void StopTalking()  => anim?.SetBool("isTalking", false);
}