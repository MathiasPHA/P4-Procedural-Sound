using UnityEngine;
using UnityEngine.InputSystem;
using InteractionSystem;

/// <summary>
/// TEMPORARY debug helper. Attach to the player. Press F to log everything
/// the system can see at the cursor position. Remove once fishing works.
/// </summary>
public class FishingDebugProbe : MonoBehaviour
{
    [SerializeField] private InteractionDetector detector;
    [SerializeField] private LayerMask checkLayer = ~0; // all layers

    void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.fKey.wasPressedThisFrame) return;

        var cam = Camera.main;
        if (cam == null) { Debug.Log("[Probe] no camera"); return; }
        if (Mouse.current == null) { Debug.Log("[Probe] no mouse"); return; }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world  = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        world.z = 0f;

        Debug.Log($"[Probe] === Cursor at {world} ===");

        // 1) What 2D colliders are under the cursor on ANY layer?
        var allHits = Physics2D.OverlapPointAll(world, checkLayer);
        Debug.Log($"[Probe] OverlapPointAll found {allHits.Length} colliders:");
        foreach (var h in allHits)
        {
            int layer = h.gameObject.layer;
            string layerName = LayerMask.LayerToName(layer);
            var inter = h.GetComponent<Interactable>();
            Debug.Log($"[Probe]   - '{h.gameObject.name}'  layer={layer}({layerName})  Interactable={(inter != null ? inter.GetType().Name : "none")}  CanInteract={(inter != null ? inter.CanInteract().ToString() : "n/a")}");
        }

        // 2) What does the detector currently see?
        if (detector != null)
        {
            Debug.Log($"[Probe] InteractionDetector.CurrentTarget = {(detector.CurrentTarget != null ? detector.CurrentTarget.GetType().Name : "null")}");
        }

        // 3) Confirm the Interactable layer index
        int interactLayer = LayerMask.NameToLayer("Interactable");
        Debug.Log($"[Probe] 'Interactable' layer index = {interactLayer} (-1 means the layer doesn't exist)");
    }
}
