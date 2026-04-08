using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SunRotation : MonoBehaviour
{

    public TimeReference timeReference;
    public new Light2D light;
    public Color dayColor;
    public Color nightColor;

    public float lightIntensity = 1f;
    [SerializeField] private float t;

    private void Awake()
    {
        timeReference = GameObject.Find("Day Night System").GetComponent<TimeReference>();
    }

    void Start()
    {
        light = GetComponent<Light2D>(); // Get the Light2D component attached to the same GameObject
    }

    // Update is called once per frame
    void Update()
    {

        Daylight();

    }


    public void Daylight()
    {
        // If the current time is between 0:00 and 3:00, set the light intensity to 0 (no light)
        if (timeReference.time >= timeReference.timeReferenceArray[10] && timeReference.time < timeReference.timeReferenceArray[0])
        {
            // Midnight to Dawn (0:00 - 3:00): No light
            light.intensity = 0f;
        }
        // If the current time is between 3:00 and 12:00, ramp up the light intensity from 0 to the specified lightIntensity and transition the color from nightColor to dayColor
        else if (timeReference.time >= timeReference.timeReferenceArray[0] && timeReference.time < timeReference.timeReferenceArray[2])
        {
            // Dawn to Morning (3:00 - 7:00): Ramp UP intensity, transition color from nightColor to dayColor
            t = (timeReference.time - timeReference.timeReferenceArray[0]) / (timeReference.timeReferenceArray[2] - timeReference.timeReferenceArray[0]);
            light.intensity = lightIntensity * t;
            light.color = Color.Lerp(nightColor, dayColor, t);
        }
        else if (timeReference.time >= timeReference.timeReferenceArray[2] && timeReference.time < timeReference.timeReferenceArray[5])
        {
            // Morning to Afternoon (7:00 - 14:00): Full intensity, dayColor
            light.intensity = lightIntensity;
            light.color = dayColor;
        }
        // If the current time is between 14:00 and 23:00, ramp down the light intensity from the specified lightIntensity to 0 and transition the color from dayColor back to nightColor
        else if (timeReference.time >= timeReference.timeReferenceArray[5] && timeReference.time < timeReference.timeReferenceArray[8])
        {
            // Afternoon to Night (14:00 - 23:00): Ramp DOWN intensity, transition color from dayColor back to nightColor
            t = (timeReference.time - timeReference.timeReferenceArray[5]) / (timeReference.timeReferenceArray[8] - timeReference.timeReferenceArray[5]);
            light.intensity = lightIntensity * (1f - t);
            light.color = Color.Lerp(dayColor, nightColor, t);
        }

        // If the current time is between 23:00 and 0:00, set the light intensity to 0 (no light)
        else if (timeReference.time >= timeReference.timeReferenceArray[8] && timeReference.time < timeReference.timeReferenceArray[9])
        {
            // Night to Midnight (23:00 - 24:00): No light
            light.intensity = 0f;
        }
    }

}