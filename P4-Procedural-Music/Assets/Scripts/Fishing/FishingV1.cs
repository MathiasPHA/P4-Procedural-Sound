using UnityEngine;

public class FishingV1 : MonoBehaviour
{

    public float FishingTime = 10f;

    [SerializeField] private Transform Fish;
    [SerializeField] private Transform startFishPos;
    [SerializeField] private float fishClamp;
    [SerializeField] private float fishDestination;
    [SerializeField] private float fishPos;
    [SerializeField] private float swimSpeed = 1f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (FishingTime > 0)
        {
            FishingTime -= Time.deltaTime;
            Fishing();
        }
    }

    private void Fishing()
    {
        

    }

}
