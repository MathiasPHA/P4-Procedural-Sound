using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CampfireFlicker : MonoBehaviour
{

    private float Flicker = 2.2f;
    private float fickerRange = 0.15f;
    public new Light2D light;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        light = GetComponent<Light2D>(); // Get the Light2D component attached to the same GameObject
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        light.intensity = Flicker + Random.Range(-fickerRange, fickerRange);
    }
}
