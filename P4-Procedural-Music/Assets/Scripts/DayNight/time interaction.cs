using UnityEngine;

public class timeinteraction : MonoBehaviour
{
    public TimeReference timeReference; // Reference to the TimeReference script to access the current time of day
    public GameObject timeReferenceObject; // Reference to the GameObject that has the TimeReference script attached

    public SpriteRenderer spriteRenderer; // Reference to the SpriteRenderer component
    public Color currentColor; // Variable to store the current color of the sprite

    public float hueTimeMultiplier;

    float H, S, V;

    private void Awake()
    {
        timeReferenceObject = GameObject.Find("Day Night System"); // Find the GameObject by name that has the TimeReference script attached
        timeReference =  timeReferenceObject.GetComponent<TimeReference>(); // Get the TimeReference component from the found GameObject
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>(); // Get the SpriteRenderer component attached to the same GameObject

        hueTimeMultiplier = 360 / 24; // Calculate the multiplier to convert time of day (0-24) to hue (0-360)
        Debug.Log("Hue Time Multiplier: " + hueTimeMultiplier); // Log the calculated hue time multiplier for debugging purposes
    }

    // Update is called once per frame
    void Update()
    {

        GetColorHSV(currentColor); 
        spriteRenderer.color = currentColor;
    }

    public void GetColorHSV(Color color)
    {
            Color.RGBToHSV(color, out H, out S, out V); // Convert the current color from RGB to HSV and store the values in H, S, and V        
            H = (timeReference.time * hueTimeMultiplier) / 360; // Update the hue based on the current time of day and the hue time multiplier
            currentColor = Color.HSVToRGB(H, S, V); // Convert the updated HSV values back to RGB and store it in currentColor
    }

}
