#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramienta de editor: reconstruye el mapa de la escena abierta a partir de su LevelDefinition,
/// con los mismos prefabs y materiales que usan las escenas generadas (todo queda en la jerarquía).
/// Menú: TD / Niveles / Reconstruir mapa desde LevelDefinition.
/// Regla: borra y vuelve a crear "Map" y "Paths"; no toca managers, HUD, cámara ni luces.
/// </summary>
public static class LevelSceneBuilder
{
    const string PrefabDir = "Assets/prefabs/Level/";
    const string MatDir = "Assets/Materials/TD/";

    [MenuItem("TD/Niveles/Reconstruir mapa desde LevelDefinition")]
    public static void RebuildOpenScene()
    {
        var controller = Object.FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
        if (controller == null || controller.level == null)
        {
            EditorUtility.DisplayDialog("Reconstruir mapa", "La escena abierta no tiene un LevelController con LevelDefinition asignado.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Reconstruir mapa",
            "Se van a borrar los objetos 'Map' y 'Paths' de la escena y a regenerarlos desde '" + controller.level.name + "'. ¿Seguir?", "Reconstruir", "Cancelar"))
            return;

        Undo.SetCurrentGroupName("Reconstruir mapa " + controller.level.displayName);
        int group = Undo.GetCurrentGroup();

        Build(controller);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Debug.Log("[LevelSceneBuilder] Mapa reconstruido para " + controller.level.displayName);
    }

