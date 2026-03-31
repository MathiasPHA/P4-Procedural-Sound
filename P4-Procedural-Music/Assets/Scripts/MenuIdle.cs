using UnityEngine;

public class MenuIdle : MonoBehaviour
{
    private Animator mainIdle;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainIdle = GetComponent<Animator>();
        mainIdle.Play("PlayerIdleLeft");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
