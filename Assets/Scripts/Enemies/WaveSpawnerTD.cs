using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Formato original de oleada (se conserva porque está serializado en la escena).</summary>
[System.Serializable]
public class WaveConfig
{
    public EnemyId enemyType;
    public int enemyCount;
    public float spawnInterval;
}

/// <summary>
/// Spawner de oleadas. Dos modos:
///  - Legacy: usa el array `waves` de la escena (comportamiento original: una oleada tras otra, sin preparación).
///  - Nivel: LevelController lo configura con las WaveDef del LevelDefinition: preparación con cuenta regresiva
///    salteable (con bonus), sub-oleadas simultáneas por distintos caminos y bonus por oleada limpia.
/// </summary>
public class WaveSpawnerTD : MonoBehaviour
{
    [SerializeField] EnemyFactoryTD enemyFactory;
    [SerializeField] Transform spawnPoint;
    [SerializeField] WaveConfig[] waves;
    [SerializeField] float startDelay = 1f;

    // ── estado ──
    List<List<WaveDef>> groups = new List<List<WaveDef>>();
    LevelController runtime;
    LevelDefinition level;
    bool configured;
    bool running;

    int enemiesAlive;
    int pendingToSpawn;
    int activeSubSpawns;
    int waveIndex;
    int totalWaves;

    bool inPrep;
    bool skipRequested;
    float prepRemaining;
    float prepTotal;

    public int WaveIndex => waveIndex;
    public int TotalWaves => totalWaves;
    public int EnemiesAlive => enemiesAlive;
    public int PendingToSpawn => pendingToSpawn;
    public bool InPrep => inPrep;
    public float PrepRemaining => prepRemaining;
    public bool IsRunning => running;

    void Awake()
    {
        GameEvents.EnemyRemoved += OnEnemyRemoved;
        GameEvents.LevelLost += OnLevelLost;
        if (enemyFactory == null) enemyFactory = FindFirstObjectByType<EnemyFactoryTD>();
    }

    void OnDestroy()
    {
        GameEvents.EnemyRemoved -= OnEnemyRemoved;
        GameEvents.LevelLost -= OnLevelLost;
    }

    void OnLevelLost()
    {
        // Derrota: se corta el bucle para que el último enemigo muerto no dispare una victoria.
        StopAllCoroutines();
        running = false;
        inPrep = false;
    }

    void Start()
    {
        if (configured) return;
        ConfigureLegacy();
    }

    // ───────────────────────── configuración ─────────────────────────

    /// <summary>Modo nivel: reemplaza las oleadas de la escena por las del LevelDefinition y arranca.</summary>
    public void Configure(LevelDefinition def, LevelController rt)
    {
        level = def;
        runtime = rt;
        groups.Clear();

        if (def != null)
        {
            for (int i = 0; i < def.waves.Count; i++)
            {
                var w = def.waves[i];
                if (w == null) continue;
                if (i == 0 || !w.joinPrevious || groups.Count == 0) groups.Add(new List<WaveDef>());
                groups[groups.Count - 1].Add(w);
            }
        }

        Begin(def != null ? def.firstWaveDelay : startDelay);
    }

    /// <summary>Modo legacy: convierte el array de la escena a grupos sin preparación.</summary>
    void ConfigureLegacy()
    {
        level = null;
        runtime = null;
        groups.Clear();
        if (waves != null)
        {
            foreach (var w in waves)
            {
                if (w == null) continue;
                var def = new WaveDef();
                def.enemyType = w.enemyType;
                def.count = w.enemyCount;
                def.spawnInterval = w.spawnInterval;
                def.prepTime = 0f;
                def.earlyCallBonus = 0;
                var g = new List<WaveDef>();
                g.Add(def);
                groups.Add(g);
            }
        }
        if (groups.Count == 0)
        {
            // Escena de nivel: las oleadas llegan por LevelController.Configure(); sin oleadas no se arranca nada.
            return;
        }
        Begin(startDelay);
    }

