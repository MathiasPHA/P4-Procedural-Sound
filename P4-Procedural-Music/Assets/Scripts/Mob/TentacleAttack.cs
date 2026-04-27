using UnityEngine;
using System.Collections;
using MobSystem.Data;

public class TentacleAttack : MonoBehaviour
{
    [Header("Data")]
    public MobData mobData; // create a Shadow MobData asset, set happinessPenalty

    [Header("Visuals")]
    public Sprite tentacleSprite;
    public float tentacleLength = 5f;

    [Header("Spawn")]
    public Transform player;
    public float edgeOffsetDistance = 3f; // how far off-screen it spawns

    private SpriteRenderer sr;
    private Collider2D hitbox;
    private bool hasDealtDamage;

    // Call this to trigger a tentacle attack from a given screen edge direction
    public void Trigger(Vector2 spawnDirection)
    {
        StartCoroutine(AttackSequence(spawnDirection));
    }

    IEnumerator AttackSequence(Vector2 direction)
    {
        // ── Spawn off-screen ──────────────────────────────────────
        GameObject tentacleObj = new GameObject("Tentacle");
        tentacleObj.transform.position = player.position + 
            (Vector3)(direction.normalized * edgeOffsetDistance);

        sr = tentacleObj.AddComponent<SpriteRenderer>();
        sr.sprite = tentacleSprite;
        sr.sortingLayerName = "Entities"; // match your other mobs
        sr.color = new Color(1, 1, 1, 0);

        // Rotate to face the player
        float angle = Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg;
        tentacleObj.transform.rotation = Quaternion.Euler(0, 0, angle);

        // Trigger collider for damage
        CircleCollider2D col = tentacleObj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = mobData.attackHitboxRadius / 100f;
        hitbox = col;
        hasDealtDamage = false;

        // Listen for hits
        TentacleDamager damager = tentacleObj.AddComponent<TentacleDamager>();
        damager.mobData = mobData;

        // ── Windup — fade in slowly ───────────────────────────────
        yield return Fade(sr, 0f, 1f, mobData.attackWindupDuration);

        if (mobData.attackWindupSound != null)
            AudioSource.PlayClipAtPoint(mobData.attackWindupSound, 
                tentacleObj.transform.position, mobData.attackWindupVolume);

        // ── Strike — lunge toward player ──────────────────────────
        Vector3 strikeTarget = player.position;
        float elapsed = 0f;
        Vector3 startPos = tentacleObj.transform.position;

        while (elapsed < mobData.attackStrikeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / mobData.attackStrikeDuration;
            tentacleObj.transform.position = Vector3.Lerp(startPos, strikeTarget, t);
            yield return null;
        }

        // ── Recovery — retract and fade out ──────────────────────
        col.enabled = false; // stop dealing damage on retract
        elapsed = 0f;
        Vector3 retractTarget = startPos;
        Vector3 retractStart = tentacleObj.transform.position;

        while (elapsed < mobData.attackRecoveryDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / mobData.attackRecoveryDuration;
            tentacleObj.transform.position = Vector3.Lerp(retractStart, retractTarget, t);
            sr.color = new Color(1, 1, 1, 1f - t);
            yield return null;
        }

        Destroy(tentacleObj);
    }

    IEnumerator Fade(SpriteRenderer target, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(from, to, elapsed / duration);
            target.color = new Color(1, 1, 1, a);
            yield return null;
        }
    }
}

