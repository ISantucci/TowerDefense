using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class WaveConfig
{
    public string name;
    public EnemyId enemyType = EnemyId.Goblin;
    public int count = 5;
    public float interval = 1f;
}

public class WaveSpawnerTD : MonoBehaviour
{
    [Header("Refs")]
    public EnemyFactoryTD enemyFactory;
    public Transform spawnPoint;

    [Header("Waves")]
    public WaveConfig[] waves;

    int currentWaveIndex = -1;
    int enemiesAlive = 0;
    bool spawning = false;

    void OnEnable()
    {
        GameEvents.EnemyRemoved += OnEnemyRemoved;
    }

    void OnDisable()
    {
        GameEvents.EnemyRemoved -= OnEnemyRemoved;
    }

    void Start()
    {
        StartNextWave();
    }

    void StartNextWave()
    {
        currentWaveIndex++;

        if (currentWaveIndex >= waves.Length)
        {
            Debug.Log("[WaveSpawnerTD] No hay más waves, nivel ganado.");
            GameEvents.RaiseLevelWon();
            return;
        }

        WaveConfig w = waves[currentWaveIndex];
        Debug.Log($"[WaveSpawnerTD] Wave {currentWaveIndex + 1}/{waves.Length} -> {w.count} {w.enemyType}");

        // Avisar al HUD
        GameEvents.RaiseWaveChanged(currentWaveIndex + 1, waves.Length);

        // Lanzar la corrutina de spawn
        StartCoroutine(SpawnWave(w));
    }

    IEnumerator SpawnWave(WaveConfig w)
    {
        spawning = true;

        for (int i = 0; i < w.count; i++)
        {
            enemyFactory.Spawn(w.enemyType, spawnPoint.position, Quaternion.identity);
            enemiesAlive++;
            yield return new WaitForSeconds(w.interval);
        }

        spawning = false;
        CheckWaveEnd();
    }

    void OnEnemyRemoved()
    {
        enemiesAlive--;
        CheckWaveEnd();
    }

    void CheckWaveEnd()
    {
        if (!spawning && enemiesAlive <= 0)
        {
            StartNextWave();
        }
    }
}
