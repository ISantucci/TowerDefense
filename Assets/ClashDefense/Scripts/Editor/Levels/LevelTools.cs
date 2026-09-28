using System.Collections.Generic;
using System.IO;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Herramientas de trabajo con niveles (menú Clash Defense): nuevo nivel, agregar recorridos, puntos y rocas, hornear todo,
    /// abrir la escena del juego. Todo con Undo: Ctrl+Z deshace.
    /// </summary>
    static class LevelTools
    {
        public const string PointsName = "Puntos", RoutesName = "Recorridos", RocksName = "Rocas";

        public static Transform Container(LevelAuthoring a, string name)
        {
            var t = a.transform.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
            go.transform.SetParent(a.transform, false);
            return go.transform;
        }

        public static Waypoint NewPoint(LevelAuthoring a, Vector3 pos)
        {
            var parent = Container(a, PointsName);
            var go = new GameObject("Punto_" + parent.childCount);
            Undo.RegisterCreatedObjectUndo(go, "Crear punto");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, 0f, pos.z);
            return Undo.AddComponent<Waypoint>(go);
        }

        public static void AddRoute(LevelAuthoring a)
        {
            var parent = Container(a, RoutesName);
            var go = new GameObject("Recorrido_" + parent.childCount);
            Undo.RegisterCreatedObjectUndo(go, "Agregar recorrido");
            go.transform.SetParent(parent, false);
            var r = Undo.AddComponent<RouteAuthoring>(go);
            var area = a.Area;
            var start = area != null ? area.transform.position + new Vector3(-area.size.x * 0.5f, 0f, 0f) : Vector3.zero;
            r.points.Add(NewPoint(a, start));
            r.gizmoColor = Color.HSVToRGB((parent.childCount * 0.17f) % 1f, 0.7f, 1f);
            Selection.activeObject = go;
        }

        public static void AppendPoint(RouteAuthoring r)
        {
            var a = r.GetComponentInParent<LevelAuthoring>();
            if (a == null) return;
            var last = r.points.Count > 0 && r.points[r.points.Count - 1] != null ? r.points[r.points.Count - 1].transform.position : Vector3.zero;
            var b = a.Base != null ? a.Base.transform.position : last + Vector3.right * 6f;
            var p = NewPoint(a, Vector3.Lerp(last, b, 0.5f));
            Undo.RecordObject(r, "Agregar punto");
            r.points.Add(p);
            EditorUtility.SetDirty(r);
        }

        public static void InsertPoint(RouteAuthoring r, int index, Vector3 pos)
        {
            var a = r.GetComponentInParent<LevelAuthoring>();
            if (a == null) return;
            var p = NewPoint(a, pos);
            Undo.RecordObject(r, "Insertar punto");
            r.points.Insert(Mathf.Clamp(index, 0, r.points.Count), p);
            EditorUtility.SetDirty(r);
        }

        public static void RemoveLastPoint(RouteAuthoring r)
        {
            if (r.points.Count <= 1) return;
            Undo.RecordObject(r, "Quitar punto");
            var last = r.points[r.points.Count - 1];
            r.points.RemoveAt(r.points.Count - 1);
            EditorUtility.SetDirty(r);
            // si ningún otro recorrido lo usa, el punto se va
            var a = r.GetComponentInParent<LevelAuthoring>();
            bool used = false;
            if (a != null) foreach (var other in a.Routes()) if (other.points.Contains(last)) used = true;
            if (!used && last != null) Undo.DestroyObjectImmediate(last.gameObject);
        }

        public static void AddRock(LevelAuthoring a)
        {
            var parent = Container(a, RocksName);
            var go = new GameObject("Roca_" + parent.childCount);
            Undo.RegisterCreatedObjectUndo(go, "Agregar roca");
            go.transform.SetParent(parent, false);
            var area = a.Area;
            go.transform.position = area != null ? area.transform.position : Vector3.zero;
            Undo.AddComponent<RockArea>(go).radius = 1.5f;
            Selection.activeObject = go;
        }

        // ------------------------------------------------------------------ menú
        [MenuItem("Clash Defense/Abrir escena del juego", priority = 0)]
        static void OpenGame()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(GameBootstrap.GameScenePath);
        }

        [MenuItem("Clash Defense/Niveles/Hornear todos los niveles", priority = 20)]
        static void BakeAllMenu() => Debug.Log("[ClashDefense] Horneado:\n" + string.Join("\n", LevelBaker.BakeAll()));

        [MenuItem("Clash Defense/Niveles/Nuevo nivel…", priority = 21)]
        static void NewLevelMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = EditorUtility.SaveFilePanelInProject("Nuevo nivel", "M1_N7_Nuevo", "unity", "Nombre de la escena del nivel", "Assets/ClashDefense/Scenes/Niveles");
            if (string.IsNullOrEmpty(path)) return;
            var r = NewLevel(path);
            if (r != null) Debug.Log("[ClashDefense] " + r);
        }

        /// <summary>
        /// Crea un nivel nuevo: su LevelDefinition (con el balance y el tema del primer nivel de la campaña, si hay) y su escena con
        /// área, base y un recorrido de dos puntos. Queda abierto para acomodarlo. Sumarlo a la campaña es un paso aparte (el asset
        /// de la campaña, lista de niveles del continente).
        /// </summary>
        public static string NewLevel(string scenePath)
        {
            string name = Path.GetFileNameWithoutExtension(scenePath);
            string defPath = $"{ProjectMigration.DataRoot}/Niveles/{name}.asset";
            var template = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{ProjectMigration.DataRoot}/Niveles/M1_N1_ElSendero.asset");
            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.id = name.ToLowerInvariant();
            def.displayName = name;
            if (template != null) { def.theme = template.theme; def.balance = template.balance; def.waves = new List<Core.WaveData>(template.waves); }
            ProjectMigration.EnsureFolder(Path.GetDirectoryName(defPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(def, defPath);

            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(defPath);   // la escena nueva descarga los assets sin uso: se vuelve a cargar
            LevelSceneBuilder.EditLight();
            var root = new GameObject("Nivel_" + name);
            var a = root.AddComponent<LevelAuthoring>();
            a.definition = def;
            var area = new GameObject("AreaConstruible").AddComponent<BuildArea>();
            area.transform.SetParent(root.transform, false);
            area.size = new Vector2(40f, 22f);
            var b = new GameObject("Base").AddComponent<BaseAuthoring>();
            b.transform.SetParent(root.transform, false);
            b.transform.position = new Vector3(18f, 0f, 0f);
            var routes = Container(a, RoutesName);
            var rgo = new GameObject("Recorrido_0");
            rgo.transform.SetParent(routes, false);
            var route = rgo.AddComponent<RouteAuthoring>();
            route.points.Add(NewPoint(a, new Vector3(-21f, 0f, 0f)));
            route.points.Add(NewPoint(a, new Vector3(0f, 0f, 0f)));
            Container(a, RocksName);
            root.AddComponent<LevelView>();
            EditorSceneManager.SaveScene(scene, scenePath);
            LevelBaker.Bake(a, true, scenePath);
            // el juego carga la escena del nivel al jugarlo: va habilitada en Build Settings
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == scenePath)) { list.Add(new EditorBuildSettingsScene(scenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
            Selection.activeObject = root;
            return $"nivel nuevo: {scenePath} y {defPath} (ya está en Build Settings). Para jugarlo en la campaña, sumalo a la lista de niveles de un continente en el asset de la campaña.";
        }
    }
}
