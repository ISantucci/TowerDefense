using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tipo de celda del mapa. El camino y los spots se derivan de los datos del nivel.</summary>
public enum CellKind
{
    Grass = 0,
    Path = 1,
    Water = 2,
    Tree = 3,
    Rock = 4,
    Bush = 5,
    Flower = 6,
    Sand = 7,
    Snow = 8,
    Lava = 9,
    Crystal = 10,
    Spawn = 11,
    Base = 12
}

public enum BuildMode
{
    /// <summary>Se construye en cualquier celda libre (comportamiento original del proyecto).</summary>
    Free = 0,
    /// <summary>Sólo en los spots marcados del nivel.</summary>
    Spots = 1
}

/// <summary>Un camino: lista de waypoints en coordenadas de celda. Se rasteriza por segmentos alineados a los ejes.</summary>
[Serializable]
public class PathDef
{
    public string name = "Camino";
    public List<Vector2Int> waypoints = new List<Vector2Int>();
}

/// <summary>Celda decorativa/terreno especial (agua, árbol, roca, arena...).</summary>
[Serializable]
public class CellDecor
{
    public Vector2Int cell;
    public CellKind kind = CellKind.Tree;
}

/// <summary>Oleada: qué enemigo, cuántos, cada cuánto, por qué camino y cuánto esperar antes.</summary>
[Serializable]
public class WaveDef
{
    public EnemyId enemyType = EnemyId.Goblin;
    public int count = 5;
    public float spawnInterval = 1.2f;
    [Tooltip("Índice del camino por el que entra esta oleada (0 = primero).")]
    public int pathIndex = 0;
    [Tooltip("Segundos de preparación antes de que arranque la oleada (se puede saltear con 'Siguiente oleada').")]
    public float prepTime = 8f;
    [Tooltip("Oro extra por saltear la preparación (proporcional al tiempo restante).")]
    public int earlyCallBonus = 15;
    [Tooltip("Varias sub-oleadas con el mismo número de oleada: si es true, esta entrada arranca junto con la anterior.")]
    public bool joinPrevious = false;
}

/// <summary>Paleta del nivel. Todo se materializa en runtime con el shader Standard.</summary>
[Serializable]
public class LevelTheme
{
    public Color ground = new Color(0.35f, 0.62f, 0.28f);
    public Color groundAlt = new Color(0.31f, 0.56f, 0.25f);
    public Color path = new Color(0.62f, 0.55f, 0.42f);
    public Color pathEdge = new Color(0.48f, 0.42f, 0.32f);
    public Color water = new Color(0.20f, 0.45f, 0.85f);
    public Color spot = new Color(0.33f, 0.30f, 0.28f);
    public Color spotRing = new Color(1.0f, 0.85f, 0.25f);
    public Color trunk = new Color(0.40f, 0.26f, 0.13f);
    public Color foliage = new Color(0.16f, 0.45f, 0.18f);
    public Color foliageAlt = new Color(0.22f, 0.55f, 0.20f);
    public Color rock = new Color(0.55f, 0.55f, 0.58f);
    public Color flower = new Color(0.95f, 0.35f, 0.45f);
    public Color baseWall = new Color(0.80f, 0.78f, 0.72f);
    public Color baseRoof = new Color(0.75f, 0.20f, 0.20f);
    public Color portal = new Color(0.55f, 0.15f, 0.75f);
    public Color sky = new Color(0.19f, 0.30f, 0.47f);
    public Color fog = new Color(0.55f, 0.70f, 0.85f);
    [Range(0f, 1f)] public float groundHeightNoise = 0.06f;
}

