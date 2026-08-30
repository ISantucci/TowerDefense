using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador de un nivel. Vive en la escena del nivel (objeto "LevelController") con todo cableado
/// en el Inspector: el LevelDefinition, los caminos (EnemyGraphPath), la raíz de spots y la cámara.
/// No construye nada: el mapa, los spots y el HUD ya están en la jerarquía. Sólo conecta sistemas
/// (spawner, fábricas, economía) y atiende la construcción por spots.
/// </summary>
[DefaultExecutionOrder(-50)]
public class LevelController : MonoBehaviour
{
    public static LevelController Current { get; private set; }

    [Header("Nivel")]
    [Tooltip("Datos del nivel: oleadas, roster, economía, tema. Editable como asset.")]
    public LevelDefinition level;

    [Header("Caminos (en orden: el índice es el pathIndex de las oleadas)")]
    public EnemyGraphPath[] paths;

    [Header("Spots de construcción")]
    [Tooltip("Raíz que contiene los BuildSpot de la escena.")]
    public Transform spotsRoot;

    [Header("Cámara")]
    [Tooltip("Si está activo, al arrancar reencuadra la cámara para que entre todo el mapa. Apagado = se usa la cámara tal cual está en la escena.")]
    public bool autoFitCamera = false;
    [Tooltip("Si está activo, aplica cielo y niebla del tema del nivel a la cámara/RenderSettings.")]
    public bool applyThemeToCamera = false;

    [Header("Debug")]
    [SerializeField] bool logSetup = true;

    // ── resueltos en runtime ──
    public TowerFactoryTD TowerFactory { get; private set; }
    public EnemyFactoryTD EnemyFactory { get; private set; }
    public WaveSpawnerTD Spawner { get; private set; }
    public TowerPlacer Placer { get; private set; }
    public BuildGrid Grid { get; private set; }
    public Camera Cam { get; private set; }

    public LevelDefinition Level => level;
    public bool UsesSpots => level != null && level.buildMode == BuildMode.Spots;
    public bool IsFinished { get; private set; }

    /// <summary>Roster efectivo: los TowerId del nivel que existen en el catálogo (orden del nivel).</summary>
    public List<TowerData> Roster { get; private set; } = new List<TowerData>();

    readonly List<BuildSpot> spots = new List<BuildSpot>();
    readonly Dictionary<Vector2Int, BuildSpot> spotByCell = new Dictionary<Vector2Int, BuildSpot>();
    readonly List<IReadOnlyList<Transform>> routes = new List<IReadOnlyList<Transform>>();
    readonly List<Transform> spawnPoints = new List<Transform>();

    BuildSpot hoveredSpot;
    bool highlightActive;

    public IReadOnlyList<BuildSpot> Spots => spots;

