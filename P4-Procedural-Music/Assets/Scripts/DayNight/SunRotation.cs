using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SunRotation : MonoBehaviour
{

    public TimeReference timeReference;
    public new Light2D light;

    public float lightIntensity = 1.35f;

    private void Awake()
    {
        timeReference = GameObject.Find("Day Night System").GetComponent<TimeReference>();


    }

    void Start()
    {
        light = GetComponent<Light2D>(); // Get the Light2D component attached to the same GameObject
        lightIntensity = light.intensity; // Get the initial intensity of the sun light and store it in lightIntensity


    }

    // Update is called once per frame
    void Update()
    {
        Vector2 center = new Vector2(0, 0);
        transform.position = GetRotatedPosition(timeReference.time, 10f, center);


        light.intensity = lightIntensity * (timeReference.time / 24f);

        if (lightIntensity >= 1.35f)
        {
            lightIntensity -= 1.35f;
        }

    }

    Vector2 GetRotatedPosition(float hour, float radius, Vector2 center)
    {
        // Map 0-24 to 0-360 degrees
        float angle = (hour / 24f) * Mathf.PI * -2f;

        // Offset by -pi/2 so 0 starts at the top (12 o'clock)
        float x = center.x + Mathf.Cos(angle - Mathf.PI / 2f) * radius;
        float y = center.y + Mathf.Sin(angle - Mathf.PI / 2f) * radius;

        return new Vector2(x, y);
    }

}
