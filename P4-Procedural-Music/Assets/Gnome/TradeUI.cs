using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TradeUI : MonoBehaviour
{
    public static TradeUI Instance;
    public GameObject tradePanel;
    public TextMeshProUGUI titleText;
    public Transform offerContainer;   // parent for slots
    public GameObject tradeSlotPrefab;

    private GnomeInteractable currentGnome;

    void Awake() => Instance = this;
    void Start()  => tradePanel.SetActive(false);

    public void OpenTrade(GnomeInteractable gnome)
    {
        currentGnome = gnome;
        titleText.text = gnome.gnomeName;
        PopulateOffers(gnome.tradeOffers);
        tradePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None; // show cursor
    }

    public void CloseTrade()
    {
        tradePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
    }

    void PopulateOffers(TradeOffer[] offers)
    {
        // clear old slots
        foreach (Transform child in offerContainer)
            Destroy(child.gameObject);

        foreach (var offer in offers)
        {
            var slot = Instantiate(tradeSlotPrefab, offerContainer);
            slot.GetComponent<TradeSlot>().Setup(offer, this);
        }
    }
}