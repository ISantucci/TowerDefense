using System.Collections.Generic;
using System.IO;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Builds de Windows y WebGL (RQ-002.8) en Builds/, fuera de Assets, con las escenas de Build Settings (el juego primero y
    /// las escenas de nivel, que se cargan al jugar). Antes de cada build, BuildCheck valida los datos: una build no sale con
    /// un nivel sin hornear o una campaña que no valida.
    /// </summary>
    public static class BuildTools
    {
        [MenuItem("Clash Defense/Build/Windows", priority = 60)]
        static void BuildWindowsMenu() => Debug.Log("[ClashDefense] " + BuildWindows());

        [MenuItem("Clash Defense/Build/WebGL", priority = 61)]
        static void BuildWebGLMenu() => Debug.Log("[ClashDefense] " + BuildWebGL());

        public static string BuildWindows() => BuildTo(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/ClashDefense.exe");

        public static string BuildWebGL() => BuildTo(BuildTarget.WebGL, BuildTargetGroup.WebGL, "Builds/WebGL");

        public static string[] Scenes()
        {
            var list = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }

        static string BuildTo(BuildTarget target, BuildTargetGroup group, string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target)) return $"NO DISPONIBLE: el módulo de {target} no está instalado en este editor";
            var scenes = Scenes();
            if (scenes.Length == 0 || scenes[0] != GameBootstrap.GameScenePath) return "ERROR: la escena del juego tiene que ser la primera habilitada en Build Settings (Clash Defense/Mantenimiento/Migrar…)";
            var cfg = DataExporter.FindConfig(true);
            if (cfg != null) PlayerSettings.bundleVersion = cfg.version;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = target, targetGroup = group, options = BuildOptions.None });
            var s = report.summary;
            return $"{(s.result == BuildResult.Succeeded ? "OK" : "FALLO")} build {target}: {s.result} · {scenes.Length} escenas · {s.totalErrors} errores · {s.totalWarnings} avisos · {s.totalSize / (1024 * 1024)} MB · {s.totalTime.TotalSeconds:0} s · {Path.GetFullPath(path)}";
        }
    }

    /// <summary>Freno antes de la build: datos que no validan o niveles sin hornear o fuera de Build Settings cortan la build con el motivo.</summary>
    sealed class BuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = new List<string>();
            var cfg = DataExporter.FindConfig(true);
            if (cfg == null) errors.Add("no hay GameConfig con campaña");
            else
            {
                foreach (var e in cfg.campaign.Validate()) errors.Add("campaña: " + e);
                var enabled = new HashSet<string>(BuildTools.Scenes());
                var levels = new List<LevelDefinition>(cfg.campaign.Levels());
                if (cfg.labLevel != null) levels.Add(cfg.labLevel);
                foreach (var l in levels)
                {
                    foreach (var e in l.Validate()) errors.Add($"{l.name}: {e}");
                    if (!string.IsNullOrEmpty(l.ScenePath) && !enabled.Contains(l.ScenePath)) errors.Add($"{l.name}: su escena {l.ScenePath} no está habilitada en Build Settings");
                }
            }
            if (errors.Count > 0) throw new BuildFailedException("Clash Defense: los datos no validan, la build no sale:\n - " + string.Join("\n - ", errors));
        }
    }
}
