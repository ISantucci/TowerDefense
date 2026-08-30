using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye el mapa de un LevelDefinition con primitivas y materiales generados en runtime.
/// No conoce las oleadas ni la economía: sólo geometría, decoración, spots y waypoints.
/// </summary>
public class MapBuilder
{
    public const int LayerGround = 3;      // "Ground"    → raycast de TowerPlacer
    public const int LayerObstacles = 10;  // "obstacles" → bloquea construcción libre
    public const int LayerEnemyPath = 11;  // "EnemyPath" → bloquea construcción libre

    public class Result
    {
        public GameObject root;
        public List<List<Transform>> routes = new List<List<Transform>>();
        public List<Transform> spawnPoints = new List<Transform>();
        public List<BuildSpot> spots = new List<BuildSpot>();
        public Dictionary<Vector2Int, BuildSpot> spotByCell = new Dictionary<Vector2Int, BuildSpot>();
        public HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
        public Bounds bounds;
        public Vector3 basePosition;
    }

    readonly LevelDefinition level;
    readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    readonly Dictionary<Color, Material> emissive = new Dictionary<Color, Material>();
    System.Random rng;
    Shader standard;

    public MapBuilder(LevelDefinition level)
    {
        this.level = level;
    }

    // ───────────────────────── materiales ─────────────────────────

    public Material Mat(Color c)
    {
        Material m;
        if (materials.TryGetValue(c, out m)) return m;
        if (standard == null) standard = Shader.Find("Standard");
        m = standard != null ? new Material(standard) : new Material(Shader.Find("Diffuse"));
        m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        materials[c] = m;
        return m;
    }

    public Material EmissiveMat(Color c, float intensity = 1.2f)
    {
        Material m;
        if (emissive.TryGetValue(c, out m)) return m;
        m = new Material(Mat(c));
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
        emissive[c] = m;
        return m;
    }

    // ───────────────────────── build ─────────────────────────

    public Result Build(Transform parent)
    {
        var res = new Result();
        rng = new System.Random(level.decorSeed);

        res.root = new GameObject("Map_" + level.levelId);
        if (parent != null) res.root.transform.SetParent(parent, false);

        var tilesRoot = Child(res.root, "Tiles");
        var decorRoot = Child(res.root, "Decor");
        var spotsRoot = Child(res.root, "Spots");
        var pathRoot = Child(res.root, "Paths");

        // 1) Celdas de camino y decoración declarada
        res.pathCells = level.AllPathCells();
        var decorMap = new Dictionary<Vector2Int, CellKind>();
        foreach (var d in level.decor)
            if (level.InBounds(d.cell) && !res.pathCells.Contains(d.cell)) decorMap[d.cell] = d.kind;

        var spotCells = level.ComputeSpots();
        var spotSet = new HashSet<Vector2Int>(spotCells);

        // 2) Decoración procedural (lejos del camino y de los spots)
        if (level.decorSeed != 0 && level.decorDensity > 0f)
            ScatterDecor(res.pathCells, spotSet, decorMap);

        // 3) Tiles
        for (int y = 0; y < level.height; y++)
        {
            for (int x = 0; x < level.width; x++)
            {
                var c = new Vector2Int(x, y);
                Vector3 w = level.CellToWorld(c);
                CellKind kind;
                bool isPath = res.pathCells.Contains(c);
                if (isPath) kind = CellKind.Path;
                else if (!decorMap.TryGetValue(c, out kind)) kind = CellKind.Grass;
                BuildTile(tilesRoot.transform, decorRoot.transform, c, w, kind);
            }
        }

        // 4) Spots
        foreach (var c in spotCells)
        {
            var spot = BuildSpotMarker(spotsRoot.transform, c);
            res.spots.Add(spot);
            res.spotByCell[c] = spot;
        }

        // 5) Waypoints, portales y base
        for (int i = 0; i < level.paths.Count; i++)
        {
            var p = level.paths[i];
            if (p.waypoints.Count == 0) continue;

            var routeRoot = Child(pathRoot, "Route_" + i);
            var route = new List<Transform>();
            for (int k = 0; k < p.waypoints.Count; k++)
            {
                var wp = new GameObject("WP_" + i + "_" + k);
                wp.transform.SetParent(routeRoot.transform, false);
                Vector3 w = level.CellToWorld(p.waypoints[k]);
                wp.transform.position = new Vector3(w.x, 0.5f, w.z);
                route.Add(wp.transform);
            }
            res.routes.Add(route);
            res.spawnPoints.Add(route[0]);

            BuildPortal(decorRoot.transform, level.CellToWorld(p.waypoints[0]), PathDirection(p, true));

            if (i == 0)
            {
                res.basePosition = level.CellToWorld(p.waypoints[p.waypoints.Count - 1]);
                BuildBase(decorRoot.transform, res.basePosition);
            }
            else
            {
                // Si otro camino termina en otra celda, también se marca con una base chica.
                Vector3 end = level.CellToWorld(p.waypoints[p.waypoints.Count - 1]);
                if ((end - res.basePosition).sqrMagnitude > 0.5f) BuildBase(decorRoot.transform, end);
            }
        }

        // 6) Bounds del mapa
        Vector3 min = level.CellToWorld(new Vector2Int(0, 0)) - new Vector3(0.5f, 0f, 0.5f) * level.cellSize;
        Vector3 max = level.CellToWorld(new Vector2Int(level.width - 1, level.height - 1)) + new Vector3(0.5f, 0f, 0.5f) * level.cellSize;
        res.bounds = new Bounds((min + max) * 0.5f, max - min + Vector3.up * 2f);

        return res;
    }

