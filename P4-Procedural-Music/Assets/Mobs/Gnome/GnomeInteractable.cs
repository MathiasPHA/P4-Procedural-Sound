using UnityEngine;
using InteractionSystem;

public class GnomeInteractable : Interactable
{
    public string gnomeName = "";
    public TradeOffer[] tradeOffers;

    private Animator anim;

    void Awake()
    {
        // TryGetComponent won't throw if Animator is missing
        TryGetComponent(out anim);
    }

    public override void Interact(PlayerStateManager player)
    {
        TradeUI.Instance.OpenTrade(this);
    }

    public override bool CanInteract() => true;

    public void StartTalking() => anim?.SetBool("isTalking", true);
    public void StopTalking()  => anim?.SetBool("isTalking", false);
}