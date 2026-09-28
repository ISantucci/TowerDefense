using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Dibuja el nivel a partir de sus piezas: terreno, caminos, arcos de entrada, base y rocas (LDS-002.6, UXS-002.2).
    /// Corre también en el editor, sin Play: mover un punto del recorrido redibuja el camino al instante.
    /// Lo que dibuja es "generado": no se guarda en la escena (se rehace al abrirla) y no se puede seleccionar,
    /// para que lo que se toca sean siempre las piezas, no su dibujo.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(LevelAuthoring)), AddComponentMenu("Clash Defense/Nivel/Vista del nivel")]
    public sealed class LevelView : MonoBehaviour
    {
        const string GeneratedName = "(generado)";
        const HideFlags GenFlags = HideFlags.DontSave | HideFlags.NotEditable;

        LevelAuthoring authoring;
        int signature;
        Transform generated;
        readonly List<Renderer> baseRenderers = new List<Renderer>();

        public LevelAuthoring Authoring => authoring != null ? authoring : (authoring = GetComponent<LevelAuthoring>());
        /// <summary>Transform de la base (para el temblor al recibir daño).</summary>
        public Transform BaseTransform => Authoring.Base != null ? Authoring.Base.transform : null;
        /// <summary>Renderers de la base (para el destello rojo al recibir daño).</summary>
        public IReadOnlyList<Renderer> BaseRenderers => baseRenderers;

        void OnEnable()
        {
            Rebuild();
        }

        void Update()
        {
            if (Application.isPlaying) return;
            int s = Signature();
            if (s != signature) Rebuild();
        }

        /// <summary>Huella barata del estado de las piezas: si cambia, se redibuja.</summary>
        int Signature()
        {
            var a = Authoring;
            unchecked
            {
                int h = 17;
                void Add(float v) { h = h * 31 + Mathf.RoundToInt(v * 100f); }
                void AddV(Vector3 v) { Add(v.x); Add(v.z); }
                Add(a.pathWidth);
                var def = a.definition;
                h = h * 31 + (def != null && def.theme != null ? def.theme.GetInstanceID() : 0);
                foreach (var r in a.Routes())
                {
                    h = h * 31 + r.points.Count;
                    foreach (var p in r.points) if (p != null) AddV(p.transform.position);
                }
                var b = a.Base;
                if (b != null) { AddV(b.transform.position); Add(b.radius); h = h * 31 + (b.generateVisual ? 1 : 0); }
                var area = a.Area;
                if (area != null) { AddV(area.transform.position); Add(area.size.x); Add(area.size.y); }
                foreach (var rk in a.Rocks()) { AddV(rk.transform.position); Add(rk.radius); h = h * 31 + (rk.generateVisual ? 1 : 0); }
                return h;
            }
        }

        // ------------------------------------------------------------------ dibujo
        public void Rebuild()
        {
            var a = Authoring;
            if (a == null) return;
            signature = Signature();
            ClearGenerated();
            baseRenderers.Clear();

            var def = a.definition;
            var theme = def != null ? def.theme : null;
            var shape = a.BuildShape();

            generated = NewGen("Vista " + GeneratedName, transform).transform;

            // terreno: el área construible y lo que la rodea
            var ar = shape.buildArea;
            if (ar.maxX > ar.minX && ar.maxZ > ar.minZ)
            {
                var center = new Vector3((ar.minX + ar.maxX) * 0.5f, -0.05f, (ar.minZ + ar.maxZ) * 0.5f);
                Prim(PrimitiveType.Cube, generated, center + new Vector3(0, -0.03f, 0), new Vector3(ar.maxX - ar.minX + 40f, 0.1f, ar.maxZ - ar.minZ + 30f), Mat(theme, t => t.outside), "FueraDelArea");
                Prim(PrimitiveType.Cube, generated, center, new Vector3(ar.maxX - ar.minX, 0.1f, ar.maxZ - ar.minZ), Mat(theme, t => t.terrain), "AreaConstruible");
            }

            // caminos: un tramo por segmento único (los recorridos que comparten tramo no se dibujan dos veces)
            var pathMat = Mat(theme, t => t.path);
            var seen = new HashSet<string>();
            var joints = new HashSet<string>();
            var routes = new List<Vec2[]>();
            foreach (var r in shape.routes) if (r.points != null && r.points.Length >= 2) routes.Add(r.points);
            foreach (var path in routes)
            {
                for (int i = 1; i < path.Length; i++)
                {
                    if (!seen.Add(SegKey(path[i - 1], path[i]))) continue;
                    var p0 = LevelAuthoring.ToWorld(path[i - 1]); var p1 = LevelAuthoring.ToWorld(path[i]);
                    var dir = p1 - p0;
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    var seg = Prim(PrimitiveType.Cube, generated, (p0 + p1) * 0.5f + Vector3.up * 0.012f, new Vector3(shape.pathWidth, 0.02f, dir.magnitude), pathMat, "Camino");
                    seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
                for (int i = 1; i < path.Length - 1; i++)
                    if (joints.Add(PtKey(path[i])))
                        Prim(PrimitiveType.Cylinder, generated, LevelAuthoring.ToWorld(path[i]) + Vector3.up * 0.012f, new Vector3(shape.pathWidth, 0.01f, shape.pathWidth), pathMat, "Junta");
            }

            // entradas: un arco oscuro por punto de aparición
            var entMat = Mat(theme, t => t.entrance);
            float half = shape.pathWidth * 0.5f + 0.3f;
            var starts = new HashSet<string>();
            foreach (var path in routes)
            {
                if (!starts.Add(PtKey(path[0]))) continue;
                var s = LevelAuthoring.ToWorld(path[0]);
                var dir = LevelAuthoring.ToWorld(path[1]) - s;
                var arch = NewGen("Entrada", generated).transform;
                arch.position = s;
                if (dir.sqrMagnitude > 1e-6f) arch.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                Prim(PrimitiveType.Cube, arch, new Vector3(-half, 1.1f, 0), new Vector3(0.5f, 2.2f, 0.6f), entMat, null, true);
                Prim(PrimitiveType.Cube, arch, new Vector3(half, 1.1f, 0), new Vector3(0.5f, 2.2f, 0.6f), entMat, null, true);
                Prim(PrimitiveType.Cube, arch, new Vector3(0, 2.3f, 0), new Vector3(half * 2f + 0.5f, 0.4f, 0.6f), entMat, null, true);
            }

            // rocas del continente
            foreach (var rk in a.Rocks())
            {
                if (!rk.generateVisual) continue;
                var root = NewGen("Roca " + GeneratedName, rk.transform).transform;
                float r = rk.radius;
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.25f, 0), new Vector3(r * 2f, 0.9f, r * 2f), Mat(theme, t => t.rock), "Roca", true);
                Prim(PrimitiveType.Sphere, root, new Vector3(r * 0.3f, 0.55f, -r * 0.2f), new Vector3(r * 1.1f, 0.8f, r * 1.1f), Mat(theme, t => t.rockLight), "Roca", true);
            }

            // base: bloque de piedra con almenas y una puerta por cada llegada (UXS-001.1, Doc 03 §9)
            var b = a.Base;
            if (b != null)
            {
                var root = NewGen("Base " + GeneratedName, b.transform).transform;
                if (b.generateVisual)
                {
                    float sz = b.radius * 2f;
                    baseRenderers.Add(Prim(PrimitiveType.Cube, root, new Vector3(0, 0.9f, 0), new Vector3(sz, 1.8f, sz), Mat(theme, t => t.baseBlock), "Bloque", true).GetComponent<Renderer>());
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i & 1) == 0 ? -sz * 0.5f : sz * 0.5f, z = (i & 2) == 0 ? -sz * 0.5f : sz * 0.5f;
                        baseRenderers.Add(Prim(PrimitiveType.Cube, root, new Vector3(x, 2.05f, z), new Vector3(0.6f, 0.5f, 0.6f), Mat(theme, t => t.baseBattlement), "Almena", true).GetComponent<Renderer>());
                    }
                    foreach (var d in ArrivalDirections(routes))
                        baseRenderers.Add(Prim(PrimitiveType.Cube, root, -d * (sz * 0.5f) + new Vector3(0, 0.55f, 0), new Vector3(1f, 1.1f, 1f), entMat, "Puerta", true).GetComponent<Renderer>());
                }
                // un modelo propio puesto como hijo de la base también destella al recibir daño
                foreach (var rend in b.GetComponentsInChildren<Renderer>(true))
                    if (!baseRenderers.Contains(rend)) baseRenderers.Add(rend);
            }

            // en la partida, el terreno y los caminos no se mueven: se combinan para dibujarse en pocas llamadas
            if (Application.isPlaying) StaticBatchingUtility.Combine(generated.gameObject);
        }

        static List<Vector3> ArrivalDirections(List<Vec2[]> routes)
        {
            var list = new List<Vector3>();
            foreach (var r in routes)
            {
                var d = LevelAuthoring.ToWorld(r[r.Length - 1]) - LevelAuthoring.ToWorld(r[r.Length - 2]);
                if (d.sqrMagnitude < 1e-8f) continue;
                d.Normalize();
                bool dup = false;
                foreach (var s in list) if (Vector3.Dot(s, d) > 0.99f) { dup = true; break; }
                if (!dup) list.Add(d);
            }
            return list;
        }

        static string PtKey(Vec2 p) => $"{Mathf.RoundToInt(p.x * 100)}:{Mathf.RoundToInt(p.z * 100)}";
        static string SegKey(Vec2 a, Vec2 b) { string ka = PtKey(a), kb = PtKey(b); return string.CompareOrdinal(ka, kb) < 0 ? ka + "|" + kb : kb + "|" + ka; }

        static Material Mat(LevelTheme theme, System.Func<LevelTheme, Material> pick) => theme != null ? pick(theme) : null;

        // ------------------------------------------------------------------ objetos generados
        GameObject NewGen(string name, Transform parent)
        {
            var go = new GameObject(name) { hideFlags = GenFlags };
            go.transform.SetParent(parent, false);
            DisablePicking(go);
            return go;
        }

        GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, string name, bool local = false)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.hideFlags = GenFlags;
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = pos; else go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            DisablePicking(go);
            return go;
        }

        static void DisablePicking(GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.SceneVisibilityManager.instance.DisablePicking(go, false);
#endif
        }

        /// <summary>Borra todo lo generado (en el nivel, en la base y en las rocas). Lo generado se reconoce por su marca DontSave.</summary>
        void ClearGenerated()
        {
            generated = null;
            var doomed = new List<GameObject>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && (t.gameObject.hideFlags & HideFlags.DontSave) != 0 && (t.parent == null || (t.parent.gameObject.hideFlags & HideFlags.DontSave) == 0))
                    doomed.Add(t.gameObject);
            foreach (var go in doomed)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
        }
    }
}