/// <summary>
/// Definición completa de un nivel (Type Object). La escena de juego es genérica:
/// LevelController lee esto y construye el mapa, los spots, las oleadas y la economía.
/// </summary>
[CreateAssetMenu(menuName = "TD/Level Definition", fileName = "Level_")]
public class LevelDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string levelId = "level_01";
    [Tooltip("Nombre de la escena del nivel (tiene que estar en Build Settings).")]
    public string sceneName = "Level01";
    public string displayName = "Nivel";
    [TextArea(2, 4)] public string subtitle = "";
    [TextArea(2, 5)] public string description = "";
    public int order = 1;
    public DefenseSource family = DefenseSource.Original;

    [Header("Grilla")]
    public int width = 20;
    public int height = 14;
    public float cellSize = 1f;

    [Header("Caminos (coordenadas de celda)")]
    public List<PathDef> paths = new List<PathDef>();

    [Header("Construcción")]
    public BuildMode buildMode = BuildMode.Spots;
    [Tooltip("Spots explícitos. Si autoSpots es true, se suman a los generados alrededor del camino.")]
    public List<Vector2Int> spots = new List<Vector2Int>();
    public bool autoSpots = true;
    [Tooltip("Distancia máxima (Chebyshev) al camino para generar spots automáticos.")]
    public int autoSpotMaxDistance = 2;
    [Tooltip("Celdas donde NO se generan spots automáticos.")]
    public List<Vector2Int> spotExclusions = new List<Vector2Int>();

    [Header("Decoración y terreno")]
    public List<CellDecor> decor = new List<CellDecor>();
    [Tooltip("Semilla para la decoración procedural (0 = sin decoración extra).")]
    public int decorSeed = 1;
    [Range(0f, 0.6f)] public float decorDensity = 0.12f;

    [Header("Economía")]
    public int startMoney = 250;
    public int startLives = 20;
    [Range(0f, 1f)] public float sellRefund = 0.6f;

    [Header("Torres disponibles (roster)")]
    public List<TowerId> roster = new List<TowerId>();

    [Header("Oleadas")]
    public List<WaveDef> waves = new List<WaveDef>();
    public float firstWaveDelay = 10f;
    public int waveClearBonus = 20;

    [Header("Tema visual")]
    public LevelTheme theme = new LevelTheme();

    [Header("Cámara")]
    [Range(30f, 80f)] public float cameraPitch = 58f;
    [Range(0.8f, 1.6f)] public float cameraDistanceFactor = 1.05f;

    // ─────────────────────────────────────────────────────────────────────
    // Helpers de geometría (no dependen de la escena)
    // ─────────────────────────────────────────────────────────────────────

    public bool InBounds(Vector2Int c) => c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;

    /// <summary>Centro de una celda en el mundo. Las celdas quedan centradas en enteros para que BuildGrid.Snap coincida.</summary>
    public Vector3 CellToWorld(Vector2Int c)
    {
        return new Vector3((c.x - width / 2) * cellSize, 0f, (c.y - height / 2) * cellSize);
    }

    public Vector2Int WorldToCell(Vector3 w)
    {
        return new Vector2Int(Mathf.RoundToInt(w.x / cellSize) + width / 2,
                              Mathf.RoundToInt(w.z / cellSize) + height / 2);
    }

    /// <summary>Rasteriza un camino: devuelve todas las celdas recorridas, en orden, sin repetir consecutivas.</summary>
    public List<Vector2Int> RasterizePath(PathDef p)
    {
        var cells = new List<Vector2Int>();
        if (p == null || p.waypoints == null || p.waypoints.Count == 0) return cells;

        Vector2Int cur = p.waypoints[0];
        cells.Add(cur);

        for (int i = 1; i < p.waypoints.Count; i++)
        {
            Vector2Int next = p.waypoints[i];
            // Segmento alineado a un eje: primero X, después Y (si el diseñador puso una diagonal, se resuelve en L).
            while (cur.x != next.x)
            {
                cur.x += cur.x < next.x ? 1 : -1;
                cells.Add(cur);
            }
            while (cur.y != next.y)
            {
                cur.y += cur.y < next.y ? 1 : -1;
                cells.Add(cur);
            }
        }
        return cells;
    }

    /// <summary>Conjunto de todas las celdas de camino de todos los caminos.</summary>
    public HashSet<Vector2Int> AllPathCells()
    {
        var set = new HashSet<Vector2Int>();
        foreach (var p in paths)
            foreach (var c in RasterizePath(p))
                set.Add(c);
        return set;
    }

    /// <summary>Spots finales: explícitos + automáticos (anillo alrededor del camino con separación 2, sin agua/decoración).</summary>
    public List<Vector2Int> ComputeSpots()
    {
        var result = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        var pathCells = AllPathCells();
        var blocked = new HashSet<Vector2Int>();
        foreach (var d in decor) blocked.Add(d.cell);
        foreach (var e in spotExclusions) blocked.Add(e);

        // Los extremos de cada camino (spawn y base) no admiten spots pegados.
        var endpoints = new HashSet<Vector2Int>();
        foreach (var p in paths)
        {
            if (p.waypoints.Count == 0) continue;
            endpoints.Add(p.waypoints[0]);
            endpoints.Add(p.waypoints[p.waypoints.Count - 1]);
        }

        foreach (var s in spots)
        {
            if (!InBounds(s) || pathCells.Contains(s) || blocked.Contains(s)) continue;
            if (seen.Add(s)) result.Add(s);
        }

        if (!autoSpots) return result;

        int maxD = Mathf.Max(1, autoSpotMaxDistance);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var c = new Vector2Int(x, y);
                if (pathCells.Contains(c) || blocked.Contains(c) || seen.Contains(c)) continue;
                if ((x % 2) != 0 || (y % 2) != 0) continue;   // separación 2 → las torres no se pisan

                int best = int.MaxValue;
                bool nearEndpoint = false;
                foreach (var pc in pathCells)
                {
                    int d = Mathf.Max(Mathf.Abs(pc.x - x), Mathf.Abs(pc.y - y));
                    if (d < best) best = d;
                    if (d <= 1 && endpoints.Contains(pc)) nearEndpoint = true;
                }
                if (best > maxD || nearEndpoint) continue;

                seen.Add(c);
                result.Add(c);
            }
        }
        return result;
    }

    /// <summary>Cantidad total de enemigos del nivel (para HUD / balance).</summary>
    public int TotalEnemies()
    {
        int n = 0;
        foreach (var w in waves) n += Mathf.Max(0, w.count);
        return n;
    }

    /// <summary>Cantidad de "números de oleada" (las sub-oleadas con joinPrevious no suman).</summary>
    public int WaveCount()
    {
        int n = 0;
        for (int i = 0; i < waves.Count; i++)
            if (i == 0 || !waves[i].joinPrevious) n++;
        return n;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        width = Mathf.Max(6, width);
        height = Mathf.Max(6, height);
        cellSize = Mathf.Max(0.5f, cellSize);
        startMoney = Mathf.Max(0, startMoney);
        startLives = Mathf.Max(1, startLives);
        if (waves != null)
            foreach (var w in waves)
            {
                w.count = Mathf.Max(1, w.count);
                w.spawnInterval = Mathf.Max(0.05f, w.spawnInterval);
                w.prepTime = Mathf.Max(0f, w.prepTime);
            }
    }
#endif
}
