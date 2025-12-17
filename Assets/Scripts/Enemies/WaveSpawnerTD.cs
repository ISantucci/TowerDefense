using System.Collections;
using UnityEngine;

[System.Serializable]
public class WaveConfig
{
    public EnemyId enemyType;
    public int enemyCount;
    public float spawnInterval;
}

public class WaveSpawnerTD : MonoBehaviour
{
    [SerializeField] EnemyFactoryTD enemyFactory;
    [SerializeField] Transform spawnPoint;
    [SerializeField] WaveConfig[] waves;
    [SerializeField] float startDelay = 1f;

    WaveQueueTF waveQueue;
    bool isSpawning;
    int enemiesAlive;

    int waveIndex;
    int totalWaves;

    void Awake()
    {
        totalWaves = waves != null ? waves.Length : 0;
        waveIndex = 0;

        waveQueue = new WaveQueueTF();
        waveQueue.InicializarCola(waves);

        GameEvents.EnemyRemoved += OnEnemyRemoved;
    }

    void OnDestroy()
    {
        GameEvents.EnemyRemoved -= OnEnemyRemoved;
    }

    void Start()
    {
        StartCoroutine(StartFirstWave());
    }

    IEnumerator StartFirstWave()
    {
        yield return new WaitForSeconds(startDelay);
        StartNextWave();
    }

    void StartNextWave()
    {
        if (waveQueue.ColaVacia())
        {
            if (enemiesAlive <= 0)
                EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.LevelWon));
            return;
        }

        waveIndex++;
        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.WaveChanged, waveIndex, totalWaves));

        WaveConfig next = waveQueue.Primero();
        waveQueue.Desacolar();
        StartCoroutine(SpawnWave(next));
    }

    IEnumerator SpawnWave(WaveConfig wave)
    {
        isSpawning = true;

        for (int i = 0; i < wave.enemyCount; i++)
        {
            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            var enemy = enemyFactory.Spawn(wave.enemyType, pos, Quaternion.identity);
            if (enemy != null) enemiesAlive++;
            yield return new WaitForSeconds(wave.spawnInterval);
        }

        isSpawning = false;
        if (enemiesAlive <= 0)
            StartNextWave();
    }

    void OnEnemyRemoved()
    {
        enemiesAlive--;
        if (enemiesAlive < 0) enemiesAlive = 0;
        if (!isSpawning && enemiesAlive == 0)
            StartNextWave();
    }

    class WaveQueueTF
    {
        WaveConfig[] elementos;
        int indice;

        public void InicializarCola(WaveConfig[] origen)
        {
            if (origen == null) { elementos = new WaveConfig[0]; indice = 0; return; }
            elementos = new WaveConfig[origen.Length];
            for (int i = 0; i < origen.Length; i++) elementos[i] = origen[i];
            indice = origen.Length;
        }

        public bool ColaVacia() => indice == 0;

        public WaveConfig Primero() => indice == 0 ? null : elementos[0];

        public void Desacolar()
        {
            if (indice == 0) return;
            for (int i = 1; i < indice; i++) elementos[i - 1] = elementos[i];
            elementos[indice - 1] = null;
            indice--;
        }
    }
}
