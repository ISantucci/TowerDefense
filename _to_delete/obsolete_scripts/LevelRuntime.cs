using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador de un nivel dentro de la escena genérica de juego.
/// Lo crea LevelBootstrap al cargar la escena; construye el mapa (MapBuilder), reconfigura
/// spawner/fábricas/economía y expone consultas para construcción por spots.
/// </summary>
public class LevelRuntime : MonoBehaviour
{
    public static LevelRuntime Current { get; private set; }

    public LevelDefinition Level { get; private set; }
    public MapBuilder.Result Map { get; private set; }
    public MapBuilder Builder { get; private set; }
    public TowerFactoryTD TowerFactory { get; private set; }
    public EnemyFactoryTD EnemyFactory { get; private set; }
    public WaveSpawnerTD Spawner { get; private set; }
    public TowerPlacer Placer { get; private set; }
    public BuildGrid Grid { get; private set; }
    public Camera Cam { get; private set; }

    public bool UsesSpots => Level != null && Level.buildMode == BuildMode.Spots;
    public bool IsFinished { get; private set; }

    /// <summary>Roster efectivo: los TowerId del nivel que existen en el catálogo (orden del nivel).</summary>
    public List<TowerData> Roster { get; private set; } = new List<TowerData>();

    static readonly string[] LegacyRootsToHide = { "Ground", "EnemyPath", "Waypoints" };

    BuildSpot hoveredSpot;
    bool highlightActive;
    float baseTimeScale = 1f;

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

    // ───────────────────────── setup ─────────────────────────

    public void Setup(LevelDefinition def)
    {
        Level = def;
        if (Level == null)
        {
            Debug.LogError("[LevelRuntime] Sin LevelDefinition: la escena queda como estaba.");
            return;
        }

        Cam = Camera.main;
        // Objetos de la escena de juego (se recrean con cada carga)
        TowerFactory = SceneObjects.FindInActiveScene<TowerFactoryTD>();
        EnemyFactory = SceneObjects.FindInActiveScene<EnemyFactoryTD>();
        Spawner = SceneObjects.FindInActiveScene<WaveSpawnerTD>();
        // Objetos de _Managers (DontDestroyOnLoad): el duplicado de la recarga muere al final del frame
        Placer = SceneObjects.FindPreferPersistent<TowerPlacer>();
        Grid = SceneObjects.FindPreferPersistent<BuildGrid>();

        HideLegacyMap();

        Builder = new MapBuilder(Level);
        Map = Builder.Build(transform);

        SetupCamera();
        SetupRoster();
        EnemyPriorityABB.Instance?.Clear();   // el ABB es persistente: nodos muertos del nivel anterior
        SetupEnemies();
        SetupWaves();
        SetupEconomy();

        GameEvents.LevelWon += OnLevelWon;
        GameEvents.LevelLost += OnLevelLost;

        LevelEvents.RaiseLevelStarted(Level);
        Debug.Log("[LevelRuntime] Nivel armado: " + Level.displayName + " (" + Level.width + "x" + Level.height + ", " +
                  Map.spots.Count + " spots, " + Level.WaveCount() + " oleadas)");
    }

