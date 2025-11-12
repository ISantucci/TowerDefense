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

    [Header("Debug")]
    [SerializeField] bool dataTrace = true; // TRACE: toggle para logs

    public EnemyGraphPath graphProvider;

    void Trace(string msg)              // TRACE: helper (no altera lógica)
    {
        if (dataTrace) Debug.Log($"[WaveSpawnerTD] {msg}");
    }

    void OnEnable()
    {
        GameEvents.EnemySpawned += OnEnemySpawned;
        GameEvents.EnemyRemoved += OnEnemyRemoved;
        Trace("OnEnable suscripto a eventos"); // TRACE
    }

    void OnDisable()
    {
        GameEvents.EnemySpawned -= OnEnemySpawned;
        GameEvents.EnemyRemoved -= OnEnemyRemoved;
        Trace("OnDisable desuscripto de eventos"); // TRACE
    }

    void Start()
    {
        if (path == null || path.points == null || path.points.Length == 0)
        {
            Debug.LogError("WaveSpawnerTD: falta asignar WaypointsPath");
            enabled = false; return;
        }
        Trace($"Start OK. Waypoints: {path.points.Length}"); // TRACE
        StartNextWave();
    }

    void OnEnemySpawned()
    {
        aliveCount++;
        Trace($"EnemySpawned -> aliveCount={aliveCount}"); // TRACE
    }

    void OnEnemyRemoved()
    {
        aliveCount = Mathf.Max(0, aliveCount - 1);
        Trace($"EnemyRemoved -> aliveCount={aliveCount}"); // TRACE
        CheckAdvance();
    }

    void CheckAdvance()
    {
        // TRACE: mostramos estado clave para entender el avance
        Trace($"CheckAdvance finishedSpawningCurrent={finishedSpawningCurrent}, aliveCount={aliveCount}, waveIndex={waveIndex}/{waveSizes.Length - 1}");

        if (finishedSpawningCurrent && aliveCount == 0)
        {
            bool hayMasWaves = (waveIndex + 1) < waveSizes.Length;
            if (hayMasWaves && !launchingNext)
            {
                Trace("Listo para siguiente wave (cola vacía y sin vivos). Arranco delay..."); // TRACE
                StartCoroutine(StartNextWaveDelayed());
            }
            else if (!hayMasWaves)
            {
                Trace("No hay más waves. Level WON"); // TRACE
                GameEvents.RaiseLevelWon();
            }
        }
    }

    IEnumerator StartNextWaveDelayed()
    {
        launchingNext = true;
        Trace($"Esperando {nextWaveDelay} s para próxima wave..."); // TRACE
        yield return new WaitForSeconds(nextWaveDelay);
        launchingNext = false;
        StartNextWave();
    }

    void StartNextWave()
    {
        waveIndex++;
        if (waveIndex >= waveSizes.Length)
        {
            Trace("StartNextWave llamado pero no hay más waves."); // TRACE
            return;
        }

        if (EnemyPriorityABB.Instance != null)
            EnemyPriorityABB.Instance.Clear();

        int size = waveSizes[waveIndex];
        GameEvents.RaiseWaveChanged(waveIndex + 1, waveSizes.Length);

        currentQueue.Clear();
        Trace($"Wave {waveIndex + 1}/{waveSizes.Length} -> preparando cola con {size} enemigos"); // TRACE

        for (int i = 0; i < size; i++)
        {
            currentQueue.Enqueue(EnemyId.Goblin);
            // opcional: evita spam, pero te deja ver la progresión
            if (dataTrace && (i < 5 || i == size - 1))
                Trace($"Enqueue {EnemyId.Goblin} (i={i}) -> queueCount={currentQueue.Count}"); // TRACE
        }

        finishedSpawningCurrent = false;
        StartCoroutine(SpawnCurrentWave());
        Trace($"Wave {waveIndex + 1} armada. queueCount={currentQueue.Count}"); // TRACE
    }

    IEnumerator SpawnCurrentWave()
    {
        Vector3 pos = (spawnPoint != null ? spawnPoint.position : path.points[0].position);
        Trace("SpawnCurrentWave iniciado");

        while (currentQueue.Count > 0)
        {
            var id = currentQueue.Dequeue();
            Trace($"Dequeue {id} -> queueCount={currentQueue.Count}");

            // 1) Crear enemigo
            var go = factory.Create(id, pos, Quaternion.identity, path);
            if (go != null)
            {
                // 2) Calcular ruta con Dijkstra en ESTE enemigo
                var gpath = go.GetComponent<EnemyGraphPath>();
                if (gpath != null)
                {
                    gpath.path = path; // aseguramos el path
                    var route = gpath.ComputeAndGetPath();

                    // 3) Inyectar ruta al movimiento
                    var move = go.GetComponent<EnemyMovement>();
                    if (move != null) move.SetRoute(route);

                    // 4) Asegurar EnemyProgress + registrarlo en ABB
                    var prog = go.GetComponent<EnemyProgress>();
                    if (prog == null) prog = go.AddComponent<EnemyProgress>();
                    EnemyPriorityABB.Instance?.Insert(prog);
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }

        finishedSpawningCurrent = true;
        Trace("Cola de la wave vacía. finishedSpawningCurrent=true");
        CheckAdvance();
    }

}
