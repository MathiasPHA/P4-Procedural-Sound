using UnityEngine;

public class CampfireAnimations : MonoBehaviour
{
    [SerializeField] private CampfireController campfireController;
    private Animator animator;
    void Start()
    {
       animator = GetComponent<Animator>();
    }


    void Update()
    {
        if (campfireController.CurrentBurnState == CampfireController.BurnState.Big)
        {
            animator.Play("Campfire_High");
        }
        else if (campfireController.CurrentBurnState == CampfireController.BurnState.Medium)
        {
            animator.Play("Campfire_Medium");
        }
        else if (campfireController.CurrentBurnState == CampfireController.BurnState.Low) 
        {
            animator.Play("Campfire_Low");
        }
        else if (campfireController.CurrentBurnState == CampfireController.BurnState.Out)
        {
            animator.Play("Campfire_Out");
        }

    }
}
