using ProceduralMusic.Bridge;
using ProceduralMusic.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitButton : MonoBehaviour
{
    ComfortMusicBridge comfortMusicBridge;
    public void ExitGame()
    {
        Debug.Log("Exiting game...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1;
        comfortMusicBridge = FindObjectOfType<ComfortMusicBridge>();
        if (comfortMusicBridge != null)
        {
            comfortMusicBridge.OverrideGameState(GameMusicState.Cozy, PitchClass.C, MusicalMode.Major);
        }
        else
        {
            Debug.Log("ComfortMusicBridge not found. Cannot set music state to Exploring.");
        }
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadScene("Main Menu");
        else
            SceneManager.LoadScene("Main Menu"); // fallback if transition prefab missing

    }
}
