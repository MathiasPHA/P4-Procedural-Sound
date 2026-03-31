using InventorySystem.Data;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStateManager : MonoBehaviour
{
    PlayerBaseState currentState;
    public PlayerIdleState idleState = new PlayerIdleState();
    public PlayerRunState runState = new PlayerRunState();
    public PlayerInventoryState inventoryState = new PlayerInventoryState();
    public PlayerHarvestState harvestState = new PlayerHarvestState();

    public string animationQue;

    public Rigidbody2D playerRB;
    public Vector2 moveInput;

    public string playerDir;

    public float moveSpeed;

    private void Awake()
    {
        playerRB = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        currentState = idleState;

        currentState.EnterState(this);
    }

    void Update()
    {
        GetDircetion(moveInput.x, moveInput.y);
        currentState.UpdateState(this);
    }

    public void SwitchState(PlayerBaseState state) 
    {
        currentState = state;
        currentState.EnterState(this);
    }

    public void StartHarvest()
    {
        SwitchState(harvestState);
    }

    public void OnHarvestAnimationComplete()
    {
        SwitchState(idleState);
    }

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    private void GetDircetion(float x, float y)
    {
        if (x < 0)
        {
            playerDir = "Left";
            //Debug.Log("Left");
        }
        else if (x > 0)
        {
            playerDir = "Right";
            //Debug.Log("Right");
        }
        else if (y < 0)
        {
            playerDir = "Down";
            //Debug.Log("Down");
        }
        else if (y > 0)
        {
            playerDir = "Up";
            //Debug.Log("Up");
        }
    }
}
