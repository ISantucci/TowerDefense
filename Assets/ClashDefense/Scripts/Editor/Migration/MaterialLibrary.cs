using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Materiales como assets (TL-003): uno por color, compartidos por todos los prefabs que lo usan. Durante la partida no se
    /// crea ningún material: los destellos y fundidos van por MaterialPropertyBlock (antes se creaban miles; orden 051:
    /// de 124 a 3670 materiales en un nivel). Cambiar el color de un material acá cambia todo lo que lo usa.
    /// </summary>
    static class MaterialLibrary
    {
        public const string Root = "Assets/ClashDefense/Materials";
        public const string FontRoot = "Assets/ClashDefense/UI/Fuentes";
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static void Reset() => cache.Clear();

        /// <summary>Opaco de un color (torres, enemigos, base).</summary>
        public static Material Solid(string name, Color c) => Get($"{Root}/Colores/{name}.mat", "Standard", c, false);

        /// <summary>Transparente de un color (sombras, quemadura, efectos que se desvanecen).</summary>
        public static Material Fade(string name, Color c) => Get($"{Root}/Efectos/{name}.mat", "Standard", c, true);

        /// <summary>Sin luz (líneas, barras de vida): el color se ve igual con cualquier iluminación.</summary>
        public static Material Unlit(string name, Color c) => Get($"{Root}/Efectos/{name}.mat", "Sprites/Default", c, false);

        /// <summary>Material de un tema de continente (terreno, camino, rocas, base).</summary>
        public static Material Theme(string theme, string part, Color c) => Get($"{Root}/Temas/{theme}_{part}.mat", "Standard", c, false);

        static Material Get(string path, string shaderName, Color c, bool transparent)
        {
            if (cache.TryGetValue(path, out var m) && m != null) return m;
            var shader = Shader.Find(shaderName);
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                AssetUtil.EnsureFolder(Path.GetDirectoryName(path));
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.color = c;
            if (transparent) MakeFade(m);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            cache[path] = m;
            return m;
        }

        static void MakeFade(Material m)
        {
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        /// <summary>
        /// Variante de la fuente con contorno oscuro (textos sobre el mapa, cuenta regresiva, título). Es un asset compartido:
        /// poner el contorno en cada texto creaba un material por texto.
        /// </summary>
        public static Material TmpOutline(float width)
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null || font.material == null) return null;
            string path = $"{FontRoot}/{font.name} Contorno {Mathf.RoundToInt(width * 100)}.mat";
            if (cache.TryGetValue(path, out var m) && m != null) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                AssetUtil.EnsureFolder(FontRoot);
                m = new Material(font.material);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(font.material);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(20, 22, 28, 255));
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            EditorUtility.SetDirty(m);
            cache[path] = m;
            return m;
        }
    }
}