    void HideLegacyMap()
    {
        var roots = gameObject.scene.GetRootGameObjects();
        foreach (var go in roots)
        {
            foreach (var n in LegacyRootsToHide)
                if (go.name == n) go.SetActive(false);
        }
        // Las cajas de waypoint del prefab original quedan visibles si no se desactiva su raíz; por las dudas,
        // apagamos cualquier WaypointsPath activo que no sea nuestro.
        var legacyPaths = FindObjectsByType<WaypointsPath>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var wp in legacyPaths)
            if (wp.transform.root != transform.root) wp.gameObject.SetActive(false);
    }

    void SetupCamera()
    {
        if (Cam == null) return;
        Cam.backgroundColor = Level.theme.sky;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        RenderSettings.fog = true;
        RenderSettings.fogColor = Level.theme.fog;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 28f;
        RenderSettings.fogEndDistance = 70f;
        FitCamera();
    }

    /// <summary>Ubica la cámara para que todo el mapa entre en el viewport útil (dejando lugar al HUD).</summary>
    public void FitCamera()
    {
        if (Cam == null || Map == null) return;
        Bounds b = Map.bounds;
        float pitch = Level.cameraPitch;
        Vector3 dir = Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward; // apunta hacia abajo/adelante
        Vector3 center = b.center;
        float dist = Mathf.Max(b.size.x, b.size.z) * Level.cameraDistanceFactor;

        Vector3[] corners =
        {
            new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.max.x, 0f, b.min.z),
            new Vector3(b.min.x, 0f, b.max.z), new Vector3(b.max.x, 0f, b.max.z),
            new Vector3(center.x, 3f, b.min.z), new Vector3(center.x, 3f, b.max.z)
        };

        // Rect útil del viewport: el HUD ocupa arriba (~9%) y abajo (~19%).
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

            if (inside)
            {
                // achicar hasta que deje de entrar, después volver un paso
                dist *= 0.97f;
            }
            else
            {
                dist /= 0.97f;
                Cam.transform.position = center - dir * dist;
                Cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                break;
            }
        }

        // Centrar verticalmente dentro del rect útil: desplazar el punto de mira un poco hacia el HUD superior.
        Vector3 bottom = Cam.WorldToViewportPoint(new Vector3(center.x, 0f, b.min.z));
        Vector3 topV = Cam.WorldToViewportPoint(new Vector3(center.x, 0f, b.max.z));
        float midY = (bottom.y + topV.y) * 0.5f;
        float wantY = (yMin + yMax) * 0.5f;
        float deltaY = wantY - midY;
        if (Mathf.Abs(deltaY) > 0.005f)
        {
            // mover el centro de mira en Z del mundo hasta compensar (aprox. lineal)
            float worldPerViewport = b.size.z / Mathf.Max(0.05f, (topV.y - bottom.y));
            center.z -= deltaY * worldPerViewport;
            Cam.transform.position = center - dir * dist;
        }
    }

    void SetupRoster()
    {
        Roster.Clear();
        if (TowerFactory == null) return;
        foreach (var id in Level.roster)
        {
            var d = TowerFactory.GetData(id);
            if (d == null) { Debug.LogWarning("[LevelRuntime] Roster: no hay TowerData para " + id); continue; }
            if (!Roster.Contains(d)) Roster.Add(d);
        }
        if (Roster.Count == 0)
        {
            // fallback: lo que haya en escena
            foreach (var d in TowerFactory.Catalog)
                if (d != null && (d.id == TowerId.Archer || d.id == TowerId.Bomber)) Roster.Add(d);
        }
    }

    void SetupEnemies()
    {
        if (EnemyFactory == null) { Debug.LogError("[LevelRuntime] No hay EnemyFactoryTD en la escena."); return; }
        EnemyFactory.SetRouteProvider(GetRoute);
    }

    void SetupWaves()
    {
        if (Spawner == null) { Debug.LogError("[LevelRuntime] No hay WaveSpawnerTD en la escena."); return; }
        Spawner.Configure(Level, this);
    }

    void SetupEconomy()
    {
        // GameManager es persistente entre escenas: se resetea por evento (no toca su serialización).
        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.SetStats, Level.startMoney, Level.startLives, 0));
        if (Grid != null) Grid.spotMode = UsesSpots;
        var gm = SceneObjects.FindPreferPersistent<GameManager>();
        if (gm != null) gm.SetSellRefundPercent(Level.sellRefund);
        // Sin historial de undo entre niveles
        var invoker = SceneObjects.FindPreferPersistent<BuildInvoker>();
        if (invoker != null) invoker.ClearHistory();
        if (Placer != null && Placer.HasSelection) Placer.CancelSelection();
    }

    // ───────────────────────── consultas ─────────────────────────

    public IReadOnlyList<Transform> GetRoute(int pathIndex)
    {
        if (Map == null || Map.routes.Count == 0) return null;
        if (pathIndex < 0 || pathIndex >= Map.routes.Count) pathIndex = 0;
        return Map.routes[pathIndex];
    }

    public Transform GetSpawnPoint(int pathIndex)
    {
        if (Map == null || Map.spawnPoints.Count == 0) return null;
        if (pathIndex < 0 || pathIndex >= Map.spawnPoints.Count) pathIndex = 0;
        return Map.spawnPoints[pathIndex];
    }

    public BuildSpot SpotAt(Vector3 worldPos)
    {
        if (Map == null) return null;
        var cell = Level.WorldToCell(worldPos);
        BuildSpot s;
        return Map.spotByCell.TryGetValue(cell, out s) ? s : null;
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
        if (Map == null) return 0;
        int n = 0;
        foreach (var s in Map.spots) if (s != null && !s.IsOccupied) n++;
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
        if (Map == null || !UsesSpots) return;

        bool selecting = Placer != null && Placer.HasSelection;
        if (selecting != highlightActive)
        {
            highlightActive = selecting;
            foreach (var s in Map.spots) if (s != null) s.SetHighlight(selecting ? 1 : 0);
            if (!selecting) hoveredSpot = null;
        }
        if (!selecting) return;

        BuildSpot newHover = null;
        if (Cam != null)
        {
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            int mask = 1 << MapBuilder.LayerGround;
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
            hoveredSpot.SetHighlight(2); // refresca ocupación
        }
    }

    // ───────────────────────── cierre ─────────────────────────

    void OnLevelWon()
    {
        if (IsFinished) return;
        IsFinished = true;
        int lives = GameManager.I != null ? GameManager.I.Lives : 0;
        LevelCatalog.MarkWon(Level, lives);
        LevelEvents.RaiseLevelFinished(Level, true);
    }

    void OnLevelLost()
    {
        if (IsFinished) return;
        IsFinished = true;
        LevelEvents.RaiseLevelFinished(Level, false);
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
