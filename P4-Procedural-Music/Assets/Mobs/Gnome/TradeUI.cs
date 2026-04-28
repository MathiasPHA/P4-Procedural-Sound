using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.Input;

public class TradeUI : MonoBehaviour
{
    public static TradeUI Instance;

    [Header("UI References")]
    public GameObject tradePanel;
    public Transform offerContainer;
    public GameObject tradeSlotPrefab;

    [Header("Input")]
    public InventoryInputProvider inputProvider;

    private GnomeInteractable currentGnome;

    void Awake()
    {
        if (Instance != null && Instance != this)
        { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => tradePanel.SetActive(false);

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && tradePanel.activeSelf)
            CloseTrade();
    }

    public void OpenTrade(GnomeInteractable gnome)
    {
        currentGnome = gnome;
        PopulateOffer(gnome.MyOffer);
        tradePanel.SetActive(true);
        gnome.StartTalking();

        inputProvider?.DisableMovement();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseTrade()
    {
        tradePanel.SetActive(false);
        currentGnome?.StopTalking();
        currentGnome = null;

        inputProvider?.EnableMovement();
    }

    void PopulateOffer(TradeOffer offer)
    {
        foreach (Transform child in offerContainer)
            Destroy(child.gameObject);
            var slot = Instantiate(tradeSlotPrefab, offerContainer);
            slot.GetComponent<TradeSlot>().Setup(offer);
        
    }
}