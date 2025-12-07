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
    [Header("Config")]
    [SerializeField] EnemyFactoryTD enemyFactory;
    [SerializeField] Transform spawnPoint;
    [SerializeField] WaveConfig[] waves;
    [SerializeField] float startDelay = 1f;

    WaveQueueTF waveQueue;
    bool isSpawning;
    int enemiesAlive;

    int totalWaves;
    int currentWaveNumber;   // 1,2,3,...

    void Awake()
    {
        waveQueue = new WaveQueueTF();
        waveQueue.InicializarCola(waves);

        totalWaves = waves != null ? waves.Length : 0;
        currentWaveNumber = 0;

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
        // no quedan waves en la cola
        if (waveQueue.ColaVacia())
        {
            if (enemiesAlive <= 0)
            {
                // acá ya no hay enemigos vivos -> nivel ganado
                Debug.Log("[WaveSpawnerTD] LEVEL WON -> RaiseLevelWon()");
                GameEvents.RaiseLevelWon();
            }
            return;
        }

        // avanzamos el número de wave
        currentWaveNumber++;

        // 🔹 aviso al HUD de la wave actual (X/Y)
        GameEvents.RaiseWaveChanged(currentWaveNumber, totalWaves);

        // tomo la siguiente wave y la disparo
        WaveConfig next = waveQueue.Primero();
        waveQueue.Desacolar();

        StartCoroutine(SpawnWave(next));
    }

    IEnumerator SpawnWave(WaveConfig wave)
    {
        isSpawning = true;

        int count = wave.enemyCount;
        float delay = wave.spawnInterval;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;

            var enemy = enemyFactory.Spawn(wave.enemyType, pos, Quaternion.identity);
            if (enemy != null)
            {
                enemiesAlive++;
                GameEvents.RaiseEnemySpawned();
            }

            yield return new WaitForSeconds(delay);
        }

        isSpawning = false;

        // si no quedan enemigos vivos, pasamos de wave
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

    // ==========================================
    //            COLA TIPO TF
    // ==========================================
    class WaveQueueTF
    {
        WaveConfig[] elementos;
        int indice; // cantidad actual

        public void InicializarCola(WaveConfig[] origen)
        {
            if (origen == null)
            {
                elementos = new WaveConfig[0];
                indice = 0;
                return;
            }

            elementos = new WaveConfig[origen.Length];
            for (int i = 0; i < origen.Length; i++)
                elementos[i] = origen[i];

            indice = origen.Length;
        }

        public bool ColaVacia()
        {
            return indice == 0;
        }

        public WaveConfig Primero()
        {
            if (indice == 0) return null;
            return elementos[0];
        }

        public void Desacolar()
        {
            if (indice == 0) return;

            for (int i = 1; i < indice; i++)
                elementos[i - 1] = elementos[i];

            elementos[indice - 1] = null;
            indice--;
        }
    }
}
