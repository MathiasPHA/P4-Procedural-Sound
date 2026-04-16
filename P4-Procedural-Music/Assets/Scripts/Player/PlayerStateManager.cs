using InventorySystem.Data;
using InteractionSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStateManager : MonoBehaviour
{
    PlayerBaseState currentState;
    public PlayerBaseState CurrentState => currentState;
    public PlayerIdleState idleState = new PlayerIdleState();
    public PlayerRunState runState = new PlayerRunState();
    public PlayerInventoryState inventoryState = new PlayerInventoryState();
    public PlayerHarvestState harvestState = new PlayerHarvestState();
    public PlayerMusicPlayingState musicPlayingState = new PlayerMusicPlayingState();
    public PlayerMoveToInteractState moveToInteractState = new PlayerMoveToInteractState();
    public PlayerShrugState playerShrugState = new PlayerShrugState();

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
        // Don't update facing direction during harvest — prevents animation restart
        if (currentState != harvestState)
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

    public void StartMusicPlaying()
    {
        SwitchState(musicPlayingState);
    }

    public void StopMusicPlaying()
    {
        if (currentState == musicPlayingState)
            SwitchState(idleState);
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
        }
        else if (x > 0)
        {
            playerDir = "Right";
        }
        else if (y < 0)
        {
            playerDir = "Down";
        }
        else if (y > 0)
        {
            playerDir = "Up";
        }
    }

    /*public void StartShrug()
{
    SwitchState(playerShrugState);
}*/
}