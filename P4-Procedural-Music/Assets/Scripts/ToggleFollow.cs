using UnityEngine;

public class ToggleFollow : MonoBehaviour
{
    [SerializeField] private TwigglingFollowScript Twiggling;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) 
        {
            if (Twiggling.following == true)
            {
                Twiggling.following = false;
            }
            else if (Twiggling.following == false)
            { 
                Twiggling.following = true;
            }
        }
    }
}
