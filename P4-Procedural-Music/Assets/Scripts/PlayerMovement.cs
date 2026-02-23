using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Timeline;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    //private Vector3 mouseWorldPosition;

    [SerializeField] private float moveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        /*LookAtMouse();
        if (body.linearVelocity.x != 0 || body.linearVelocity.y != 0)
        {
            animationTracker.isIdle = false;
            animationTracker.isRunning = true;
        }
        else
        {
            animationTracker.isRunning = false;
            animationTracker.isIdle = true;
        }*/

    }

    /*private void FlipX(float x)
    {
        if (x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(x), 1, 1);
        }
    }

    private void LookAtMouse()
    {
        mouseWorldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        var mouseForwardPosition = mouseWorldPosition + (Camera.main.transform.forward * 10.0f);
        var dir = (mouseForwardPosition - (Vector3)body.position).normalized;

        FlipX(dir.x);
    }*/

    private void OnMove(InputValue value)
    {
        var moveInput = value.Get<Vector2>();
        rb.linearVelocity = moveInput * moveSpeed;

    }
}
