using UnityEngine;

public class SpriteSorting : MonoBehaviour
{
    public bool isColliding = false;
    void Start()
    {
        
    }
    void Update()
    {
        
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Mob"))
        {
            isColliding = true;
            GetComponent<SpriteRenderer>().sortingOrder = 10;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Mob"))
        {
            isColliding = false;
            GetComponent<SpriteRenderer>().sortingOrder = 0;
        }
    }
}
