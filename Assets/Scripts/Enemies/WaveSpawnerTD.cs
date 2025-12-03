using System;
using System.Collections;
using System.Collections.Generic; 
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

    [Header("Waves (config en Inspector)")]
    public WaveConfig[] waves;   

    //COLA REAL DE WAVES
    Queue<WaveConfig> waveQueue;

    int currentWaveIndex = -1;   // solo para HUD / debug
    int totalWaves = 0;          // cantidad total (para mostrar en HUD)
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
        BuildWaveQueue();
        StartNextWave();
    }

    // 👇 Armamos la cola a partir del array del Inspector
    void BuildWaveQueue()
    {
        waveQueue = new Queue<WaveConfig>();

        if (waves != null)
        {
            foreach (var w in waves)
            {
                if (w != null)
                    waveQueue.Enqueue(w);   // ENQUEUE 
            }
        }

        totalWaves = waveQueue.Count;
        if (totalWaves == 0)
        {
            Debug.LogWarning("[WaveSpawnerTD] No hay waves configuradas.");
        }
    }

    void StartNextWave()
    {
        if (spawning) return;

        // Si la cola está vacía, no hay más waves
        if (waveQueue == null || waveQueue.Count == 0)
        {
            Debug.Log("[WaveSpawnerTD] No hay más waves, nivel ganado.");
            GameEvents.RaiseLevelWon();
            return;
        }

        // Índice lógico para HUD (Wave 1, Wave 2, etc.)
        currentWaveIndex++;

        // DEQUEUE = DESACOLAR: saco la wave del frente de la cola
        WaveConfig w = waveQueue.Dequeue();

        Debug.Log($"[WaveSpawnerTD] Wave {currentWaveIndex + 1}/{totalWaves} -> {w.count} {w.enemyType}");

        // Avisar al HUD (Wave actual y total)
        GameEvents.RaiseWaveChanged(currentWaveIndex + 1, totalWaves);

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

            // si querés, acá podrías avisar EnemySpawned:
            // GameEvents.RaiseEnemySpawned();

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
