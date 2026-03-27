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

    private void OnEnable()
    {
        // Refresh the list every time this panel is opened
        PopulateSlots();
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

            // Set label — e.g. "MyWorld — last played 27/03/2026 14:32"
            var label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"{slot.worldName}\n<size=70%>Last played: {slot.lastPlayed:dd/MM/yyyy HH:mm}</size>";

            // Wire up the button to load this slot
            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                string worldName = slot.worldName; // capture for lambda
                button.onClick.AddListener(() => LoadSlot(worldName));
            }
        }
    }

    private void LoadSlot(string worldName)
    {
        bool success = GameSettings.Instance.LoadSave(worldName);
        if (!success)
            Debug.LogWarning($"[LoadGame] Could not load save '{worldName}'.");
    }
}
