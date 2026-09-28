using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClashDefense.Game
{
    /// <summary>
    /// Carga la escena del nivel que se juega (o que se ve de fondo en los menús) junto a la escena del juego, y descarga la
    /// anterior (Unity: carga aditiva de escenas). La partida no espera a la escena: el núcleo usa la forma horneada del
    /// LevelDefinition; la escena aporta cómo se ve.
    /// </summary>
    public sealed class LevelStage
    {
        readonly Dictionary<string, Scene> loaded = new Dictionary<string, Scene>();
        readonly HashSet<string> loading = new HashSet<string>();
        string wanted;

        public LevelDefinition Current { get; private set; }
        /// <summary>La vista del nivel cargado. Null mientras carga.</summary>
        public LevelView View { get; private set; }
        public event Action<LevelView> Ready;

        public void Show(LevelDefinition def)
        {
            string path = def != null ? def.ScenePath : null;
            Current = def;
            wanted = path;
            var drop = new List<string>();
            foreach (var kv in loaded) if (kv.Key != path) drop.Add(kv.Key);
            foreach (var p in drop) { Unload(loaded[p]); loaded.Remove(p); }
            if (string.IsNullOrEmpty(path)) { View = null; return; }
            if (loaded.TryGetValue(path, out var already) && already.isLoaded) { if (View == null) Adopt(already); return; }
            View = null;
            var existing = SceneManager.GetSceneByPath(path);
            if (existing.IsValid() && existing.isLoaded) { loaded[path] = existing; Adopt(existing); return; }
            if (!loading.Add(path)) return;
            AsyncOperation op = null;
#if UNITY_EDITOR
            // en el editor, una escena de nivel se puede probar aunque todavía no esté en Build Settings
            op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            op = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
#endif
            if (op == null) { loading.Remove(path); Debug.LogError($"[ClashDefense] No se pudo cargar la escena del nivel {path}"); return; }
            op.completed += _ =>
            {
                loading.Remove(path);
                var s = SceneManager.GetSceneByPath(path);
                if (!s.IsValid()) return;
                if (path != wanted) { Unload(s); return; }   // se pidió otro nivel mientras cargaba
                loaded[path] = s;
                Adopt(s);
            };
        }

        /// <summary>Adopta una escena de nivel que ya estaba abierta (Play desde la escena del nivel).</summary>
        public void AdoptOpen(LevelDefinition def, Scene scene)
        {
            Current = def;
            wanted = def.ScenePath;
            loaded[wanted] = scene;
            Adopt(scene);
        }

        public void Clear() => Show(null);

        void Adopt(Scene s)
        {
            View = null;
            foreach (var root in s.GetRootGameObjects())
            {
                // la luz de edición de la escena del nivel no suma a la del juego
                foreach (var l in root.GetComponentsInChildren<Light>(true)) l.enabled = false;
                if (View == null) View = root.GetComponentInChildren<LevelView>(true);
            }
            Ready?.Invoke(View);
        }

        static void Unload(Scene s)
        {
            if (s.IsValid() && s.isLoaded) SceneManager.UnloadSceneAsync(s);
        }
    }
}
