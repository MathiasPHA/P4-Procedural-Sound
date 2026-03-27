using UnityEngine;
using TMPro;

public class NewGame : MonoBehaviour
{
    [Tooltip("Input field for the world name.")]
    [SerializeField] private TMP_InputField worldNameInput;

    [Tooltip("Input field for the seed. Leave empty for a random seed.")]
    [SerializeField] private TMP_InputField seedInput;

    /// <summary>
    /// Hook this up to your Create Save button's OnClick event.
    /// </summary>
    public void CreateNewGame()
    {
        string worldName = worldNameInput.text.Trim();

        if (string.IsNullOrEmpty(worldName))
        {
            Debug.LogWarning("[NewGame] World name cannot be empty.");
            return;
        }

        if (GameSettings.Instance.SaveExists(worldName))
        {
            Debug.LogWarning($"[NewGame] A save called '{worldName}' already exists.");
            return;
        }

        // Parse seed — null if empty (GameSettings will generate a random one)
        int? seed = int.TryParse(seedInput.text.Trim(), out int parsed) ? parsed : null;

        GameSettings.Instance.CreateSave(worldName, seed);
    }
}