using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Guardado local del progreso (Doc 02 §2 y §9: local e independiente en cada versión). PlayerPrefs funciona igual
    /// en Windows (registro) y en WebGL (IndexedDB), que es la paridad que pide el Doc 02.
    /// </summary>
    public static class SaveStore
    {
        public const string DefaultKey = "cd_guardado_v1";
        /// <summary>El piloto de QA usa otra clave para no tocar el progreso del owner.</summary>
        public static string Key = DefaultKey;

        public static SaveData Load() => Load(Key);

        public static SaveData Load(string key)
        {
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return new SaveData();
            try
            {
                var s = JsonUtility.FromJson<SaveData>(json);
                return s ?? new SaveData();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ClashDefense] El guardado no se pudo leer; se empieza de cero. " + ex.Message);
                return new SaveData();
            }
        }

        public static void Save(SaveData s) => Save(s, Key);

        public static void Save(SaveData s, string key)
        {
            if (s == null) return;
            PlayerPrefs.SetString(key, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }

        public static void Reset() => Reset(Key);

        public static void Reset(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
