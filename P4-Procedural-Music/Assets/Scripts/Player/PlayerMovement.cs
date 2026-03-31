using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Timeline;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 moveInput;

    [SerializeField] private float moveSpeed;
    [SerializeField] private Canvas playerCanvas; // Drag your player's Canvas here in the Inspector

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        FlipX(moveInput.x);
    }

    private void FlipX(float x)
    {
        if (x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(x), 1, 1);

            // Counter-flip the canvas so it always stays upright
            if (playerCanvas != null)
                playerCanvas.transform.localScale = new Vector3(Mathf.Sign(x), 1, 1);
        }
    }

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        rb.linearVelocity = moveInput * moveSpeed;
    }
}