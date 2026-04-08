using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    public Texture2D cursorTexture;
    public Texture2D cursorTextureClicked;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
    }
    private void OnClick() 
    {
        Cursor.SetCursor(cursorTextureClicked, Vector2.zero, CursorMode.Auto);
    }
    private void OnReleaseClick() 
        {
            Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
    }
}
