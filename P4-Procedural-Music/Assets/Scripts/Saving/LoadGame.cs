using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LoadGame : MonoBehaviour
{
    [Tooltip("Parent transform where save slot buttons will be spawned.")]
    [SerializeField] private Transform slotContainer;

    [Tooltip("Prefab for each save slot button. Needs a TMP_Text child for the label.")]
    [SerializeField] private GameObject slotButtonPrefab;

    [Tooltip("Shown when there are no saves to display.")]
    [SerializeField] private GameObject noSavesMessage;

    [Header("Delete")]
    [Tooltip("Prefab for the delete button spawned next to each slot. " +
             "Needs a Button component. If left empty, no delete buttons are shown.")]
    [SerializeField] private GameObject deleteButtonPrefab;

    [Tooltip("The confirmation popup panel. Should contain a text field, a Yes button, and a No/Cancel button.")]
    [SerializeField] private GameObject confirmPopup;

    [Tooltip("Text on the confirmation popup (e.g. 'Are you sure you want to delete MyWorld?').")]
    [SerializeField] private TMP_Text confirmText;

    [Tooltip("The Yes button on the confirmation popup.")]
    [SerializeField] private Button confirmYesButton;

    [Tooltip("The No/Cancel button on the confirmation popup.")]
    [SerializeField] private Button confirmNoButton;

    // The world name queued for deletion
    private string _worldToDelete;

    private void OnEnable()
    {
        PopulateSlots();
        HideConfirmPopup();
    }

    private void Start()
    {
        // Wire up the popup buttons once — they never change
        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(HideConfirmPopup);
    }

    private void PopulateSlots()
    {
        // Clear old buttons
        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);

        List<GameSettings.SaveSlotInfo> slots = GameSettings.Instance.GetSaveSlots();

        if (noSavesMessage != null)
            noSavesMessage.SetActive(slots.Count == 0);

        foreach (var slot in slots)
        {
            var buttonObj = Instantiate(slotButtonPrefab, slotContainer);

            // Set label
            var label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"{slot.worldName}\n<size=70%>Last played: {slot.lastPlayed:dd/MM/yyyy HH:mm}</size>";

            // Wire up the button to load this slot
            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                string worldName = slot.worldName;
                button.onClick.AddListener(() => LoadSlot(worldName));
            }

            // Add delete button if prefab is assigned
            if (deleteButtonPrefab != null)
            {
                var deleteObj = Instantiate(deleteButtonPrefab, buttonObj.transform);

                var deleteButton = deleteObj.GetComponent<Button>();
                if (deleteButton != null)
                {
                    string worldName = slot.worldName;
                    deleteButton.onClick.AddListener(() => ShowConfirmPopup(worldName));
                }
            }
        }
    }

    private void LoadSlot(string worldName)
    {
        bool success = GameSettings.Instance.LoadSave(worldName);
        if (!success)
            Debug.LogWarning($"[LoadGame] Could not load save '{worldName}'.");
    }

    // =====================================================================
    // Confirmation popup
    // =====================================================================

    private void ShowConfirmPopup(string worldName)
    {
        _worldToDelete = worldName;

        if (confirmText != null)
            confirmText.text = $"Are you sure you want to delete \"{worldName}\"?";

        if (confirmPopup != null)
            confirmPopup.SetActive(true);
    }

    private void HideConfirmPopup()
    {
        _worldToDelete = null;

        if (confirmPopup != null)
            confirmPopup.SetActive(false);
    }

    private void OnConfirmYes()
    {
        if (!string.IsNullOrEmpty(_worldToDelete))
        {
            GameSettings.Instance.DeleteSave(_worldToDelete);
            Debug.Log($"[LoadGame] Deleted save '{_worldToDelete}'.");
        }

        HideConfirmPopup();
        PopulateSlots();
    }
}