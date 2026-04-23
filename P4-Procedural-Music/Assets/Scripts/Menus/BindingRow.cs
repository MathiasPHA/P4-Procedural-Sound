using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A single row in the controls list: action name on the left, key button on the right.
/// Attach this to your BindingRow prefab.
/// </summary>
public class BindingRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI actionLabel;
    [SerializeField] private TextMeshProUGUI keyLabel;
    [SerializeField] private Button rebindButton;

    public void Setup(string action, string key, System.Action onRebind)
    {
        actionLabel.text = action;
        keyLabel.text    = string.IsNullOrEmpty(key) ? "—" : key;
        rebindButton.onClick.RemoveAllListeners();
        rebindButton.onClick.AddListener(() => onRebind());
    }
}
