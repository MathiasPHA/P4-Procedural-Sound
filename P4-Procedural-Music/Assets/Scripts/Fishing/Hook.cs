using UnityEngine;
public class Hook : MonoBehaviour
{
    [Header("Script")]
    public FishingV1 fishingScript;

    [Header("Hook Positions")]
    private Vector3 inHPos;
    public Transform hookT;
    private float clampY = 2.55f;
    public Vector3 minHeight;
    public Vector3 maxHeight;

    [Header("Hook Movement")]
    public float hookMoveSpeed = 3f;
    private float smoothZone = 0.05f;

    void Start()
    {
        fishingScript = GameObject.Find("Fish").GetComponent<FishingV1>();

        hookT = gameObject.transform;
        inHPos = hookT.position;

        maxHeight = new Vector3(inHPos.x, inHPos.y + clampY, inHPos.z);
        minHeight = new Vector3(inHPos.x, inHPos.y - clampY, inHPos.z);

        hookT.position = minHeight;
    }

    void Update()
    {

        if (fishingScript.fishingTime > 0)
        {

            Vector3 currentPos = hookT.position;
            Vector3 target = Input.GetKey(KeyCode.Space) ? maxHeight : minHeight; // Move up when space is held, otherwise move down

            float distToTarget = Vector3.Distance(currentPos, target);

            // Calculate the interpolation factor based on distance to target
            float t = Mathf.InverseLerp(smoothZone, 0f, distToTarget);

            Vector3 linearMove = Vector3.MoveTowards(currentPos, target, hookMoveSpeed * Time.deltaTime);
            Vector3 smoothMove = Vector3.Lerp(currentPos, target, hookMoveSpeed * Time.deltaTime);

            hookT.position = Vector3.Lerp(linearMove, smoothMove, t);
        }
    }
}