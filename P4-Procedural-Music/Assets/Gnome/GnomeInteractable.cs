// using UnityEngine;

// public class GnomeInteractable : MonoBehaviour
// {
//     public string gnomeName = "Gnome Merchant";
//     public TradeOffer[] tradeOffers;
//     private float interactRange = 3f;
//     private Animator anim;

//     void Awake() => anim = GetComponent<Animator>();

//     void Update()
//     {
//         if (Input.GetMouseButtonDown(1))
//             TryInteract();
//     }

//     void TryInteract()
//     {
//         Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//         if (Physics.Raycast(ray, out RaycastHit hit, 10f))
//         {
//             if (hit.collider.gameObject == gameObject)
//             {
//                 float dist = Vector3.Distance(
//                     Camera.main.transform.position,
//                     transform.position);
//                 if (dist <= interactRange)
//                     TradeUI.Instance.OpenTrade(this);
//                 else
//                     Debug.Log("Too far away!");
//             }
//         }
//     }

//     public void StartTalking() => anim?.SetBool("isTalking", true);
//     public void StopTalking() => anim?.SetBool("isTalking", false);
// }