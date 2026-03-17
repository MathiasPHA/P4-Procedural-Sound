using UnityEngine;

public class DayNightMaster : MonoBehaviour
{
    public float dayLengthMinutes; // How long a full day lasts in real-time minutes

    [Range(0f, 24f)] // Adds a visual slider in the Inspector for seeing the time
    public float currentTime; // Starting time (0–24)

    private float _timeSpeed; // Hours per second

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
}
