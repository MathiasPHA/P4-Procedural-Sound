using UnityEngine;

public class ShadowTendrillUI : MonoBehaviour
{
    private Animator animator;

    public bool big;
    public bool medium;
    public bool small;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (big)
        {
            animator.Play("BigTendril");
        }
        else if (medium)
        {
            animator.Play("MediumTendril");
        }
        else if (small)
        {
            animator.Play("SmallTendril");
        }
    }

}
