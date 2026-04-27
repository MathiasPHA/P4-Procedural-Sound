using UnityEngine;

public class ConsoleProgressBar : MonoBehaviour
{
    private string[] Progress = new string[]
    {
        "[                    ] 0%",
        "[>                   ] 5%",
        "[=>                  ] 10%",
        "[==>                 ] 15%",
        "[===>                ] 20%",
        "[====>               ] 25%",
        "[=====>              ] 30%",
        "[======>             ] 35%",
        "[=======>            ] 40%",
        "[========>           ] 45%",
        "[=========>          ] 50%",
        "[==========>         ] 55%",
        "[===========>        ] 60%",
        "[============>       ] 65%",
        "[=============>      ] 70%",
        "[==============>     ] 75%",
        "[===============>    ] 80%",
        "[================>   ] 85%",
        "[=================>  ] 90%",
        "[==================> ] 95%",
        "[===================>] 100%"
    };

    [SerializeField] private Transform progT;
    [SerializeField] private string currentProgress;

    private const float MIN_POS = -8.25f;
    private const float MAX_POS = 8.25f;

    private FishingV1 fishingScript;

    void Start()
    {
        fishingScript = GameObject.Find("Fish").GetComponent<FishingV1>();
        progT = transform.GetChild(0);
    }

    void Update()
    {
        float points = fishingScript.fishingPoints;
        float target = fishingScript.targetPoints;
        float progressClamp = Mathf.Clamp01(points / target);

        // Convert 0-1 progress into one of 21 steps (0-20) matching the Progress array indices
        int index = Mathf.Clamp(Mathf.FloorToInt(progressClamp * 20f), 0, 20);

        // Only log when the progress step actually changes to avoid spamming the console every frame
        if (currentProgress != Progress[index])
        {
            currentProgress = Progress[index];
            Debug.Log(currentProgress);
        }

        if (fishingScript.fishingTime > 0)
        {
            float targetY = Mathf.Lerp(MIN_POS, MAX_POS, progressClamp);
            Vector3 pos = progT.localPosition;
            progT.localPosition = new Vector3(pos.x, targetY, pos.z);
        }
    }
}