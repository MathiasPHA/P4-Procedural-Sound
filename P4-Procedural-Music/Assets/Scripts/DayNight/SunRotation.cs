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
    }

    // Update is called once per frame
    void Update()
    {

        if (lightIntensity <= 1.35f)
        {
            light.intensity = lightIntensity * (timeReference.time / 12f);
        }


        if (lightIntensity >= 1.35f)
        {
            light.intensity = -1 * lightIntensity * (timeReference.time / 12f);
        }

    }

}
