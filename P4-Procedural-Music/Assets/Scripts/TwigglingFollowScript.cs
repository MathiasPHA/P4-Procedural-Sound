using Unity.VisualScripting;
using UnityEngine;

public class TwigglingFollowScript : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float followDistance;
    [SerializeField] private float stayDistance;

    public bool following;
    private Vector2 targetPosition;

    void Update()
    {
        targetPosition = followTarget.position;
        CheckFollowDistance();
        if (following)
        {
            MoveToTarget();
        }
    }

    void MoveToTarget()
    {
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed);
        if (Vector2.Distance(transform.position, followTarget.position) < stayDistance)
        {
            following = false;

        }
    }

    void CheckFollowDistance() 
    {
        if (Vector2.Distance(transform.position, targetPosition) < followDistance)
        {
            if (Vector2.Distance(transform.position, targetPosition) > stayDistance) 
            {
                following = true;
            }
            
        }
        else if (Vector2.Distance(transform.position, targetPosition) > followDistance)
        {
            following = false;
        }
    }

     
         
}

