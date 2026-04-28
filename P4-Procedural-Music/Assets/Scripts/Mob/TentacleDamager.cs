using UnityEngine;
using MobSystem.Data;
using MobSystem;

public class TentacleDamager : MonoBehaviour
{
    public MobData mobData;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.TakeDamage(0, mobData.happinessPenalty, transform.position);
    }
}