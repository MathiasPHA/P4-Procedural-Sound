using UnityEngine;

public class ExitButton : MonoBehaviour
{
public void ExitGame()
    {
        Debug.Log("Exiting game...");
        if (Application.isEditor)
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
        else
        {
            Application.Quit();
        }
    }
}
