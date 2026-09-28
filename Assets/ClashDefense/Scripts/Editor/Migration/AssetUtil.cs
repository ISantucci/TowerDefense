using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Crear o actualizar assets sin perder su identidad: si el archivo ya existe, se copian los datos nuevos adentro
    /// (el GUID no cambia y las referencias que lo apuntan siguen andando). Así los generadores se pueden correr dos veces.
    /// </summary>
    static class AssetUtil
    {
        /// <summary>Crea la carpeta y las que falten arriba ("Assets/A/B/C").</summary>
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Guarda el ScriptableObject en la ruta; si ya existía uno, lo actualiza en el lugar y devuelve ese.</summary>
        public static T Save<T>(T fresh, string path) where T : ScriptableObject
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(fresh);
            return existing;
        }

        /// <summary>Guarda el objeto como prefab (si existía, lo reemplaza conservando el GUID) y borra el objeto de la escena.</summary>
        public static T SavePrefab<T>(GameObject go, string path) where T : Component
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
            Object.DestroyImmediate(go);
            if (!ok || prefab == null) throw new System.Exception("no se pudo guardar el prefab " + path);
            return prefab.GetComponent<T>();
        }

        public static GameObject SavePrefab(GameObject go, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
            Object.DestroyImmediate(go);
            if (!ok || prefab == null) throw new System.Exception("no se pudo guardar el prefab " + path);
            return prefab;
        }

        /// <summary>Primer letra en mayúscula y sin tildes ni espacios: "el paso del dragón" → "ElPasoDelDragon". Para nombres de archivo.</summary>
        public static string Pascal(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var norm = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            bool up = true;
            foreach (char ch in norm)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (!char.IsLetterOrDigit(ch)) { up = true; continue; }
                sb.Append(up ? char.ToUpperInvariant(ch) : ch);
                up = false;
            }
            return sb.ToString();
        }
    }
}
