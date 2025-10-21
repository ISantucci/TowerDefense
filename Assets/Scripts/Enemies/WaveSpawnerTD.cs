using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawnerTD : MonoBehaviour
{
    [Header("Refs")]
    public EnemyFactoryTD factory;
    public WaypointsPath path;
    public Transform spawnPoint;   

    [Header("Spawn")]
    public float spawnInterval = 0.8f;
    public float nextWaveDelay = 3f;

    [Header("Waves")]
    public int[] waveSizes = new int[] { 5, 8, 12 };

    Queue<EnemyId> currentQueue = new Queue<EnemyId>();
    int waveIndex = -1;
    bool finishedSpawningCurrent = false;
    bool launchingNext = false;

    int aliveCount = 0;  

    void OnEnable()
    {
        GameEvents.EnemySpawned += OnEnemySpawned;
        GameEvents.EnemyRemoved += OnEnemyRemoved;
    }

    void OnDisable()
    {
        GameEvents.EnemySpawned -= OnEnemySpawned;
        GameEvents.EnemyRemoved -= OnEnemyRemoved;
    }

    void Start()
    {
        if (path == null || path.points == null || path.points.Length == 0)
        {
            Debug.LogError("WaveSpawnerTD: falta asignar WaypointsPath");
            enabled = false; return;
        }
        StartNextWave();
    }

    void OnEnemySpawned() { aliveCount++; }
    void OnEnemyRemoved()
    {
        aliveCount = Mathf.Max(0, aliveCount - 1);
        CheckAdvance();
    }

    void CheckAdvance()
    {
        
        if (finishedSpawningCurrent && aliveCount == 0)
        {
            bool hayMasWaves = (waveIndex + 1) < waveSizes.Length;
            if (hayMasWaves && !launchingNext)
            {
                StartCoroutine(StartNextWaveDelayed());
            }
            else if (!hayMasWaves)
            {
                
                GameEvents.RaiseLevelWon();
            }
        }
    }
    IEnumerator StartNextWaveDelayed()
    {
        launchingNext = true;
        yield return new WaitForSeconds(nextWaveDelay);
        launchingNext = false;
        StartNextWave();
    }

    void StartNextWave()
    {
        waveIndex++;
        if (waveIndex >= waveSizes.Length)
        {
           return;
        }

        GameEvents.RaiseWaveChanged(waveIndex + 1, waveSizes.Length);

        currentQueue.Clear();
        for (int i = 0; i < waveSizes[waveIndex]; i++)
            currentQueue.Enqueue(EnemyId.Goblin); 

        finishedSpawningCurrent = false;
        StartCoroutine(SpawnCurrentWave());
        Debug.Log($"Wave {waveIndex + 1}/{waveSizes.Length} -> {currentQueue.Count} enemigos");
    }

    IEnumerator SpawnCurrentWave()
    {
        Vector3 pos = (spawnPoint != null ? spawnPoint.position : path.points[0].position);

        while (currentQueue.Count > 0)
        {
            var id = currentQueue.Dequeue();
            factory.Create(id, pos, Quaternion.identity, path);
            yield return new WaitForSeconds(spawnInterval);
        }

        finishedSpawningCurrent = true;
        CheckAdvance(); 
    }
}
