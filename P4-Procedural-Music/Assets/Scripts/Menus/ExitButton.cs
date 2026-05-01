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
        // Save everything before leaving the game scene.
        if (SaveSystemManager.Instance != null)
            SaveSystemManager.Instance.SaveModifiedChunks();
        else
            Debug.LogWarning("[ExitButton] SaveSystemManager not found — game was not saved.");

        Time.timeScale = 1f;

        comfortMusicBridge = FindObjectOfType<ComfortMusicBridge>();
        if (comfortMusicBridge != null)
            comfortMusicBridge.OverrideGameState(GameMusicState.Cozy, PitchClass.C, MusicalMode.Major);

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadScene("Main Menu");
        else
            SceneManager.LoadScene("Main Menu");
    }
}