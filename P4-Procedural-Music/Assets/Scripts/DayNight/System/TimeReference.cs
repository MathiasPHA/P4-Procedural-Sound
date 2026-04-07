using UnityEngine;

public class TimeReference : MonoBehaviour
{
    // Purpose of this script is to make it easier to reference the current time and time milestone in other scripts that aren't part of the day/night system, such as
    // other scripts that need to know the current time of day for various reasons. This script will act as a reference point for the current time and time milestone,
    // allowing other scripts to easily access this information without needing to reference both the TimeMilestoneManager and DayNightMaster separately.


    [Header("Scripts")]
    // Scripts that are used to create the reference system
    public TimeMilestoneManager timeMilestoneManager;
    public DayNightMaster dayNightMaster;


    [Header("Variables")]
    // Variables used to create the reference system
    public string timeOfDay;
    [Range(0f, 24f)]
    public float time;
    public int timeRounded;
    public int[] timeReferenceArray;


    void Awake()
    {
        // Instantiate the scripts that are used to create the reference system
        timeMilestoneManager = GetComponent<TimeMilestoneManager>();
        dayNightMaster = GetComponent<DayNightMaster>();
    }

    private void Start()
    {
        foreach (int milestoneTime in timeMilestoneManager.timeMilestoneTimes)
        {
            timeReferenceArray = timeMilestoneManager.timeMilestoneTimes;
        }
    }


    void Update()
    {
        // Update the variables used to create the reference system
        time = dayNightMaster.currentTime;
        timeRounded = Mathf.RoundToInt(time);
        timeOfDay = timeMilestoneManager.currentTimeMilestone;
    }
}
