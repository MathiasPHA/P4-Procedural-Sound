using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SunRotation : MonoBehaviour
{

    public TimeReference timeReference;
    public new Light2D light;
    public Color dayColor;
    public Color nightColor;

    public float lightIntensity = 1.35f;
    private float t;

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
        if (timeReference.time >= timeReference.timeReferenceArray[10] && timeReference.time < timeReference.timeReferenceArray[0])
        {
            // Midnight to Dawn (0:00 - 5:00): No light
            light.intensity = 0f;
        }
        else if (timeReference.time >= timeReference.timeReferenceArray[0] && timeReference.time < timeReference.timeReferenceArray[3])
        {
            // Dawn to Noon (5:00 - 12:00): Ramp UP intensity, transition color from nightColor to dayColor
            t = (timeReference.time - timeReference.timeReferenceArray[0]) / (timeReference.timeReferenceArray[3] - timeReference.timeReferenceArray[0]);
            light.intensity = lightIntensity * t;
            light.color = Color.Lerp(nightColor, dayColor, t);
        }
        else if (timeReference.time >= timeReference.timeReferenceArray[3] && timeReference.time < timeReference.timeReferenceArray[8])
        {
            // Noon to Night (12:00 - 22:00): Ramp DOWN intensity, transition color from dayColor back to nightColor
            t = (timeReference.time - timeReference.timeReferenceArray[3]) / (timeReference.timeReferenceArray[8] - timeReference.timeReferenceArray[3]);
            light.intensity = lightIntensity * (1f - t);
            light.color = Color.Lerp(dayColor, nightColor, t);
        }
        else if (timeReference.time >= timeReference.timeReferenceArray[8] && timeReference.time < timeReference.timeReferenceArray[9])
        {
            // Night to Midnight (22:00 - 24:00): No light
            light.intensity = 0f;
        }
    }

}