    void Begin(float firstDelay)
    {
        StopAllCoroutines();
        configured = true;
        running = true;
        enemiesAlive = 0;
        activeSubSpawns = 0;
        waveIndex = 0;
        totalWaves = groups.Count;
        pendingToSpawn = 0;
        foreach (var g in groups) foreach (var w in g) pendingToSpawn += Mathf.Max(0, w.count);
        inPrep = false;
        skipRequested = false;

        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.WaveChanged, 0, totalWaves));
        LevelEvents.RaiseEnemiesChanged(enemiesAlive, pendingToSpawn);
        StartCoroutine(RunLevel(firstDelay));
    }

    // ───────────────────────── bucle principal ─────────────────────────

    IEnumerator RunLevel(float firstDelay)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            float prep = i == 0 ? firstDelay : group[0].prepTime;
            int bonus = group[0].earlyCallBonus;

            LevelEvents.RaiseWavePrepStarted(i + 1, totalWaves, Summarize(group));
            yield return Prep(prep, bonus);

            waveIndex = i + 1;
            EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.WaveChanged, waveIndex, totalWaves));
            LevelEvents.RaiseWaveStarted(waveIndex, totalWaves, Summarize(group));

            foreach (var sub in group)
            {
                activeSubSpawns++;
                StartCoroutine(SpawnSub(sub));
            }

            // esperar a que termine de salir todo y muera todo
            while (activeSubSpawns > 0 || enemiesAlive > 0)
                yield return null;

            if (i < groups.Count - 1 && level != null && level.waveClearBonus > 0)
            {
                EventQueueManager.Enqueue(GameplayEvent.AddMoney(level.waveClearBonus));
                LevelEvents.RaiseWaveCleared(waveIndex, level.waveClearBonus);
            }
            else
            {
                LevelEvents.RaiseWaveCleared(waveIndex, 0);
            }
        }

        while (enemiesAlive > 0) yield return null;

        running = false;
        if (runtime != null && runtime.IsFinished) yield break;   // ya se perdió en el mismo frame
        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.LevelWon));
    }

    IEnumerator Prep(float seconds, int bonus)
    {
        prepTotal = Mathf.Max(0f, seconds);
        prepRemaining = prepTotal;
        skipRequested = false;
        inPrep = prepTotal > 0f;

        while (prepRemaining > 0f && !skipRequested)
        {
            LevelEvents.RaiseWaveCountdown(prepRemaining, prepTotal);
            yield return null;
            prepRemaining -= Time.deltaTime;
        }

        if (skipRequested && bonus > 0 && prepTotal > 0f)
        {
            int give = Mathf.RoundToInt(bonus * Mathf.Clamp01(prepRemaining / prepTotal));
            if (give > 0) EventQueueManager.Enqueue(GameplayEvent.AddMoney(give));
        }

        prepRemaining = 0f;
        inPrep = false;
        LevelEvents.RaiseWaveCountdown(0f, prepTotal);
    }

    IEnumerator SpawnSub(WaveDef sub)
    {
        for (int i = 0; i < sub.count; i++)
        {
            SpawnOne(sub);
            if (sub.spawnInterval > 0f) yield return new WaitForSeconds(sub.spawnInterval);
            else yield return null;
        }
        activeSubSpawns--;
    }

    void SpawnOne(WaveDef sub)
    {
        pendingToSpawn = Mathf.Max(0, pendingToSpawn - 1);

        if (enemyFactory == null)
        {
            Debug.LogError("[WaveSpawner] Sin EnemyFactoryTD.");
            LevelEvents.RaiseEnemiesChanged(enemiesAlive, pendingToSpawn);
            return;
        }

        Vector3 pos;
        IReadOnlyList<Transform> route = null;
        if (runtime != null)
        {
            var sp = runtime.GetSpawnPoint(sub.pathIndex);
            pos = sp != null ? sp.position : Vector3.zero;
            route = runtime.GetRoute(sub.pathIndex);
        }
        else
        {
            pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        }

        var enemy = enemyFactory.Spawn(sub.enemyType, pos, Quaternion.identity, route);
        if (enemy != null)
        {
            enemiesAlive++;
            CombatEvents.RaiseEnemySpawned(enemy);
        }
        else
        {
            Debug.LogWarning("[WaveSpawner] No se pudo spawnear " + sub.enemyType + " (¿falta EnemyData en Resources/Enemies?)");
        }
        LevelEvents.RaiseEnemiesChanged(enemiesAlive, pendingToSpawn);
    }

    void OnEnemyRemoved()
    {
        enemiesAlive--;
        if (enemiesAlive < 0) enemiesAlive = 0;
        LevelEvents.RaiseEnemiesChanged(enemiesAlive, pendingToSpawn);
    }

    // ───────────────────────── API para la UI ─────────────────────────

    /// <summary>Saltea la preparación de la oleada actual (si la hay). Devuelve true si había algo que saltear.</summary>
    public bool CallNextWaveEarly()
    {
        if (!inPrep) return false;
        skipRequested = true;
        return true;
    }

    static string Summarize(List<WaveDef> group)
    {
        var parts = new List<string>();
        foreach (var w in group)
            parts.Add(w.count + " × " + EnemyNames.Of(w.enemyType));
        return string.Join("  +  ", parts.ToArray());
    }
}
