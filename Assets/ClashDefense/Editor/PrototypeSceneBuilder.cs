using System.Collections.Generic;
using System.IO;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Genera la escena del Prototipo 0 (SOL-001 D5): materiales, cámara, luz, EventSystem y GameBootstrap con sus datos,
    /// y la pone primera y única habilitada en Build Settings. Reproducible: correrlo dos veces da lo mismo.
    /// No guarda ninguna otra escena: si hay una abierta con cambios sin guardar, se niega.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string Root = "Assets/ClashDefense";
        const string ScenePath = Root + "/Scenes/Prototipo0.unity";

        [MenuItem("Clash Defense/Construir escena Prototipo 0")]
        public static void BuildMenu()
        {
            var r = Build();
            if (r.StartsWith("ERROR")) EditorUtility.DisplayDialog("Clash Defense", r, "OK");
            else Debug.Log(r);
        }

        [MenuItem("Clash Defense/Abrir escena Prototipo 0")]
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
            EnsureFolder(Root, "Materials"); EnsureFolder(Root, "Scenes");

            var solid = MakeMaterial(Root + "/Materials/CD_Solido.mat", "Standard", false, log);
            var fade = MakeMaterial(Root + "/Materials/CD_Transparente.mat", "Standard", true, log);
            var line = MakeMaterial(Root + "/Materials/CD_Linea.mat", "Sprites/Default", false, log);
            var balance = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/balance_p0.json");
            var level = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/level_p0.json");
            if (balance == null || level == null) return "ERROR: faltan los JSON en " + Root + "/Data";

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
            so.FindProperty("balanceJson").objectReferenceValue = balance;
            so.FindProperty("levelJson").objectReferenceValue = level;
            so.FindProperty("solidMaterial").objectReferenceValue = solid;
            so.FindProperty("fadeMaterial").objectReferenceValue = fade;
            so.FindProperty("lineMaterial").objectReferenceValue = line;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            log.Add(saved ? $"escena guardada: {ScenePath}" : "ERROR al guardar la escena");

            if (!File.Exists(CampaignSceneBuilder.ScenePath))
            {
                var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
                foreach (var s in EditorBuildSettings.scenes)
                    if (s.path != ScenePath) list.Add(new EditorBuildSettingsScene(s.path, false));
                EditorBuildSettings.scenes = list.ToArray();
                log.Add($"Build Settings: {ScenePath} primera y habilitada; {list.Count - 1} escenas del TP quedan deshabilitadas (no se borran)");
                PlayerSettings.productName = "Clash Defense";
                PlayerSettings.bundleVersion = "p0-0.1";
                log.Add("PlayerSettings: productName 'Clash Defense', versión 'p0-0.1'");
            }
            else log.Add("Build Settings y versión: los maneja la escena del juego (CampaignSceneBuilder); no se tocan");
            AssetDatabase.SaveAssets();
            return "OK Prototipo 0\n - " + string.Join("\n - ", log);
        }

        public static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        public static Material MakeMaterial(string path, string shaderName, bool transparent, List<string> log)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                log.Add("material creado: " + path);
            }
            else mat.shader = shader;
            mat.color = Color.white;
            if (transparent)
            {
                mat.SetFloat("_Mode", 2f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
