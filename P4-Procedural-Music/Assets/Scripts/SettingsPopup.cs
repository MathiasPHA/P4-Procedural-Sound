using UnityEngine;

public class SettingsPopup : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsPopup;
    [SerializeField] private GameObject controlsUI;
    [SerializeField] private GameObject soundUI;

    public void OpenPopup()
    {
        settingsPopup.SetActive(true);
    }

    public void ClosePopup()
    {
        settingsPopup.SetActive(false);
    }

    public void OpenControls()
    {
        settingsPopup.SetActive(false);
        controlsUI.SetActive(true);
    }

    public void OpenSound()
    {
        settingsPopup.SetActive(false);
        soundUI.SetActive(true);
    }

    public void CloseControls()
    {
        controlsUI.SetActive(false);
        settingsPopup.SetActive(true);
    }

    public void CloseSound()
    {
        soundUI.SetActive(false);
        settingsPopup.SetActive(true);
    }
}