using InteractionSystem;
using InventorySystem;
using InventorySystem.Data;
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
    public PlayerHurtState hurtState = new PlayerHurtState();
    public PlayerDeathState deathState = new PlayerDeathState();
    public PlayerFishingState fishingState = new PlayerFishingState();
    public ConsumableEffectManager consumableEffect;

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
        // Don't update facing direction during harvest, hurt, or death —
        // prevents animation restart / direction flip while locked
        if (currentState != harvestState && currentState != hurtState && currentState != deathState)
            GetDircetion(moveInput.x, moveInput.y);

        currentState.UpdateState(this);
    }

    public void SwitchState(PlayerBaseState state)
    {
        currentState = state;
        currentState.EnterState(this);
    }

    /// <summary>
    /// Most recent Collision2D received by the player. Set immediately before
    /// the current state's OnCollisionEnter callback is invoked, so states
    /// can read collision info without changing the abstract signature.
    /// Null between collision events.
    /// </summary>
    public Collision2D LastCollision { get; private set; }

    // Forward physics collisions to the current state so states like
    // moveToInteractState can react (e.g. "I bumped into water → start fishing").
    private void OnCollisionEnter2D(Collision2D collision)
    {
        LastCollision = collision;
        currentState?.OnCollisionEnter(this);
        LastCollision = null;
    }

    public void StartHarvest()
    {
        SwitchState(harvestState);
    }

    public void StartHurt(float stunDuration)
    {
        hurtState.Configure(stunDuration);
        SwitchState(hurtState);
    }

    public void StartDeath()
    {
        SwitchState(deathState);
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
        if (consumableEffect.invertControls == true)
        {
            moveInput = -moveInput;
        }
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