    void Awake()
    {
        Current = this;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
        GameEvents.LevelWon -= OnLevelWon;
        GameEvents.LevelLost -= OnLevelLost;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    void Start()
    {
        Setup();
    }

    // ───────────────────────── setup ─────────────────────────

    void Setup()
    {
        if (level == null)
        {
            Debug.LogError("[LevelController] Falta el LevelDefinition en el Inspector.", this);
            return;
        }
        LevelCatalog.Selected = level;

        Cam = Camera.main;
        TowerFactory = SceneObjects.FindInActiveScene<TowerFactoryTD>();
        EnemyFactory = SceneObjects.FindInActiveScene<EnemyFactoryTD>();
        Spawner = SceneObjects.FindInActiveScene<WaveSpawnerTD>();
        Placer = SceneObjects.FindPreferPersistent<TowerPlacer>();
        Grid = SceneObjects.FindPreferPersistent<BuildGrid>();

        CollectRoutes();
        CollectSpots();

        if (applyThemeToCamera) ApplyTheme();
        if (autoFitCamera) FitCamera();

        SetupRoster();
        EnemyPriorityABB.Instance?.Clear();   // el ABB es persistente: nodos muertos del nivel anterior
        SetupEnemies();
        SetupWaves();
        SetupEconomy();

        GameEvents.LevelWon += OnLevelWon;
        GameEvents.LevelLost += OnLevelLost;

        LevelEvents.RaiseLevelStarted(level);
        if (logSetup)
            Debug.Log("[LevelController] " + level.displayName + ": " + routes.Count + " caminos, " + spots.Count +
                      " spots, " + level.WaveCount() + " oleadas, roster " + Roster.Count + " torres.", this);
    }

    void CollectRoutes()
    {
        routes.Clear();
        spawnPoints.Clear();
        if (paths == null) return;
        foreach (var p in paths)
        {
            if (p == null) continue;
            var r = p.ComputeAndGetPath();
            if (r == null || r.Count == 0)
            {
                Debug.LogWarning("[LevelController] El camino " + p.name + " no tiene waypoints.", p);
                continue;
            }
            routes.Add(r);
            spawnPoints.Add(r[0]);
        }
        if (routes.Count == 0)
            Debug.LogError("[LevelController] Ningún camino válido: asigná los EnemyGraphPath en 'paths'.", this);
    }

    void CollectSpots()
    {
        spots.Clear();
        spotByCell.Clear();
        Transform root = spotsRoot != null ? spotsRoot : transform;
        var found = root.GetComponentsInChildren<BuildSpot>(true);
        foreach (var s in found)
        {
            if (s == null) continue;
            s.RefreshFromTransform(level);
            spots.Add(s);
            spotByCell[s.cell] = s;
        }
    }

    void ApplyTheme()
    {
        if (Cam == null) return;
        Cam.backgroundColor = level.theme.sky;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        RenderSettings.fog = true;
        RenderSettings.fogColor = level.theme.fog;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 28f;
        RenderSettings.fogEndDistance = 70f;
    }

    /// <summary>Reencuadra la cámara para que entre todo el mapa dejando lugar al HUD (opcional).</summary>
    public void FitCamera()
    {
        if (Cam == null || level == null) return;
        Vector3 min = level.CellToWorld(new Vector2Int(0, 0)) - new Vector3(0.5f, 0f, 0.5f) * level.cellSize;
        Vector3 max = level.CellToWorld(new Vector2Int(level.width - 1, level.height - 1)) + new Vector3(0.5f, 0f, 0.5f) * level.cellSize;
        Bounds b = new Bounds((min + max) * 0.5f, max - min + Vector3.up * 2f);

        float pitch = level.cameraPitch;
        Vector3 dir = Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;
        Vector3 center = b.center;
        float dist = Mathf.Max(b.size.x, b.size.z) * level.cameraDistanceFactor;

        Vector3[] corners =
        {
            new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.max.x, 0f, b.min.z),
            new Vector3(b.min.x, 0f, b.max.z), new Vector3(b.max.x, 0f, b.max.z),
            new Vector3(center.x, 3f, b.min.z), new Vector3(center.x, 3f, b.max.z)
        };
        const float xMin = 0.03f, xMax = 0.97f, yMin = 0.20f, yMax = 0.91f;

        for (int it = 0; it < 40; it++)
        {
            Cam.transform.position = center - dir * dist;
            Cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            bool inside = true;
            foreach (var c in corners)
            {
                Vector3 v = Cam.WorldToViewportPoint(c);
                if (v.z <= 0f || v.x < xMin || v.x > xMax || v.y < yMin || v.y > yMax) { inside = false; break; }
            }
            if (inside) dist *= 0.97f;
            else
            {
                dist /= 0.97f;
                Cam.transform.position = center - dir * dist;
                Cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                break;
            }
        }
    }

    void SetupRoster()
    {
        Roster.Clear();
        if (TowerFactory == null) { Debug.LogError("[LevelController] No hay TowerFactoryTD en la escena.", this); return; }
        foreach (var id in level.roster)
        {
            var d = TowerFactory.GetData(id);
            if (d == null) { Debug.LogWarning("[LevelController] Roster: no hay TowerData para " + id, this); continue; }
            if (!Roster.Contains(d)) Roster.Add(d);
        }
    }

    void SetupEnemies()
    {
        if (EnemyFactory == null) { Debug.LogError("[LevelController] No hay EnemyFactoryTD en la escena.", this); return; }
        EnemyFactory.SetRouteProvider(GetRoute);
    }

    void SetupWaves()
    {
        if (Spawner == null) { Debug.LogError("[LevelController] No hay WaveSpawnerTD en la escena.", this); return; }
        Spawner.Configure(level, this);
    }

