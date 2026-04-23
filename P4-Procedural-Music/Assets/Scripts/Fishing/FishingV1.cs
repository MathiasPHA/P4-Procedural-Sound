using InventorySystem;
using InventorySystem.Data;
using UnityEngine;

public class FishingV1 : MonoBehaviour
{
    [Header("Fishing Time")]
    public float fishingTime = 8f;

    [Header("Fish Pos")]
    private Transform FishT;
    private Vector3 initialFishPos;
    public Vector3 fishPos;
    public Vector3 targetPos;
    [SerializeField] private float posClamp = 3f;
    [SerializeField] private float deltaDistance; // Debugger

    [Header("Fish Move")]
    public bool canFishMove = true;
    public bool isFishMoving = false;
    public float fishMoveSpeed = 1.5f;

    [Header("Fish Catch")]
    public bool isFishCaught = false;
    public bool addedToInventory = false;
    public float fishingPoints = 0f;
    public float targetPoints;
    [SerializeField] private float initialFishingTime;
    [SerializeField] private float tFraction = 0.7f;



    [Header("Drops")]
    [Tooltip("The item dropped when this resource is harvested.")]
    [SerializeField] private ItemData dropItem;

    [Tooltip("How many items drop when the resource is destroyed.")]
    [Min(1)][SerializeField] private int dropAmount = 1;


    [Tooltip("Sound played when harvesting with directToInventory.")]
    [SerializeField] private AudioClip pickupSound;
    [Range(0f, 1f)]
    [SerializeField] private float pickupVolume = 0.5f;
    [Tooltip("How much the pitch varies randomly each pickup (e.g. 0.15 = ±15%).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pitchVariation = 0.15f;



    private void OnTriggerStay2D(Collider2D other)
    {
        fishingPoints += 1f * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        fishingPoints += 1f * Time.deltaTime;
    }

    void Start()
    {
        FishT = gameObject.transform;
        initialFishPos = FishT.position;
        fishPos = FishT.position;
        addedToInventory = false;


        // Randomize the fishing time by adding a random value between -2 and 2 seconds
        fishingTime += Random.Range(-2f, 2f);
        initialFishingTime = fishingTime;
    }

    void Update()
    {

        if (fishingTime > 0)
        {

            fishPos = FishT.position;
            deltaDistance = Vector3.Distance(fishPos, targetPos);

            if (canFishMove)
            {
                targetPos = new Vector3(initialFishPos.x, initialFishPos.y + Random.Range(-posClamp, posClamp), initialFishPos.z);
                fishMoveSpeed = Random.Range(0.8f, 2f);
                canFishMove = false;
            }
            else
            {
                MoveFish();
                isFishMoving = true;
                if (isFishMoving)
                {
                    if (deltaDistance < 0.4f)
                    {
                        canFishMove = true;
                        isFishMoving = false;
                    }
                }
            }


        }

        FishTimer();
        FishCaught();
    }

    private void MoveFish()
    {
        FishT.position = Vector3.Lerp(fishPos, targetPos, fishMoveSpeed * Time.deltaTime);
    }

    private void FishTimer()
    {
        if (fishingTime >= 0)
        {
            fishingTime -= Time.deltaTime;
        }

    }

    private void FishCaught()
    {
        // Have Hook on fish for 70% of the initial fishing time to catch the fish
        targetPoints = initialFishingTime * tFraction;

        if (fishingPoints >= targetPoints && !addedToInventory)
        {
            isFishCaught = true;
            addedToInventory = true;

            // Add straight to player inventory
            var inventory = InventoryBootstrap.PlayerInventory;
            if (inventory != null)
            {
                int overflow = inventory.AddItem(dropItem, dropAmount);
                if (overflow > 0)
                    Debug.LogWarning($"[Harvestable] {overflow}x {dropItem.displayName} didn't fit.");
            }

            // Play sound with pitch variation (survives Destroy)
            /* 
               if (pickupSound != null)
                   PlaySoundWithPitch(pickupSound, transform.position, pickupVolume);
            */

            Debug.Log("Fish Caught!");
        }
    }


}
