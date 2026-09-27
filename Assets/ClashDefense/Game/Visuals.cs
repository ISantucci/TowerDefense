using System.Collections.Generic;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Materiales y primitivas del P0 gris (Doc 05 §2.2: sin arte definitivo). Un material por color, cacheado.</summary>
    public sealed class Visuals
    {
        readonly Material solidTemplate;
        readonly Material fadeTemplate;
        readonly Material lineTemplate;
        readonly Dictionary<Color, Material> solid = new Dictionary<Color, Material>();
        readonly Dictionary<Color, Material> line = new Dictionary<Color, Material>();

        public Visuals(Material solidMat, Material fadeMat, Material lineMat)
        {
            if (solidMat == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                solidMat = new Material(probe.GetComponent<Renderer>().sharedMaterial);
                Object.Destroy(probe);
            }
            solidTemplate = solidMat;
            fadeTemplate = fadeMat != null ? fadeMat : MakeFade(new Material(solidMat));
            lineTemplate = lineMat != null ? lineMat : new Material(Shader.Find("Sprites/Default"));
        }

        static Material MakeFade(Material m)
        {
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return m;
        }

        public Material Solid(Color c)
        {
            c.a = 1f;
            if (!solid.TryGetValue(c, out var m)) { m = new Material(solidTemplate) { color = c }; solid[c] = m; }
            return m;
        }

        /// <summary>Material propio (no compartido) para lo que cambia de color o transparencia: destellos, fantasma, fundidos.</summary>
        public Material Unique(Color c, bool transparent)
        {
            var m = new Material(transparent ? fadeTemplate : solidTemplate) { color = c };
            return m;
        }

        public Material Line(Color c)
        {
            if (!line.TryGetValue(c, out var m)) { m = new Material(lineTemplate) { color = c }; line[c] = m; }
            return m;
        }

        public GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return go;
        }

        /// <summary>Anillo en el piso (LineRenderer). Radio en unidades de mundo: el alcance real (LDS-001.1).</summary>
        public LineRenderer Ring(Transform parent, float radius, Color color, float width, float y = 0.05f, int segments = 72)
        {
            var go = new GameObject("Anillo");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.widthMultiplier = width;
            lr.sharedMaterial = Line(Color.white);
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            SetRing(lr, radius, y);
            return lr;
        }

        public static void SetRing(LineRenderer lr, float radius, float y = 0.05f)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
            }
        }
    }
}
