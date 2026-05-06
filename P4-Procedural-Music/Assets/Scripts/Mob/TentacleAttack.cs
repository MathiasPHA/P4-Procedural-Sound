using UnityEngine;
using System.Collections;
using MobSystem.Data;
using MobSystem;

public class TentacleAttack : MonoBehaviour
{
    [Header("Data")]
    public MobData mobData;

    [Header("Visuals")]
    public float spriteScale = 3f;
    public float maxStretch = 1f;

    [Header("Spawn")]
    public GameObject tentaclePrefab;
    public Transform player;
    public float spawnOffset = 3f; // distance from player to spawn

    [Header("Intensity")]
    public PostProcessingManager postProcessingManager;
    public ComfortToEntityBridge comfortToEntityBridge; 

    public float intensityDecreaseSpeed = 0.2f;

    public void Trigger(Vector2 spawnDirection)
    {
        StartCoroutine(AttackSequence(spawnDirection));
    }

    IEnumerator AttackSequence(Vector2 direction)
    {
        //Spawn offset from player 
        Vector3 spawnPos = player.position + (Vector3)(direction.normalized * spawnOffset);

        //Point toward player
        Vector2 toPlayer = (player.position - spawnPos).normalized;
        float angle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

        GameObject tentacleObj = Instantiate(tentaclePrefab, spawnPos, Quaternion.Euler(0, 0, angle));
        tentacleObj.transform.localScale = new Vector3(0f, spriteScale, 1f);

        SpriteRenderer sr = tentacleObj.GetComponent<SpriteRenderer>();
        sr.color = new Color(1, 1, 1, 0);

        //Subscribe to death event
        MobController mobController = tentacleObj.GetComponent<MobController>();
        if (mobController != null)
            mobController.OnDeath += OnTentacleDied;

        float distance = Vector2.Distance(spawnPos, player.position);
        float elapsed = 0f;
        while (elapsed < mobData.attackWindupDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / mobData.attackWindupDuration;
            float currentLength = Mathf.Lerp(0f, distance, t);
            tentacleObj.transform.localScale = new Vector3(Mathf.Min(currentLength * spriteScale, maxStretch), spriteScale, 1f);            
            sr.color = new Color(1, 1, 1, t);
            yield return null;
        }

        if (mobData.attackWindupSound != null)
            AudioSource.PlayClipAtPoint(mobData.attackWindupSound,
                tentacleObj.transform.position, mobData.attackWindupVolume);
    }
    
        private void OnTentacleDied(MobController mob)
    {
        mob.OnDeath -= OnTentacleDied;
        ComfortToEntityBridge.IsDraining = true;
        if (comfortToEntityBridge != null)
            comfortToEntityBridge.TriggerComfortBoost();
        StartCoroutine(DrainIntensity());
    }

IEnumerator DrainIntensity()
{
    while (postProcessingManager.masterIntensity > 0f)
    {
        postProcessingManager.SetIntensity(
            postProcessingManager.masterIntensity - intensityDecreaseSpeed * Time.deltaTime);
        yield return null;
    }

    ComfortToEntityBridge.IsDraining = false; // resume comfort trigger
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