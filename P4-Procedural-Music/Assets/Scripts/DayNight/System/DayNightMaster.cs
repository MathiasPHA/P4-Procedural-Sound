using System.IO;
using UnityEngine;

public class DayNightMaster : MonoBehaviour
{
    public static DayNightMaster Instance { get; private set; }

    public float dayLengthMinutes; // How long a full day lasts in real-time minutes

    [Range(0f, 24f)] // Adds a visual slider in the Inspector for seeing the time
    public float currentTime; // Starting time (0-24)

    private float _timeSpeed; // Hours per second

    private const string SAVE_FILENAME = "time.json";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        // Recalculate speed each frame so changes to dayLengthMinutes take effect immediately
        _timeSpeed = 24f / (dayLengthMinutes * 60f);

        currentTime += _timeSpeed * Time.deltaTime;

        if (currentTime >= 24f)
        {
            currentTime -= 24f;
        }
    }

    // Returns the current time as a formatted string such as "14:35", can be called in other scripts to display time on UI
    public string GetTimeString()
    {
        int hours = Mathf.FloorToInt(currentTime);
        int minutes = Mathf.FloorToInt((currentTime - hours) * 60f);
        return $"{hours:D2}:{minutes:D2}";
    }

    // =====================================================================
    // Save / Load
    // =====================================================================

    [System.Serializable]
    private class TimeSaveData
    {
        public float currentTime;
    }

    public void SaveTime(string worldName)
    {
        var data = new TimeSaveData { currentTime = this.currentTime };
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(GetSavePath(worldName), json);
    }

    public void LoadTime(string worldName)
    {
        string path = GetSavePath(worldName);
        if (!File.Exists(path)) return;

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<TimeSaveData>(json);
            currentTime = data.currentTime;
            Debug.Log($"[DayNightMaster] Loaded time: {GetTimeString()}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DayNightMaster] Failed to load time: {e.Message}");
        }
    }

    private string GetSavePath(string worldName)
    {
        string dir = Path.Combine(Application.persistentDataPath, "worlds", worldName);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return Path.Combine(dir, SAVE_FILENAME);
    }
}