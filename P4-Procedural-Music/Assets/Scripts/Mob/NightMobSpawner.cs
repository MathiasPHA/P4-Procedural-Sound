using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NightMobSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TimeReference timeReference;

    [Header("Spawn Settings")]
    [SerializeField] private GameObject[] nightMobPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int maxMobsAtOnce = 5;
    [SerializeField] private float spawnInterval = 30f;

    [Header("Night Windows")]
    [SerializeField] private string[] spawnDuringMilestones = { "Dusk", "Night", "Midnight" };

    private List<GameObject> _activeMobs = new();
    private Coroutine _spawnCoroutine;
    private bool _isNight = false;

    private void Update()
    {
        bool shouldBeNight = IsNightTime();

        if (shouldBeNight && !_isNight)
        {
            _isNight = true;
            _spawnCoroutine = StartCoroutine(SpawnRoutine());
        }
        else if (!shouldBeNight && _isNight)
        {
            _isNight = false;
            if (_spawnCoroutine != null)
                StopCoroutine(_spawnCoroutine);

            DespawnAllMobs();
        }
    }

    private bool IsNightTime()
    {
        foreach (string milestone in spawnDuringMilestones)
        {
            if (timeReference.timeOfDay == milestone)
                return true;
        }
        return false;
    }

    private IEnumerator SpawnRoutine()
    {
        while (_isNight)
        {
            CleanupDeadMobs();

            if (_activeMobs.Count < maxMobsAtOnce)
                SpawnMob();

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnMob()
    {
        if (nightMobPrefabs.Length == 0 || spawnPoints.Length == 0) return;

        var prefab = nightMobPrefabs[Random.Range(0, nightMobPrefabs.Length)];
        var point  = spawnPoints[Random.Range(0, spawnPoints.Length)];

        var mob = Instantiate(prefab, point.position, Quaternion.identity);
        _activeMobs.Add(mob);
    }

    private void DespawnAllMobs()
    {
        foreach (var mob in _activeMobs)
        {
            if (mob != null)
                Destroy(mob);
        }
        _activeMobs.Clear();
    }

    private void CleanupDeadMobs()
    {
        // Remove entries for mobs that have been destroyed externally (e.g. killed by player)
        _activeMobs.RemoveAll(m => m == null);
    }
}