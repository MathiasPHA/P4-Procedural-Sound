using UnityEngine;

public class TimeMilestoneManager : MonoBehaviour
{
    [Header("Times of Day")]
    [SerializeField] private string[] timeMilestones = new string[]
    {
        "Midnight",     // 0
        "Dawn",         // 1
        "Sunrise",      // 2
        "Morning",      // 3
        "Noon",         // 4
        "Midday",       // 5
        "Afternoon",    // 6
        "Sunset",       // 7
        "Dusk",         // 8
        "Night",        // 9
    };

    // Det her er lort, men det virker ikke hvis man bruger et array, da de ikke kan være sat til at være const
    private const int DawnTime = 5;         // Dawn starts at 5:00
    private const int SunriseTime = 6;      // Sunrise at 6:00
    private const int MorningTime = 7;      // Morning starts at 8:00
    private const int MiddayTime = 11;      // Midday starts at 11:00
    private const int NoonTime = 12;        // Noon at 12:00
    private const int AfternoonTime = 14;   // Afternoon starts at 13:00
    private const int SunsetTime = 18;      // Sunset at 18:00
    private const int DuskTime = 19;        // Dusk starts at 18:00
    private const int NightTime = 22;       // Night starts at 22
    private const int MidnightTime24 = 24;  // Midnight at 24:00 - Kun gjordt for ikke at misse den ved uheld
    private const int MidnightTime0 = 0;    // Midnight at 00:00 - Kun gjordt for ikke at misse den ved uheld

    public int[] timeMilestoneTimes = new int[]
    {
        DawnTime,       
        SunriseTime,    
        MorningTime,    
        NoonTime,       
        MiddayTime,     
        AfternoonTime,  
        SunsetTime,     
        DuskTime,       
        NightTime,      
        MidnightTime24, 
        MidnightTime0,  
    };


    [Header("Others")]
    public string currentTimeMilestone;
    private int roundedTime;
    private DayNightMaster dayNightMaster;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        dayNightMaster = GetComponent<DayNightMaster>();
    }

    // Update is called once per frame
    void Update()
    {
        roundedTime = Mathf.RoundToInt(dayNightMaster.currentTime);
        CheckTime();
    }

    private void CheckTime()
    {
        switch (roundedTime)
        {
            case DawnTime:
                // 5:00 - 5:59 is Dawn
                currentTimeMilestone = timeMilestones[1];
                break;
            case SunriseTime:
                // 6:00 - 7:59 is Sunrise
                currentTimeMilestone = timeMilestones[2];
                break;
            case MorningTime:
                // 7:00 - 10:59 is Morning
                currentTimeMilestone = timeMilestones[3];
                break;
            case MiddayTime:
                // 11:00 - 11:59 is Midday
                currentTimeMilestone = timeMilestones[5];
                break;
            case NoonTime:
                // 12:00 - 12:59 is Noon - Jeg lavede en fejl i arrayet
                currentTimeMilestone = timeMilestones[4];
                break;
            case NoonTime + 1:
                // 13:00 - 13:59 is Midday
                currentTimeMilestone = timeMilestones[5];
                break;
            case AfternoonTime:
                // 14:00 - 17:59 is Afternoon
                currentTimeMilestone = timeMilestones[6];
                break;
            case SunsetTime:
                // 18:00 - 18:59 is Sunset
                currentTimeMilestone = timeMilestones[7];
                break;
            case DuskTime:
                // 19:00 - 20:59 is Dusk
                currentTimeMilestone = timeMilestones[8];
                break;
            case NightTime:
                // 21:00 - 23:59 is Night
                currentTimeMilestone = timeMilestones[9];
                break;
            case MidnightTime24:
                // 24:00 - 0:59 is Midnight
                currentTimeMilestone = timeMilestones[0];
                break;
            case MidnightTime0:
                // 00:00 - 0:59 is Midnight
                currentTimeMilestone = timeMilestones[0];
                break;
            case MidnightTime0 + 1:
                // 1:00 - 4:59 is still Night
                currentTimeMilestone = timeMilestones[9];
                break;
        }
    }

}
