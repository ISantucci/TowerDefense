using System.Collections.Generic;
using ClashDefense.Core;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Arma la escena de un nivel a partir de sus datos (TL-003, una escena por nivel): área construible, base, puntos y
    /// recorridos (los puntos repetidos entre recorridos son el mismo objeto: así se ven y se editan las bifurcaciones), rocas y
    /// pista del tutorial. Lo usa la migración para pasar los niveles del JSON a escenas; después, la escena es la fuente.
    /// </summary>
    static class LevelSceneBuilder
    {
        public const string LevelScenes = "Assets/ClashDefense/Scenes/Niveles";

        /// <summary>
        /// Luz para ver el nivel mientras se edita. Tiene la etiqueta EditorOnly (no viaja a la build) y el juego la apaga al
        /// cargar el nivel: la luz de la partida es la de la escena del juego.
        /// </summary>
        public static Light EditLight()
        {
            var go = new GameObject("Luz de edición (solo editor)") { tag = "EditorOnly" };
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.05f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.55f;
            go.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.62f);
            return l;
        }

        static string Key(Vec2 p) => $"{Mathf.RoundToInt(p.x * 100)}:{Mathf.RoundToInt(p.z * 100)}";

        /// <summary>
        /// Crea la escena del nivel, la guarda en scenePath y hornea su forma en el LevelDefinition. Recibe la RUTA del asset
        /// y lo carga después de abrir la escena nueva: abrir una escena en modo Single descarga los assets que ninguna escena
        /// usa, y una referencia tomada antes queda inválida (orden 052).
        /// </summary>
        public static LevelAuthoring Build(string defPath, LevelData data, string scenePath, List<string> log)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(defPath);
            if (def == null) throw new System.Exception("no está el asset " + defPath);
            EditLight();
            var root = new GameObject("Nivel_" + def.name);
            var a = root.AddComponent<LevelAuthoring>();
            a.definition = def;
            a.pathWidth = data.pathWidth > 0f ? data.pathWidth : 2.2f;

            var ar = data.buildArea ?? new RectData();
            var area = new GameObject("AreaConstruible").AddComponent<BuildArea>();
            area.transform.SetParent(root.transform, false);
            area.transform.position = new Vector3((ar.minX + ar.maxX) * 0.5f, 0f, (ar.minZ + ar.maxZ) * 0.5f);
            area.size = new Vector2(ar.maxX - ar.minX, ar.maxZ - ar.minZ);

            var routes = LevelGeometry.RoutePoints(data);
            var basePos = routes.Count > 0 ? routes[0][routes[0].Length - 1] : new Vec2(0f, 0f);
            var b = new GameObject("Base").AddComponent<BaseAuthoring>();
            b.transform.SetParent(root.transform, false);
            b.transform.position = LevelAuthoring.ToWorld(basePos);
            b.radius = data.baseRadius > 0f ? data.baseRadius : 1.6f;

            var points = new GameObject(LevelTools.PointsName).transform;
            points.SetParent(root.transform, false);
            var routesRoot = new GameObject(LevelTools.RoutesName).transform;
            routesRoot.SetParent(root.transform, false);
            var byKey = new Dictionary<string, Waypoint>();
            int shared = 0;
            for (int ri = 0; ri < routes.Count; ri++)
            {
                var path = routes[ri];
                var rgo = new GameObject("Recorrido_" + ri);
                rgo.transform.SetParent(routesRoot, false);
                var route = rgo.AddComponent<RouteAuthoring>();
                route.gizmoColor = Color.HSVToRGB((ri * 0.17f + 0.12f) % 1f, 0.75f, 1f);
                if (Key(path[path.Length - 1]) != Key(basePos))
                    log.Add($"AVISO {def.name}: el recorrido {ri} no termina en la base ({path[path.Length - 1].x}, {path[path.Length - 1].z}); se lleva a la base");
                for (int i = 0; i < path.Length - 1; i++)   // el último punto es la base: el recorrido llega solo
                {
                    string k = Key(path[i]);
                    if (!byKey.TryGetValue(k, out var w))
                    {
                        var go = new GameObject("Punto_" + byKey.Count);
                        go.transform.SetParent(points, false);
                        go.transform.position = LevelAuthoring.ToWorld(path[i]);
                        w = go.AddComponent<Waypoint>();
                        byKey[k] = w;
                    }
                    else shared++;
                    route.points.Add(w);
                }
            }

            var rocks = new GameObject(LevelTools.RocksName).transform;
            rocks.SetParent(root.transform, false);
            if (data.blocked != null)
                for (int i = 0; i < data.blocked.Length; i++)
                {
                    var c = data.blocked[i];
                    if (c == null) continue;
                    var r = new GameObject("Roca_" + i).AddComponent<RockArea>();
                    r.transform.SetParent(rocks, false);
                    r.transform.position = new Vector3(c.x, 0f, c.z);
                    r.radius = c.radius;
                }

            if (data.tutorialHint != null && data.tutorialHint.radius > 0f)
            {
                var t = new GameObject("PistaTutorial").AddComponent<TutorialSpot>();
                t.transform.SetParent(root.transform, false);
                t.transform.position = new Vector3(data.tutorialHint.x, 0f, data.tutorialHint.z);
                t.radius = data.tutorialHint.radius;
            }

            root.AddComponent<LevelView>();
            AssetUtil.EnsureFolder(System.IO.Path.GetDirectoryName(scenePath));
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new System.Exception("no se pudo guardar " + scenePath);
            bool baked = LevelBaker.Bake(a, false, scenePath);
            EditorSceneManager.SaveScene(scene, scenePath);
            log.Add($"escena {scenePath}: {routes.Count} recorridos, {byKey.Count} puntos ({shared} compartidos), {(data.blocked?.Length ?? 0)} rocas{(baked ? "" : " · NO HORNEADA")}");
            return a;
        }
    }
}
