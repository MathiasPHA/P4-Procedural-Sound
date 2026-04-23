using UnityEngine;

public class ConsoleProgressBar : MonoBehaviour
{
    private string[] Progress = new string[]
{
    "[                    ] 0%",   // [0]
    "[>                   ] 5%",   // [1]
    "[=>                  ] 10%",  // [2]
    "[==>                 ] 15%",  // [3]
    "[===>                ] 20%",  // [4]
    "[====>               ] 25%",  // [5]
    "[=====>              ] 30%",  // [6]
    "[======>             ] 35%",  // [7]
    "[=======>            ] 40%",  // [8]
    "[========>           ] 45%",  // [9]
    "[=========>          ] 50%",  // [10]
    "[==========>         ] 55%",  // [11]
    "[===========>        ] 60%",  // [12]
    "[============>       ] 65%",  // [13]
    "[=============>      ] 70%",  // [14]
    "[==============>     ] 75%",  // [15]
    "[===============>    ] 80%",  // [16]
    "[================>   ] 85%",  // [17]
    "[=================>  ] 90%",  // [18]
    "[==================> ] 95%",  // [19]
    "[===================>] 100%"  // [20]
};


    public Material material;

    [SerializeField] private float progressValue;
    [SerializeField] private string currentProgress;

    private FishingV1 fishingScript;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        material = GetComponent<Renderer>().material;
        fishingScript = GameObject.Find("Fish").GetComponent<FishingV1>();
    }
    void Update()
    {
        float points = fishingScript.fishingPoints;
        float target = fishingScript.targetPoints;

        progressValue = (points % target) / target; // Normalize to 0-1 range
        float progressClamp = Mathf.Clamp01(points / target);

        int index = Mathf.Clamp(Mathf.FloorToInt((points / target) * 20f), 0, 20);

        if (currentProgress != Progress[index])
        {
            currentProgress = Progress[index];
            Debug.Log(currentProgress);
        }

        if (fishingScript.fishingTime > 0 && progressClamp <= 1)
        {
            material.SetFloat("_Progress", Mathf.Min(progressClamp, 1f));
        }   

    }
}
