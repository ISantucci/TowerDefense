using System.Collections.Generic;
using ClashDefense.Core;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Horneado de niveles (TL-003): la forma que se acomoda en la escena del nivel pasa a su LevelDefinition, que es lo que
    /// usan el juego, las pruebas y el simulador. Corre solo al guardar la escena y al entrar a Play (así se prueba lo que se
    /// ve aunque no se haya guardado). Después exporta los datos para el simulador.
    /// </summary>
    [InitializeOnLoad]
    static class LevelBaker
    {
        static LevelBaker()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            bool any = false;
            foreach (var a in LevelsIn(scene)) { Bake(a, false, path); any = true; }
            if (any) EditorApplication.delayCall += () => DataExporter.ExportAll(false);
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (var a in LevelsIn(SceneManager.GetSceneAt(i))) Bake(a, false);
        }

        public static IEnumerable<LevelAuthoring> LevelsIn(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var a in root.GetComponentsInChildren<LevelAuthoring>(true)) yield return a;
        }

        /// <summary>Hornea la forma de la escena en el LevelDefinition. Devuelve false si hay errores de forma (no hornea).</summary>
        public static bool Bake(LevelAuthoring a, bool verbose, string scenePath = null)
        {
            if (a == null || a.definition == null) { if (verbose) Debug.LogWarning("[ClashDefense] El nivel no tiene LevelDefinition: no se hornea."); return false; }
            var shapeErrors = a.ValidateShape();
            if (shapeErrors.Count > 0)
            {
                Debug.LogWarning($"[ClashDefense] {a.definition.name}: la forma no se horneó porque hay que corregir:\n • " + string.Join("\n • ", shapeErrors), a);
                return false;
            }
            var def = a.definition;
            string path = scenePath ?? a.gameObject.scene.path;
            bool changed = !Same(def.Shape, a.BuildShape()) || def.ScenePath != path;
            if (changed)
            {
                Undo.RecordObject(def, "Hornear nivel");
                def.SetShape(a.BuildShape());
                if (!string.IsNullOrEmpty(path)) def.SetScenePath(path);
                EditorUtility.SetDirty(def);
                AssetDatabase.SaveAssetIfDirty(def);
            }
            var errors = Validate(a);
            if (verbose || errors.Count > 0)
            {
                if (errors.Count == 0) Debug.Log($"[ClashDefense] {def.name}: horneado y válido.", def);
                else Debug.LogWarning($"[ClashDefense] {def.name}: horneado, pero no valida:\n • " + string.Join("\n • ", errors), def);
            }
            return true;
        }

        /// <summary>El nivel tal como está en la escena, con las oleadas y el resto de sus datos.</summary>
        public static LevelData CurrentData(LevelAuthoring a)
        {
            var l = a.definition != null ? a.definition.ToCore() : new LevelData();
            var s = a.BuildShape();
            l.routes = s.routes; l.pathWidth = s.pathWidth; l.buildArea = s.buildArea; l.blocked = s.blocked;
            l.baseRadius = s.baseRadius; l.tutorialHint = s.tutorialHint;
            return l;
        }

        /// <summary>Todo lo que el nivel tiene para corregir: forma y datos contra su balance.</summary>
        public static List<string> Validate(LevelAuthoring a)
        {
            var e = a.ValidateShape();
            if (e.Count > 0 || a.definition == null) return e;
            var bal = a.definition.balance != null ? a.definition.balance.ToCore() : null;
            if (bal == null) e.Add("los datos del nivel no tienen balance: no se pueden validar las oleadas");
            e.AddRange(DataValidator.ValidateLevel(bal, CurrentData(a)));
            return e;
        }

        static bool Same(LevelShape x, LevelShape y) => x != null && y != null && JsonUtility.ToJson(x) == JsonUtility.ToJson(y);

        /// <summary>Abre cada escena de nivel (sin tocar las abiertas), la hornea y la cierra.</summary>
        public static List<string> BakeAll()
        {
            var report = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:LevelDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || string.IsNullOrEmpty(def.ScenePath)) { report.Add($"{def?.name}: sin escena"); continue; }
                var open = SceneManager.GetSceneByPath(def.ScenePath);
                bool wasOpen = open.IsValid() && open.isLoaded;
                var scene = wasOpen ? open : EditorSceneManager.OpenScene(def.ScenePath, OpenSceneMode.Additive);
                bool ok = false;
                foreach (var a in LevelsIn(scene)) if (a.definition == def) ok = Bake(a, false, def.ScenePath);
                report.Add($"{def.name}: {(ok ? "horneado" : "NO horneado (ver consola)")}");
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
            return report;
        }
    }
}
