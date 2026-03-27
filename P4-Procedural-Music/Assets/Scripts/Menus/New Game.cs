using UnityEngine;
using UnityEngine.SceneManagement; // Required for switching scenes

public class NewGame : MonoBehaviour
{
    // Make sure the name matches your scene exactly!
    public void LoadNewGame(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}