    // ───────────────────────── tiles ─────────────────────────

    void BuildTile(Transform tilesParent, Transform decorParent, Vector2Int c, Vector3 w, CellKind kind)
    {
        var t = level.theme;
        float noise = kind == CellKind.Path || kind == CellKind.Water ? 0f
            : (float)(rng.NextDouble() * 2.0 - 1.0) * t.groundHeightNoise;
        bool alt = ((c.x + c.y) & 1) == 0;

        Color color;
        float top;
        int layer = LayerGround;

        switch (kind)
        {
            case CellKind.Path:
                color = alt ? t.path : Color.Lerp(t.path, t.pathEdge, 0.35f);
                top = -0.03f;
                layer = LayerEnemyPath;
                break;
            case CellKind.Water:
                color = alt ? t.water : Color.Lerp(t.water, Color.white, 0.08f);
                top = -0.28f;
                layer = LayerObstacles;
                break;
            case CellKind.Sand:
                color = alt ? new Color(0.90f, 0.82f, 0.58f) : new Color(0.86f, 0.78f, 0.54f);
                top = noise * 0.5f;
                break;
            case CellKind.Snow:
                color = alt ? new Color(0.93f, 0.95f, 0.98f) : new Color(0.86f, 0.90f, 0.95f);
                top = noise;
                break;
            case CellKind.Lava:
                color = alt ? new Color(0.95f, 0.35f, 0.08f) : new Color(1.0f, 0.55f, 0.12f);
                top = -0.2f;
                layer = LayerObstacles;
                break;
            default:
                color = alt ? t.ground : t.groundAlt;
                top = noise;
                break;
        }

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "T_" + c.x + "_" + c.y;
        cube.layer = layer;
        cube.transform.SetParent(tilesParent, false);
        float h = 1f;
        cube.transform.position = new Vector3(w.x, top - h * 0.5f, w.z);
        cube.transform.localScale = new Vector3(level.cellSize * 0.98f, h, level.cellSize * 0.98f);
        var mr = cube.GetComponent<MeshRenderer>();
        mr.sharedMaterial = kind == CellKind.Lava ? EmissiveMat(color, 0.9f) : Mat(color);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // El camino y las celdas especiales necesitan que el raycast del placer NO las tome como suelo:
        // por eso van en su propia layer. Las decoraciones se apoyan sobre el tile.
        switch (kind)
        {
            case CellKind.Tree: BuildTree(decorParent, w, top); break;
            case CellKind.Rock: BuildRock(decorParent, w, top); break;
            case CellKind.Bush: BuildBush(decorParent, w, top); break;
            case CellKind.Flower: BuildFlowers(decorParent, w, top); break;
            case CellKind.Crystal: BuildCrystal(decorParent, w, top); break;
        }
    }

    void ScatterDecor(HashSet<Vector2Int> pathCells, HashSet<Vector2Int> spotSet, Dictionary<Vector2Int, CellKind> decorMap)
    {
        for (int y = 0; y < level.height; y++)
        {
            for (int x = 0; x < level.width; x++)
            {
                var c = new Vector2Int(x, y);
                if (pathCells.Contains(c) || spotSet.Contains(c) || decorMap.ContainsKey(c)) continue;

                int d = DistanceToPath(c, pathCells);
                if (d <= 1) continue;   // dejar respirar el camino

                float chance = level.decorDensity * (d >= 3 ? 1.6f : 0.6f);
                if (rng.NextDouble() >= chance) continue;

                double r = rng.NextDouble();
                CellKind k = r < 0.45 ? CellKind.Tree : r < 0.65 ? CellKind.Bush : r < 0.85 ? CellKind.Flower : CellKind.Rock;
                decorMap[c] = k;
            }
        }
    }

