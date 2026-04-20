using UnityEngine;
using InteractionSystem;

public class GnomeInteractable : Interactable
{
    public string gnomeName = "";
    public TradeOffer[] tradeOffers;

    private Animator anim;

    void Awake() => anim = GetComponent<Animator>();

    public override void Interact(PlayerStateManager player)
    {
        TradeUI.Instance.OpenTrade(this);
    }

    public override bool CanInteract() => true;

    public void StartTalking() => anim?.SetBool("isTalking", true);
    public void StopTalking()  => anim?.SetBool("isTalking", false);
}