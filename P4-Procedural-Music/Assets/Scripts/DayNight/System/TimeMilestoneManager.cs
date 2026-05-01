using UnityEngine;
using UnityEngine.SceneManagement;

public class TimeMilestoneManager : MonoBehaviour
{
    [Header("Times of Day")]
    [SerializeField]
    private string[] timeMilestones = new string[]
    {
        "Midnight",
        "Dawn",
        "Sunrise",
        "Morning",
        "Noon",
        "Midday",
        "Afternoon",
        "Sunset",
        "Dusk",
        "Night",
    };

    [Header("References")]
    [SerializeField] private ComfortSystem comfortSystem;
    [SerializeField] private ComfortMusicBridge comfortMusicBridge;
    [SerializeField] private DayNightMaster dayNightMaster;

    [Header("Others")]
    public string currentTimeMilestone;
    private int roundedTime;

    private bool _initialized;

    // =====================================================================
    // Lifetime
    // =====================================================================

    private void Awake()
    {
        AutoAssignIfNeeded();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AutoAssignIfNeeded();
    }

    // =====================================================================
    // Auto wiring
    // =====================================================================

    private void AutoAssignIfNeeded()
    {
        if (dayNightMaster == null)
            dayNightMaster = FindSingle<DayNightMaster>();

        if (comfortSystem == null)
            comfortSystem = FindSingle<ComfortSystem>();

        if (comfortMusicBridge == null)
            comfortMusicBridge = FindSingle<ComfortMusicBridge>();
    }

    private T FindSingle<T>() where T : Object
    {
        var all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (all.Length > 1)
            Debug.LogWarning($"[TimeMilestoneManager] Multiple {typeof(T).Name} found — using first.");

        return all.Length > 0 ? all[0] : null;
    }

    // =====================================================================
    // Update loop
    // =====================================================================

    void Update()
    {
        if (dayNightMaster == null)
        {
            // Try to recover dynamically if it wasn't ready at load
            AutoAssignIfNeeded();
            return;
        }

        roundedTime = Mathf.RoundToInt(dayNightMaster.currentTime);
        CheckTime();
    }

    // =====================================================================
    // Time logic (unchanged)
    // =====================================================================

    private const int DawnTime = 3;
    private const int SunriseTime = 6;
    private const int MorningTime = 7;
    private const int MiddayTime = 11;
    private const int NoonTime = 12;
    private const int AfternoonTime = 14;
    private const int SunsetTime = 18;
    private const int DuskTime = 19;
    private const int NightTime = 23;
    private const int MidnightTime24 = 24;
    private const int MidnightTime0 = 0;

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

    private void CheckTime()
    {
        switch (roundedTime)
        {
            case DawnTime:
                currentTimeMilestone = timeMilestones[1];
                break;

            case SunriseTime:
                currentTimeMilestone = timeMilestones[2];
                if (comfortSystem != null) comfortSystem.SetDayNightValue(1);
                break;

            case MorningTime:
                currentTimeMilestone = timeMilestones[3];
                break;

            case MiddayTime:
                currentTimeMilestone = timeMilestones[5];
                break;

            case NoonTime:
                currentTimeMilestone = timeMilestones[4];
                break;

            case NoonTime + 1:
                currentTimeMilestone = timeMilestones[5];
                if (comfortMusicBridge != null) comfortMusicBridge.SetExploring2(true);
                break;

            case AfternoonTime:
                currentTimeMilestone = timeMilestones[6];
                break;

            case SunsetTime:
                currentTimeMilestone = timeMilestones[7];
                break;

            case DuskTime:
                currentTimeMilestone = timeMilestones[8];
                if (comfortSystem != null) comfortSystem.SetDayNightValue(0);
                break;

            case NightTime:
                currentTimeMilestone = timeMilestones[9];
                break;

            case MidnightTime24:
            case MidnightTime0:
                currentTimeMilestone = timeMilestones[0];
                break;

            case MidnightTime0 + 1:
                currentTimeMilestone = timeMilestones[9];
                break;
        }
    }
}