    static void Build(LevelController controller)
    {
        var level = controller.level;
        var scene = controller.gameObject.scene;

        // 1) limpiar
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Map" || root.name == "Paths") Undo.DestroyObjectImmediate(root);

        // 2) materiales
        Material groundMat = LoadOrMake("TD_" + level.levelId + "_Ground", level.theme.ground);
        Material pathMat = LoadOrMake("TD_" + level.levelId + "_Path", level.theme.path);
        Material waterMat = LoadOrMake("TD_" + level.levelId + "_Water", level.theme.water);

        var map = NewRoot("Map");
        var pathCells = level.AllPathCells();

        // 3) suelo
        Vector3 c0 = level.CellToWorld(new Vector2Int(0, 0));
        Vector3 c1 = level.CellToWorld(new Vector2Int(level.width - 1, level.height - 1));
        var ground = Cube("Ground", map.transform, new Vector3((c0.x + c1.x) * 0.5f, -0.5f, (c0.z + c1.z) * 0.5f),
                          new Vector3(level.width * level.cellSize, 1f, level.height * level.cellSize), groundMat, GameLayers.Ground, true);

        // 4) camino por segmentos
        var tiles = NewChild("PathTiles", map.transform);
        var seen = new HashSet<string>();
        int seg = 0;
        foreach (var p in level.paths)
        {
            for (int i = 0; i + 1 < p.waypoints.Count; i++)
            {
                Vector2Int a = p.waypoints[i], b = p.waypoints[i + 1];
                var lo = new Vector2Int(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
                var hi = new Vector2Int(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                string key = lo + "-" + hi;
                if (!seen.Add(key)) continue;
                Vector3 w0 = level.CellToWorld(lo), w1 = level.CellToWorld(hi);
                Cube("PathSegment_" + seg++, tiles.transform, new Vector3((w0.x + w1.x) * 0.5f, 0.03f - 0.5f, (w0.z + w1.z) * 0.5f),
                     new Vector3(w1.x - w0.x + 0.98f, 1f, w1.z - w0.z + 0.98f), pathMat, GameLayers.EnemyPath, true);
            }
        }

        // 5) agua y decoración declarada
        var water = NewChild("Water", map.transform);
        var decor = NewChild("Decor", map.transform);
        var decorCells = new Dictionary<Vector2Int, CellKind>();
        int wi = 0;
        foreach (var d in level.decor)
        {
            if (!level.InBounds(d.cell) || pathCells.Contains(d.cell)) continue;
            if (d.kind == CellKind.Water)
            {
                Vector3 w = level.CellToWorld(d.cell);
                Cube("Water_" + wi++, water.transform, new Vector3(w.x, 0.01f - 0.5f, w.z), new Vector3(0.98f, 1f, 0.98f), waterMat, GameLayers.Obstacles, true);
            }
            else decorCells[d.cell] = d.kind;
        }

        // 6) spots
        var spotsRoot = NewChild("Spots", map.transform);
        var spotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "BuildSpot.prefab");
        var spotCells = level.ComputeSpots();
        var spotSet = new HashSet<Vector2Int>(spotCells);
        foreach (var c in spotCells)
            Place(spotPrefab, "BuildSpot", spotsRoot.transform, level.CellToWorld(c), 0f, 1f);

        // 7) decoración procedural (misma regla que el generador)
        var rng = new System.Random(level.decorSeed);
        if (level.decorSeed != 0 && level.decorDensity > 0f)
        {
            for (int y = 0; y < level.height; y++)
                for (int x = 0; x < level.width; x++)
                {
                    var c = new Vector2Int(x, y);
                    if (pathCells.Contains(c) || spotSet.Contains(c) || decorCells.ContainsKey(c)) continue;
                    int dist = int.MaxValue;
                    foreach (var pc in pathCells) dist = Mathf.Min(dist, Mathf.Max(Mathf.Abs(pc.x - x), Mathf.Abs(pc.y - y)));
                    if (dist <= 1) continue;
                    float chance = level.decorDensity * (dist >= 3 ? 1.6f : 0.6f);
                    if (rng.NextDouble() >= chance) continue;
                    double r = rng.NextDouble();
                    decorCells[c] = r < 0.45 ? CellKind.Tree : r < 0.65 ? CellKind.Bush : r < 0.85 ? CellKind.Flower : CellKind.Rock;
                }
        }
        foreach (var kv in decorCells)
        {
            string prefabName = PrefabFor(kv.Value);
            if (prefabName == null) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + prefabName + ".prefab");
            float s = (kv.Value == CellKind.Tree || kv.Value == CellKind.Rock || kv.Value == CellKind.Bush) ? (float)(0.85 + rng.NextDouble() * 0.35) : 1f;
            Place(prefab, prefabName, decor.transform, level.CellToWorld(kv.Key), (float)(rng.NextDouble() * 360.0), s);
        }

        // 8) portales y base
        var landmarks = NewChild("Landmarks", map.transform);
        var portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Portal.prefab");
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Base.prefab");
        var bases = new HashSet<Vector2Int>();
        foreach (var p in level.paths)
        {
            if (p.waypoints.Count < 2) continue;
            Vector2Int d = p.waypoints[1] - p.waypoints[0];
            float yaw = d.x > 0 ? 90f : d.x < 0 ? -90f : d.y > 0 ? 0f : 180f;
            Place(portalPrefab, "Portal", landmarks.transform, level.CellToWorld(p.waypoints[0]), yaw, 1f);
            var end = p.waypoints[p.waypoints.Count - 1];
            if (bases.Add(end)) Place(basePrefab, "Base", landmarks.transform, level.CellToWorld(end), 0f, 1f);
        }

        // 9) caminos con waypoints
        var paths = NewRoot("Paths");
        var graphs = new List<EnemyGraphPath>();
        for (int i = 0; i < level.paths.Count; i++)
        {
            var p = level.paths[i];
            var go = NewChild("Path_" + i + "_" + p.name.Replace(' ', '_'), paths.transform);
            go.layer = 7;
            var wp = go.AddComponent<WaypointsPath>();
            var pts = new Transform[p.waypoints.Count];
            for (int k = 0; k < p.waypoints.Count; k++)
            {
                var w = NewChild("WP_" + k, go.transform);
                w.layer = 7;
                Vector3 pos = level.CellToWorld(p.waypoints[k]);
                w.transform.position = new Vector3(pos.x, 0.5f, pos.z);
                pts[k] = w.transform;
            }
            wp.points = pts;
            var graph = go.AddComponent<EnemyGraphPath>();
            graph.path = wp;
            graphs.Add(graph);
        }

        // 10) recablear
        Undo.RecordObject(controller, "paths");
        controller.paths = graphs.ToArray();
        controller.spotsRoot = spotsRoot.transform;
        EditorUtility.SetDirty(controller);

        var factory = Object.FindFirstObjectByType<EnemyFactoryTD>(FindObjectsInactive.Include);
        if (factory != null && graphs.Count > 0)
        {
            var so = new SerializedObject(factory);
            var prop = so.FindProperty("defaultPath");
            if (prop != null) { prop.objectReferenceValue = graphs[0]; so.ApplyModifiedProperties(); }
        }
    }

    static string PrefabFor(CellKind k)
    {
        switch (k)
        {
            case CellKind.Tree: return "Tree";
            case CellKind.Rock: return "Rock";
            case CellKind.Bush: return "Bush";
            case CellKind.Flower: return "Flowers";
            case CellKind.Crystal: return "Crystal";
        }
        return null;
    }

    static GameObject NewRoot(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "crear " + name);
        return go;
    }

    static GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "crear " + name);
        return go;
    }

    static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, int layer, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
        if (!collider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
        Undo.RegisterCreatedObjectUndo(go, "crear " + name);
        return go;
    }

    static GameObject Place(GameObject prefab, string name, Transform parent, Vector3 pos, float yaw, float scale)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[LevelSceneBuilder] Falta el prefab " + name + " en " + PrefabDir);
            return null;
        }
        var go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (go == null) return null;
        go.name = name;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        Undo.RegisterCreatedObjectUndo(go, "crear " + name);
        return go;
    }

    static Material LoadOrMake(string name, Color color)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find("Standard");
        m = new Material(sh != null ? sh : Shader.Find("Diffuse"));
        m.color = color;
        if (!AssetDatabase.IsValidFolder("Assets/Materials/TD")) AssetDatabase.CreateFolder("Assets/Materials", "TD");
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }
}
#endif
