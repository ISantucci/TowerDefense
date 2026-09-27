using System.Collections.Generic;
using System.IO;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Genera la escena del juego (SOL-002): pantalla inicial, mapa del Mundo 1, tienda, opciones, los seis niveles y el
    /// Laboratorio con el P0. La pone primera y habilitada en Build Settings (la del P0 queda, deshabilitada).
    /// Reproducible: correrlo dos veces da lo mismo. No guarda ninguna otra escena: si hay una abierta con cambios, se niega.
    /// También arma las builds de Windows y WebGL (RQ-002.8) en Builds/, fuera de Assets.
    /// </summary>
    public static class CampaignSceneBuilder
    {
        const string Root = "Assets/ClashDefense";
        public const string ScenePath = Root + "/Scenes/ClashDefense.unity";
        const string P0Scene = Root + "/Scenes/Prototipo0.unity";
        public const string Version = "w1-0.1";

        [MenuItem("Clash Defense/Construir escena del juego (Mundo 1)")]
        public static void BuildMenu()
        {
            var r = Build();
            if (r.StartsWith("ERROR")) EditorUtility.DisplayDialog("Clash Defense", r, "OK");
            else Debug.Log(r);
        }

        [MenuItem("Clash Defense/Abrir escena del juego")]
        public static void OpenMenu()
        {
            if (!File.Exists(ScenePath)) { BuildMenu(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Punto de entrada para la bandeja (script-execute). Devuelve un informe de una línea por paso.</summary>
        public static string Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty && s.path != ScenePath) return $"ERROR: la escena abierta '{s.path}' tiene cambios sin guardar. No se toca: guardala o descartala y volvé a correr.";
            }
            var log = new List<string>();
            var solid = PrototypeSceneBuilder.MakeMaterial(Root + "/Materials/CD_Solido.mat", "Standard", false, log);
            var fade = PrototypeSceneBuilder.MakeMaterial(Root + "/Materials/CD_Transparente.mat", "Standard", true, log);
            var line = PrototypeSceneBuilder.MakeMaterial(Root + "/Materials/CD_Linea.mat", "Sprites/Default", false, log);
            TextAsset Load(string name) => AssetDatabase.LoadAssetAtPath<TextAsset>($"{Root}/Data/{name}.json");
            var p0b = Load("balance_p0"); var p0l = Load("level_p0");
            var camp = Load("campaign_w1"); var w1b = Load("balance_w1");
            var lv = new List<TextAsset>();
            for (int i = 1; i <= 6; i++) { var t = Load($"level_m1_n{i}"); if (t == null) return $"ERROR: falta {Root}/Data/level_m1_n{i}.json"; lv.Add(t); }
            if (p0b == null || p0l == null || camp == null || w1b == null) return "ERROR: faltan JSON en " + Root + "/Data";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();
            var lightGo = new GameObject("Luz");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.62f);
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            var boot = new GameObject("ClashDefense");
            var gb = boot.AddComponent<GameBootstrap>();
            var so = new SerializedObject(gb);
            so.FindProperty("balanceJson").objectReferenceValue = p0b;
            so.FindProperty("levelJson").objectReferenceValue = p0l;
            so.FindProperty("campaignJson").objectReferenceValue = camp;
            so.FindProperty("worldBalanceJson").objectReferenceValue = w1b;
            var arr = so.FindProperty("levelJsons");
            arr.arraySize = lv.Count;
            for (int i = 0; i < lv.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = lv[i];
            so.FindProperty("solidMaterial").objectReferenceValue = solid;
            so.FindProperty("fadeMaterial").objectReferenceValue = fade;
            so.FindProperty("lineMaterial").objectReferenceValue = line;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            log.Add(saved ? $"escena guardada: {ScenePath}" : "ERROR al guardar la escena");

            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) list.Add(new EditorBuildSettingsScene(s.path, false));
            if (!list.Exists(x => x.path == P0Scene) && File.Exists(P0Scene)) list.Add(new EditorBuildSettingsScene(P0Scene, false));
            EditorBuildSettings.scenes = list.ToArray();
            log.Add($"Build Settings: {ScenePath} primera y única habilitada; {list.Count - 1} escenas quedan deshabilitadas (no se borran)");

            PlayerSettings.productName = "Clash Defense";
            PlayerSettings.bundleVersion = Version;
            log.Add($"PlayerSettings: productName 'Clash Defense', versión '{Version}'");
            AssetDatabase.SaveAssets();
            return "OK juego (Mundo 1)\n - " + string.Join("\n - ", log);
        }

        [MenuItem("Clash Defense/Build de Windows")]
        public static void BuildWindowsMenu() => Debug.Log(BuildWindows());

        [MenuItem("Clash Defense/Build de WebGL")]
        public static void BuildWebGLMenu() => Debug.Log(BuildWebGL());

        /// <summary>Build de Windows en Builds/Windows (RQ-002.8). Devuelve el resultado en una línea.</summary>
        public static string BuildWindows() => BuildTo(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/ClashDefense.exe");

        /// <summary>Build de WebGL en Builds/WebGL, si el módulo está instalado (RQ-002.8).</summary>
        public static string BuildWebGL() => BuildTo(BuildTarget.WebGL, BuildTargetGroup.WebGL, "Builds/WebGL");

        static string BuildTo(BuildTarget target, BuildTargetGroup group, string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target)) return $"NO DISPONIBLE: el módulo de {target} no está instalado en este editor";
            if (!File.Exists(ScenePath)) return "ERROR: primero construí la escena del juego";
            PlayerSettings.bundleVersion = Version;
            var opts = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path, target = target, targetGroup = group, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            return $"{(s.result == BuildResult.Succeeded ? "OK" : "FALLO")} build {target}: {s.result} · {s.totalErrors} errores · {s.totalWarnings} avisos · {s.totalSize / (1024 * 1024)} MB · {s.totalTime.TotalSeconds:0} s · {Path.GetFullPath(path)}";
        }
    }
}