    static int DistanceToPath(Vector2Int c, HashSet<Vector2Int> pathCells)
    {
        int best = int.MaxValue;
        foreach (var p in pathCells)
        {
            int d = Mathf.Max(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y));
            if (d < best) best = d;
        }
        return best;
    }

    // ───────────────────────── decoración ─────────────────────────

    GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, int layer, string name)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        return go;
    }

    float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    void BuildTree(Transform parent, Vector3 w, float top)
    {
        var t = level.theme;
        float s = R(0.8f, 1.25f);
        float trunkH = 0.55f * s;
        Prim(PrimitiveType.Cylinder, parent, new Vector3(w.x, top + trunkH * 0.5f, w.z),
             new Vector3(0.18f * s, trunkH * 0.5f, 0.18f * s), Mat(t.trunk), LayerObstacles, "Tree");
        var crown = Prim(PrimitiveType.Sphere, parent, new Vector3(w.x, top + trunkH + 0.32f * s, w.z),
                         new Vector3(0.62f * s, 0.62f * s, 0.62f * s), Mat(rng.NextDouble() < 0.5 ? t.foliage : t.foliageAlt), LayerObstacles, "Crown");
        if (rng.NextDouble() < 0.6)
        {
            var crown2 = Prim(PrimitiveType.Sphere, parent, new Vector3(w.x + R(-0.15f, 0.15f), top + trunkH + 0.62f * s, w.z + R(-0.15f, 0.15f)),
                              new Vector3(0.42f * s, 0.42f * s, 0.42f * s), Mat(t.foliageAlt), LayerObstacles, "Crown2");
            Object.Destroy(crown2.GetComponent<Collider>());
        }
        Object.Destroy(crown.GetComponent<Collider>());
    }

    void BuildRock(Transform parent, Vector3 w, float top)
    {
        var t = level.theme;
        int n = rng.NextDouble() < 0.5 ? 1 : 2;
        for (int i = 0; i < n; i++)
        {
            float s = R(0.35f, 0.6f);
            var rock = Prim(PrimitiveType.Sphere, parent,
                            new Vector3(w.x + R(-0.25f, 0.25f), top + s * 0.3f, w.z + R(-0.25f, 0.25f)),
                            new Vector3(s * R(0.9f, 1.3f), s * 0.7f, s * R(0.9f, 1.3f)),
                            Mat(Color.Lerp(t.rock, Color.black, R(0f, 0.2f))), LayerObstacles, "Rock");
            rock.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        }
    }

    void BuildBush(Transform parent, Vector3 w, float top)
    {
        var t = level.theme;
        float s = R(0.35f, 0.55f);
        var b = Prim(PrimitiveType.Sphere, parent, new Vector3(w.x, top + s * 0.35f, w.z),
                     new Vector3(s * 1.4f, s, s * 1.4f), Mat(t.foliageAlt), LayerObstacles, "Bush");
        Object.Destroy(b.GetComponent<Collider>());
    }

    void BuildFlowers(Transform parent, Vector3 w, float top)
    {
        var t = level.theme;
        int n = 3 + rng.Next(3);
        for (int i = 0; i < n; i++)
        {
            Color c = i % 2 == 0 ? t.flower : Color.Lerp(t.flower, Color.white, 0.5f);
            var f = Prim(PrimitiveType.Cube, parent, new Vector3(w.x + R(-0.35f, 0.35f), top + 0.1f, w.z + R(-0.35f, 0.35f)),
                         new Vector3(0.12f, 0.2f, 0.12f), Mat(c), 0, "Flower");
            Object.Destroy(f.GetComponent<Collider>());
        }
    }

    void BuildCrystal(Transform parent, Vector3 w, float top)
    {
        var t = level.theme;
        var c = Prim(PrimitiveType.Cube, parent, new Vector3(w.x, top + 0.45f, w.z),
                     new Vector3(0.35f, 0.9f, 0.35f), EmissiveMat(t.portal, 0.8f), LayerObstacles, "Crystal");
        c.transform.rotation = Quaternion.Euler(R(-10f, 10f), 45f, R(-10f, 10f));
    }

    BuildSpot BuildSpotMarker(Transform parent, Vector2Int cell)
    {
        var t = level.theme;
        Vector3 w = level.CellToWorld(cell);
        var go = new GameObject("Spot_" + cell.x + "_" + cell.y);
        go.transform.SetParent(parent, false);
        go.transform.position = w;

        var pad = Prim(PrimitiveType.Cylinder, go.transform, new Vector3(w.x, 0.02f, w.z),
                       new Vector3(0.82f, 0.04f, 0.82f), Mat(t.spot), 0, "Pad");
        Object.Destroy(pad.GetComponent<Collider>());

        var ring = Prim(PrimitiveType.Cylinder, go.transform, new Vector3(w.x, 0.05f, w.z),
                        new Vector3(0.95f, 0.03f, 0.95f), EmissiveMat(t.spotRing, 1.5f), 0, "Ring");
        Object.Destroy(ring.GetComponent<Collider>());
        ring.SetActive(false);

        var spot = go.AddComponent<BuildSpot>();
        spot.Init(cell, w, pad.GetComponent<MeshRenderer>(), ring, Mat(t.spot), EmissiveMat(Color.Lerp(t.spot, t.spotRing, 0.5f), 0.6f));
        return spot;
    }

    Vector3 PathDirection(PathDef p, bool atStart)
    {
        if (p.waypoints.Count < 2) return Vector3.forward;
        Vector2Int a = atStart ? p.waypoints[0] : p.waypoints[p.waypoints.Count - 2];
        Vector2Int b = atStart ? p.waypoints[1] : p.waypoints[p.waypoints.Count - 1];
        Vector2Int d = b - a;
        if (d.x != 0) return new Vector3(Mathf.Sign(d.x), 0f, 0f);
        return new Vector3(0f, 0f, Mathf.Sign(d.y));
    }

    void BuildPortal(Transform parent, Vector3 w, Vector3 dir)
    {
        var t = level.theme;
        var root = new GameObject("Portal");
        root.transform.SetParent(parent, false);
        root.transform.position = w;
        root.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        Material stone = Mat(Color.Lerp(t.rock, Color.black, 0.35f));
        // dos pilares + dintel
        Prim(PrimitiveType.Cube, root.transform, w + root.transform.right * 0.55f + Vector3.up * 0.9f, new Vector3(0.25f, 1.8f, 0.25f), stone, LayerObstacles, "PillarL");
        Prim(PrimitiveType.Cube, root.transform, w - root.transform.right * 0.55f + Vector3.up * 0.9f, new Vector3(0.25f, 1.8f, 0.25f), stone, LayerObstacles, "PillarR");
        Prim(PrimitiveType.Cube, root.transform, w + Vector3.up * 1.9f, new Vector3(1.45f, 0.25f, 0.3f), stone, LayerObstacles, "Lintel");
        // plano brillante del portal
        var glow = Prim(PrimitiveType.Cube, root.transform, w + Vector3.up * 0.9f, new Vector3(0.95f, 1.7f, 0.06f), EmissiveMat(t.portal, 1.6f), 0, "Glow");
        Object.Destroy(glow.GetComponent<Collider>());
        var spin = glow.AddComponent<SimpleSpin>();
        spin.axis = Vector3.forward;
        spin.degreesPerSecond = 40f;
        spin.pulse = 0.08f;
    }

    void BuildBase(Transform parent, Vector3 w)
    {
        var t = level.theme;
        var root = new GameObject("Base");
        root.transform.SetParent(parent, false);
        root.transform.position = w;

        Material wall = Mat(t.baseWall);
        Material roof = Mat(t.baseRoof);
        Material dark = Mat(Color.Lerp(t.baseWall, Color.black, 0.45f));

        Prim(PrimitiveType.Cube, root.transform, w + Vector3.up * 0.6f, new Vector3(1.6f, 1.2f, 1.6f), wall, LayerObstacles, "Keep");
        var top = Prim(PrimitiveType.Cube, root.transform, w + Vector3.up * 1.55f, new Vector3(1.15f, 0.7f, 1.15f), roof, LayerObstacles, "Roof");
        top.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        Object.Destroy(top.GetComponent<Collider>());

        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -0.8f : 0.8f;
            float sz = (i < 2) ? -0.8f : 0.8f;
            var tower = Prim(PrimitiveType.Cylinder, root.transform, w + new Vector3(sx, 0.85f, sz), new Vector3(0.42f, 0.85f, 0.42f), wall, LayerObstacles, "Turret");
            Object.Destroy(tower.GetComponent<Collider>());
            var cap = Prim(PrimitiveType.Cylinder, root.transform, w + new Vector3(sx, 1.78f, sz), new Vector3(0.5f, 0.09f, 0.5f), dark, 0, "Cap");
            Object.Destroy(cap.GetComponent<Collider>());
        }

        // bandera
        var pole = Prim(PrimitiveType.Cylinder, root.transform, w + Vector3.up * 2.5f, new Vector3(0.05f, 0.6f, 0.05f), dark, 0, "Pole");
        Object.Destroy(pole.GetComponent<Collider>());
        var flag = Prim(PrimitiveType.Cube, root.transform, w + new Vector3(0.28f, 2.95f, 0f), new Vector3(0.5f, 0.3f, 0.04f), EmissiveMat(t.baseRoof, 0.5f), 0, "Flag");
        Object.Destroy(flag.GetComponent<Collider>());
        var wave = flag.AddComponent<SimpleSpin>();
        wave.axis = Vector3.up;
        wave.degreesPerSecond = 0f;
        wave.pulse = 0.05f;
        wave.swayDegrees = 14f;
    }

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }
}