    void SetupEconomy()
    {
        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.SetStats, level.startMoney, level.startLives, 0));
        if (Grid != null) Grid.spotMode = UsesSpots;
        var gm = SceneObjects.FindPreferPersistent<GameManager>();
        if (gm != null) gm.SetSellRefundPercent(level.sellRefund);
        var invoker = SceneObjects.FindPreferPersistent<BuildInvoker>();
        if (invoker != null) invoker.ClearHistory();
        if (Placer != null && Placer.HasSelection) Placer.CancelSelection();
    }

    // ───────────────────────── consultas ─────────────────────────

    public IReadOnlyList<Transform> GetRoute(int pathIndex)
    {
        if (routes.Count == 0) return null;
        if (pathIndex < 0 || pathIndex >= routes.Count) pathIndex = 0;
        return routes[pathIndex];
    }

    public Transform GetSpawnPoint(int pathIndex)
    {
        if (spawnPoints.Count == 0) return null;
        if (pathIndex < 0 || pathIndex >= spawnPoints.Count) pathIndex = 0;
        return spawnPoints[pathIndex];
    }

    public BuildSpot SpotAt(Vector3 worldPos)
    {
        if (level == null) return null;
        var cell = level.WorldToCell(worldPos);
        BuildSpot s;
        return spotByCell.TryGetValue(cell, out s) ? s : null;
    }

    /// <summary>True si en esa posición hay un spot libre (modo Spots) o si no aplica el modo.</summary>
    public bool IsSpotFree(Vector3 worldPos)
    {
        if (!UsesSpots) return true;
        var s = SpotAt(worldPos);
        return s != null && !s.IsOccupied;
    }

    public int FreeSpotCount()
    {
        int n = 0;
        foreach (var s in spots) if (s != null && !s.IsOccupied) n++;
        return n;
    }

    public BuildSpot HoveredSpot => hoveredSpot;

    // ───────────────────────── highlight de spots ─────────────────────────

    void Update()
    {
        UpdateSpotHighlight();
    }

    void UpdateSpotHighlight()
    {
        if (!UsesSpots || spots.Count == 0) return;

        bool selecting = Placer != null && Placer.HasSelection;
        if (selecting != highlightActive)
        {
            highlightActive = selecting;
            foreach (var s in spots) if (s != null) s.SetHighlight(selecting ? 1 : 0);
            if (!selecting) hoveredSpot = null;
        }
        if (!selecting) return;

        BuildSpot newHover = null;
        if (Cam != null)
        {
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            int mask = 1 << GameLayers.Ground;
            if (Physics.Raycast(ray, out hit, 300f, mask, QueryTriggerInteraction.Ignore))
                newHover = SpotAt(hit.point);
        }

        if (newHover != hoveredSpot)
        {
            if (hoveredSpot != null) hoveredSpot.SetHighlight(1);
            hoveredSpot = newHover;
            if (hoveredSpot != null) hoveredSpot.SetHighlight(2);
        }
        else if (hoveredSpot != null)
        {
            hoveredSpot.SetHighlight(2);
        }
    }

    // ───────────────────────── cierre ─────────────────────────

    void OnLevelWon()
    {
        if (IsFinished) return;
        IsFinished = true;
        int lives = GameManager.I != null ? GameManager.I.Lives : 0;
        LevelCatalog.MarkWon(level, lives);
        LevelEvents.RaiseLevelFinished(level, true);
    }

    void OnLevelLost()
    {
        if (IsFinished) return;
        IsFinished = true;
        LevelEvents.RaiseLevelFinished(level, false);
    }

    // ───────────────────────── velocidad / pausa ─────────────────────────

    public bool IsPaused { get; private set; }
    public float GameSpeed { get; private set; } = 1f;

    public void SetGameSpeed(float speed)
    {
        GameSpeed = Mathf.Clamp(speed, 0.25f, 4f);
        if (!IsPaused) Time.timeScale = GameSpeed;
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : GameSpeed;
    }

    public void TogglePause() => SetPaused(!IsPaused);
}

/// <summary>Layers del proyecto (TagManager): se nombran acá para no repetir números mágicos.</summary>
public static class GameLayers
{
    public const int Ground = 3;
    public const int Enemy = 6;
    public const int Projectile = 8;
    public const int Towers = 9;
    public const int Obstacles = 10;
    public const int EnemyPath = 11;
}
