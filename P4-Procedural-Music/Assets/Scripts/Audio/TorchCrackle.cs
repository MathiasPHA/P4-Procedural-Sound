using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TorchFireCrackle : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("The fire crackling sound effect")]
    public AudioClip fireCrackleSound;
    
    [Header("Distance Settings")]
    [Tooltip("Maximum distance at which the sound can be heard")]
    [Range(1f, 50f)]
    public float maxHearingDistance = 15f;
    
    [Tooltip("Distance at which sound reaches maximum volume")]
    [Range(0.1f, 10f)]
    public float minDistance = 1f;
    
    [Header("Volume Settings")]
    [Tooltip("Maximum volume when player is close")]
    [Range(0f, 1f)]
    public float maxVolume = 1f;
    
    [Tooltip("Minimum volume at max distance")]
    [Range(0f, 1f)]
    public float minVolume = 0f;

    private AudioSource audioSource;
    private Transform playerTransform;

    void Start()
    {
        // Get or add AudioSource component
        audioSource = GetComponent<AudioSource>();
        
        // Configure AudioSource for 3D spatial audio
        audioSource.clip = fireCrackleSound;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f; // Full 3D sound
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxHearingDistance;
        audioSource.volume = maxVolume;
        
        // Start playing the fire crackle
        if (fireCrackleSound != null)
        {
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("Fire crackle sound not assigned to " + gameObject.name);
        }
        
        // Find the player - adjust the tag if needed
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("Player not found. Make sure your player has the 'Player' tag.");
        }
    }

    void Update()
    {
        // Optional: Manual volume control based on distance
        // Unity's 3D audio handles this automatically, but you can use this for fine-tuning
        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            float volumeMultiplier = Mathf.Clamp01(1 - (distance - minDistance) / (maxHearingDistance - minDistance));
            audioSource.volume = Mathf.Lerp(minVolume, maxVolume, volumeMultiplier);
        }
    }

    // Visualize the audio range in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, minDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxHearingDistance);
    }
}