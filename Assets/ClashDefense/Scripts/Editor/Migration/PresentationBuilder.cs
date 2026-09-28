using System.Collections.Generic;
using ClashDefense.Core;
using ClashDefense.Game;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Genera los prefabs de la presentación (TL-003) con las mismas formas que antes se armaban en código en cada partida
    /// (WorldView/TowerView de TL-002): torres, enemigos, proyectiles y efectos, más los temas de continente y la biblioteca de
    /// presentación. De acá en adelante son assets: se abren, se miran y se reemplazan por arte sin tocar código.
    /// </summary>
    static class PresentationBuilder
    {
        public const string PrefabRoot = "Assets/ClashDefense/Prefabs";
        public const string DataRoot = "Assets/ClashDefense/Data";

        const float FlyerHeight = 1.6f;   // GDS-001.4, supuesto S4 (presentación)
        const float DragonHeight = 2.3f;

        static Material M(string name, Color c) => MaterialLibrary.Solid(name, c);
        static Color Scale(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        public static string FileName(string id) => AssetUtil.Pascal(id);

        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, string name = null, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name ?? t.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            if (!shadows) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            return go;
        }

        static Transform Node(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        // ------------------------------------------------------------------ temas de continente (UXS-002.2)
        public static Dictionary<string, LevelTheme> Themes(List<string> log)
        {
            var d = new Dictionary<string, LevelTheme>();
            d["praderas"] = Theme("Praderas", "praderas", Palette.Terreno, Palette.FueraDelArea, Palette.Camino, Palette.RocaPraderas);
            d["desfiladero"] = Theme("Desfiladero", "desfiladero", Palette.TerrenoDesfiladero, Palette.FueraDesfiladero, Palette.CaminoDesfiladero, Palette.RocaDesfiladero);
            d[""] = Theme("Gris", "", Palette.Terreno, Palette.FueraDelArea, Palette.Camino, Palette.Bloqueado);
            log.Add($"temas: {d.Count} (Praderas, Desfiladero, Gris del P0)");
            return d;
        }

        static LevelTheme Theme(string name, string id, Color terrain, Color outside, Color path, Color rock)
        {
            var t = ScriptableObject.CreateInstance<LevelTheme>();
            t.id = id;
            t.terrain = MaterialLibrary.Theme(name, "Terreno", terrain);
            t.outside = MaterialLibrary.Theme(name, "FueraDelArea", outside);
            t.path = MaterialLibrary.Theme(name, "Camino", path);
            t.rock = MaterialLibrary.Theme(name, "Roca", rock);
            t.rockLight = MaterialLibrary.Theme(name, "RocaClara", Scale(rock, 1.15f));
            t.baseBlock = M("Base", Palette.Base);
            t.baseBattlement = M("BaseAlmena", Palette.BaseAlmena);
            t.entrance = M("Entrada", Palette.Entrada);
            return AssetUtil.Save(t, $"{DataRoot}/Temas/Tema_{name}.asset");
        }

        // ------------------------------------------------------------------ torres (UXS-001.2, UXS-002.4)
        public static TowerVisual Tower(TowerTypeData t)
        {
            var root = new GameObject("Torre_" + FileName(t.id));
            var v = root.AddComponent<TowerVisual>();
            float fp = t.footprintRadius > 0f ? t.footprintRadius : 0.8f;
            Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.08f, 0), new Vector3(fp * 2f, 0.08f, fp * 2f), M("Zocalo", Palette.Zocalo), "Zocalo");
            var body = Node("Cuerpo", root.transform, Vector3.zero);
            v.body = body;
            Color c = Palette.ForTower(t.id);
            var fam = M("Torre_" + FileName(t.id), c);
            var bala = M("Bala", Palette.Bala);
            float bandY = 0.95f, bandW = 1.1f;
            switch (t.id)
            {
                case "arqueras":
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.0f, 0), new Vector3(0.7f, 0.85f, 0.7f), fam, "Torre");
                    Prim(PrimitiveType.Cube, body, new Vector3(0, 2.05f, 0), new Vector3(0.62f, 0.62f, 0.62f), fam, "Techo").transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                    bandY = 1.2f; bandW = 0.95f;
                    break;
                case "canon":
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.45f, 0), new Vector3(1.35f, 0.3f, 1.35f), fam, "Base");
                    v.aim = Node("Caño", body, new Vector3(0, 0.85f, 0));
                    Prim(PrimitiveType.Cylinder, v.aim, new Vector3(0, 0, 0.45f), new Vector3(0.32f, 0.45f, 0.32f), bala, "Tubo").transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    bandY = 0.55f; bandW = 1.45f;
                    break;
                case "mortero":
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.35f, 0), new Vector3(1.6f, 0.25f, 1.6f), fam, "Base");
                    v.aim = Node("Tubo", body, new Vector3(0, 0.75f, 0));
                    Prim(PrimitiveType.Cylinder, v.aim, new Vector3(0, 0.35f, 0.25f), new Vector3(0.6f, 0.45f, 0.6f), bala, "Boca").transform.localRotation = Quaternion.Euler(40f, 0, 0);
                    bandY = 0.45f; bandW = 1.7f;
                    break;
                case "bombardera":
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.7f, 0), new Vector3(1.0f, 0.6f, 1.0f), fam, "Base");
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.55f, 0), new Vector3(0.7f, 0.7f, 0.7f), bala, "Bomba");
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.95f, 0), new Vector3(0.08f, 0.12f, 0.08f), M("Fuego", Palette.Fuego), "Mecha");
                    break;
                case "electrica":
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.9f, 0), new Vector3(0.45f, 0.9f, 0.45f), M("PanelClaro", Palette.PanelClaro), "Poste");
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.2f, 0), new Vector3(0.95f, 0.05f, 0.95f), fam, "Aro1");
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.55f, 0), new Vector3(0.8f, 0.05f, 0.8f), fam, "Aro2");
                    v.orb = Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.95f, 0), new Vector3(0.55f, 0.55f, 0.55f), fam, "Orbe").transform;
                    v.orbBaseY = 1.9f;
                    break;
                case "infernal":
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.7f, 0), new Vector3(1.0f, 0.7f, 1.0f), bala, "Base");
                    var crystal = Prim(PrimitiveType.Cube, body, new Vector3(0, 1.85f, 0), new Vector3(0.6f, 0.9f, 0.6f), fam, "Cristal");
                    crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    v.orb = crystal.transform;
                    v.orbSpins = true;
                    break;
                case "oro":
                    Prim(PrimitiveType.Cube, body, new Vector3(0, 0.55f, 0), new Vector3(1.2f, 0.8f, 0.9f), M("OroBase", Scale(Palette.Canon, 0.7f)), "Cofre");
                    Prim(PrimitiveType.Cube, body, new Vector3(0, 1.0f, 0), new Vector3(1.25f, 0.15f, 0.95f), fam, "Tapa");
                    v.coins = Node("Monedas", body, new Vector3(0, 1.1f, 0));
                    for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cylinder, v.coins, new Vector3((i - 1) * 0.3f, 0.06f + (i == 1 ? 0.12f : 0f), 0), new Vector3(0.28f, 0.06f, 0.28f), fam, "Moneda" + i);
                    var full = Prim(PrimitiveType.Sphere, body, new Vector3(0, 2.1f, 0), new Vector3(0.45f, 0.45f, 0.45f), fam, "Llena");
                    full.SetActive(false);
                    v.fullMark = full.transform;
                    bandY = 0.75f; bandW = 1.3f;
                    break;
                case "lanzallamas":
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.6f, 0), new Vector3(1.05f, 0.5f, 1.05f), fam, "Base");
                    Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.15f, 0), new Vector3(0.8f, 0.6f, 0.8f), bala, "Tanque");
                    v.aim = Node("Boquilla", body, new Vector3(0, 1.15f, 0));
                    Prim(PrimitiveType.Cylinder, v.aim, new Vector3(0, 0, 0.55f), new Vector3(0.22f, 0.4f, 0.22f), fam, "Tubo").transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    break;
                default:   // mago y torres futuras
                    Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.75f, 0), new Vector3(0.85f, 0.6f, 0.85f), fam, "Torre");
                    v.orb = Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.9f, 0), new Vector3(0.6f, 0.6f, 0.6f), M("Orbe", Palette.Orbe), "Orbe").transform;
                    v.orbBaseY = 1.9f;
                    break;
            }
            var band = Prim(PrimitiveType.Cylinder, body, new Vector3(0, bandY, 0), new Vector3(bandW, 0.08f, bandW), M("Banda", Palette.Banda), "BandaN2");
            band.SetActive(false);
            v.bandN2 = band;
            v.n2Stretch = 1.2f;
            return AssetUtil.SavePrefab<TowerVisual>(root, $"{PrefabRoot}/Torres/Torre_{FileName(t.id)}.prefab");
        }

        // ------------------------------------------------------------------ enemigos (UXS-001.4, UXS-002.5)
        public static EnemyVisual Enemy(EnemyTypeData e)
        {
            var root = new GameObject("Enemigo_" + FileName(e.id));
            var v = root.AddComponent<EnemyVisual>();
            var body = Node("Cuerpo", root.transform, Vector3.zero);
            v.body = body;
            Color c = Palette.ForEnemy(e.id);
            var mat = M("Enemigo_" + FileName(e.id), c);
            var tint = new List<Renderer>();
            GameObject Part(PrimitiveType t, Vector3 p, Vector3 s, Material m, string n) { var g = Prim(t, body, p, s, m, n); tint.Add(g.GetComponent<Renderer>()); return g; }
            float barY = 1.45f, barW = 0.9f, aim = 0.9f;
            switch (e.id)
            {
                case "esqueleto":
                    Part(PrimitiveType.Capsule, new Vector3(0, 0.4f, 0), new Vector3(0.32f, 0.4f, 0.32f), mat, "Cuerpo");
                    Part(PrimitiveType.Sphere, new Vector3(0, 0.95f, 0), new Vector3(0.36f, 0.36f, 0.36f), mat, "Cabeza");
                    break;
                case "esbirro":
                    body.localPosition = new Vector3(0, FlyerHeight, 0);
                    Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.65f, 0.55f, 0.65f), mat, "Cuerpo");
                    Part(PrimitiveType.Cube, new Vector3(0.55f, 0.05f, 0), new Vector3(0.7f, 0.06f, 0.35f), mat, "AlaDerecha");
                    Part(PrimitiveType.Cube, new Vector3(-0.55f, 0.05f, 0), new Vector3(0.7f, 0.06f, 0.35f), mat, "AlaIzquierda");
                    AirShadow(root.transform, 0.9f);
                    barY = FlyerHeight + 0.8f; aim = FlyerHeight;
                    break;
                case "dragon":
                    body.localPosition = new Vector3(0, DragonHeight, 0);
                    var wing = M("Enemigo_DragonAla", Scale(Palette.Dragon, 0.8f));
                    Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(1.2f, 0.9f, 1.6f), mat, "Cuerpo");
                    Part(PrimitiveType.Sphere, new Vector3(0, 0.3f, 0.95f), new Vector3(0.7f, 0.6f, 0.7f), mat, "Cabeza");
                    Part(PrimitiveType.Cube, new Vector3(1.15f, 0.2f, 0), new Vector3(1.6f, 0.08f, 0.9f), wing, "AlaDerecha");
                    Part(PrimitiveType.Cube, new Vector3(-1.15f, 0.2f, 0), new Vector3(1.6f, 0.08f, 0.9f), wing, "AlaIzquierda");
                    Part(PrimitiveType.Cube, new Vector3(0, 0, -1.1f), new Vector3(0.25f, 0.2f, 0.9f), mat, "Cola");
                    AirShadow(root.transform, 2.2f);
                    barY = DragonHeight + 1.2f; barW = 2.2f; aim = DragonHeight;
                    break;
                case "blindado":
                    Part(PrimitiveType.Capsule, new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.45f, 0.5f), M("Enemigo_Duende", Palette.Duende), "Cuerpo");
                    v.shell = Part(PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(0.85f, 0.75f, 0.85f), M("Metal", Palette.Metal), "Armadura");
                    break;
                case "tanque":
                    Part(PrimitiveType.Capsule, new Vector3(0, 0.62f, 0), new Vector3(0.85f, 0.62f, 0.85f), mat, "Cuerpo");
                    Part(PrimitiveType.Cube, new Vector3(0, 0.95f, 0), new Vector3(1.15f, 0.3f, 0.6f), M("TanquePlaca", Palette.TanquePlaca), "Placa");
                    barY = 1.85f; aim = 1.0f;
                    break;
                case "gigante":
                    Part(PrimitiveType.Capsule, new Vector3(0, 1.2f, 0), new Vector3(1.6f, 1.2f, 1.6f), mat, "Cuerpo");
                    Part(PrimitiveType.Cylinder, new Vector3(0, 0.95f, 0), new Vector3(1.7f, 0.12f, 1.7f), M("TanquePlaca", Palette.TanquePlaca), "Cinturon");
                    Part(PrimitiveType.Sphere, new Vector3(0, 2.55f, 0.1f), new Vector3(0.9f, 0.9f, 0.9f), mat, "Cabeza");
                    barY = 3.4f; barW = 2.2f; aim = 1.8f;
                    break;
                default:   // duende y enemigos nuevos
                    Part(PrimitiveType.Capsule, new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.45f, 0.5f), mat, "Cuerpo");
                    break;
            }
            v.tint = tint.ToArray();
            v.aimHeight = aim;
            v.barRoot = Node("Barras", root.transform, new Vector3(0, barY, 0));
            v.hpFill = Bar(v.barRoot, 0f, "Barra_Vida", Palette.Texto, barW, "BarraVida");
            if (e.armor > 0f)
            {
                v.armorFill = Bar(v.barRoot, 0.16f, "Barra_Metal", Palette.Metal, barW, "BarraMetal");
                v.armorBar = v.armorFill.parent.gameObject;
            }
            // llama de la quemadura (UXS-002.4): visible solo mientras se quema
            var flame = Prim(PrimitiveType.Sphere, root.transform, new Vector3(0, barY - 0.35f, 0), new Vector3(0.35f, 0.5f, 0.35f), MaterialLibrary.Fade("Quemadura", Palette.Fuego), "Quemadura", false);
            flame.SetActive(false);
            v.flame = flame;
            return AssetUtil.SavePrefab<EnemyVisual>(root, $"{PrefabRoot}/Enemigos/Enemigo_{FileName(e.id)}.prefab");
        }

        static void AirShadow(Transform root, float size)
        {
            Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.03f, 0), new Vector3(size, 0.01f, size), MaterialLibrary.Fade("SombraAerea", Palette.Sombra), "SombraAerea", false);
        }

        /// <summary>Barra que mira a la cámara: fondo oscuro y relleno que se escala desde la izquierda (pivote).</summary>
        static Transform Bar(Transform parent, float y, string matName, Color fill, float w, string name)
        {
            var root = Node(name, parent, new Vector3(0, y, 0));
            Prim(PrimitiveType.Quad, root, Vector3.zero, new Vector3(w, 0.12f, 1f), MaterialLibrary.Unlit("Barra_Fondo", Palette.Zocalo), "Fondo", false);
            float inner = w - 0.06f;
            var pivot = Node("Pivote", root, new Vector3(-inner * 0.5f, 0, -0.01f));
            Prim(PrimitiveType.Quad, pivot, new Vector3(inner * 0.5f, 0, 0), new Vector3(inner, 0.07f, 1f), MaterialLibrary.Unlit(matName, fill), "Relleno", false);
            return pivot;
        }

        // ------------------------------------------------------------------ proyectiles
        /// <summary>Proyectil por torre: flecha, bala, bomba, bala de mortero u orbe. Las torres de rayo, fuego y oro no disparan proyectil.</summary>
        public static ProjectileVisual Projectile(string towerId)
        {
            string name; PrimitiveType t; Vector3 s; Color c; bool face;
            switch (towerId)
            {
                case "arqueras": name = "Flecha"; t = PrimitiveType.Cube; s = new Vector3(0.08f, 0.08f, 0.55f); c = Palette.Flecha; face = true; break;
                case "canon": name = "Bala"; t = PrimitiveType.Sphere; s = Vector3.one * 0.32f; c = Palette.Bala; face = false; break;
                case "bombardera": name = "Bomba"; t = PrimitiveType.Sphere; s = Vector3.one * 0.42f; c = Palette.Bala; face = false; break;
                case "mortero": name = "BalaMortero"; t = PrimitiveType.Sphere; s = Vector3.one * 0.5f; c = Palette.Bala; face = false; break;
                case "electrica": case "infernal": case "lanzallamas": case "oro": return null;
                default: name = "Orbe"; t = PrimitiveType.Sphere; s = Vector3.one * 0.38f; c = Palette.Orbe; face = false; break;
            }
            string path = $"{PrefabRoot}/Proyectiles/Proyectil_{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<ProjectileVisual>(path);
            if (existing != null && built.Contains(path)) return existing;
            var root = new GameObject("Proyectil_" + name);
            var v = root.AddComponent<ProjectileVisual>();
            v.faceDirection = face;
            Prim(t, root.transform, Vector3.zero, s, M(name == "Flecha" ? "Flecha" : name == "Orbe" ? "Orbe" : "Bala", c), name);
            built.Add(path);
            return AssetUtil.SavePrefab<ProjectileVisual>(root, path);
        }

        static readonly HashSet<string> built = new HashSet<string>();
        public static void Reset() => built.Clear();

        // ------------------------------------------------------------------ efectos y biblioteca
        public static PresentationLibrary Library(List<string> log)
        {
            var lib = ScriptableObject.CreateInstance<PresentationLibrary>();
            var fade = MaterialLibrary.Fade("Efecto", Color.white);
            lib.ghostMaterial = MaterialLibrary.Fade("Fantasma", Color.white);
            lib.lineMaterial = MaterialLibrary.Unlit("Linea", Color.white);

            // texto flotante
            var ft = new GameObject("TextoFlotante");
            var tmp = ft.AddComponent<TextMeshPro>();
            tmp.text = "+10";
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var outline = MaterialLibrary.TmpOutline(0.25f);
            if (outline != null) tmp.fontSharedMaterial = outline;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
            ft.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            ft.AddComponent<FloatingText>();
            lib.floatingText = AssetUtil.SavePrefab<FloatingText>(ft, $"{PrefabRoot}/Efectos/TextoFlotante.prefab");

            // anillo con borde oscuro debajo
            var ring = new GameObject("Anillo");
            var rfx = ring.AddComponent<RingFx>();
            rfx.under = Line(ring.transform, "Borde", lib.lineMaterial, 72, true, false);
            rfx.ring = Line(ring.transform, "Linea", lib.lineMaterial, 72, true, false);
            lib.ring = AssetUtil.SavePrefab<RingFx>(ring, $"{PrefabRoot}/Efectos/Anillo.prefab");

            lib.spark = MeshEffect("Chispa", PrimitiveType.Sphere, fade);
            lib.flameBurst = MeshEffect("Llamarada", PrimitiveType.Cube, fade);
            lib.fireOnGround = MeshEffect("FuegoEnElPiso", PrimitiveType.Cube, fade);
            lib.armorShell = MeshEffect("ArmaduraQueCae", PrimitiveType.Cube, fade);

            var lg = new GameObject("Rayo");
            Line(lg.transform, null, lib.lineMaterial, 7, false, true);
            lg.AddComponent<LineFx>();
            lib.lightning = AssetUtil.SavePrefab<LineFx>(lg, $"{PrefabRoot}/Efectos/Rayo.prefab");

            var ib = new GameObject("RayoInfernal");
            Line(ib.transform, null, lib.lineMaterial, 2, false, true);
            ib.AddComponent<LineFx>();
            lib.infernoBeam = AssetUtil.SavePrefab<LineFx>(ib, $"{PrefabRoot}/Efectos/RayoInfernal.prefab");

            lib.valid = Palette.Valido; lib.invalid = Palette.Invalido; lib.edge = Palette.Zocalo; lib.accent = Palette.Acento;
            lib.gold = Palette.Oro; lib.fire = Palette.Fuego; lib.immune = Palette.Inmune; lib.life = Palette.Vida; lib.text = Palette.Texto;
            lib.bomb = Palette.Bombardera; lib.orb = Palette.Orbe; lib.lightningColor = Palette.Rayo; lib.infernoColor = Palette.Infernal;
            log.Add("efectos: texto flotante, anillo, chispa, llamarada, fuego en el piso, armadura, rayo y rayo infernal");
            return AssetUtil.Save(lib, $"{DataRoot}/Presentacion.asset");
        }

        /// <summary>LineRenderer en un hijo (name) o en el mismo objeto (name null).</summary>
        static LineRenderer Line(Transform parent, string name, Material mat, int points, bool loop, bool world)
        {
            var go = name == null ? parent.gameObject : new GameObject(name);
            if (name != null) go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = world;
            lr.loop = loop;
            lr.positionCount = points;
            lr.widthMultiplier = 0.1f;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (loop) RingFx.Circle(lr, 1f, 0.07f);
            return lr;
        }

        static MeshFx MeshEffect(string name, PrimitiveType t, Material mat)
        {
            var root = new GameObject(name);
            var fx = root.AddComponent<MeshFx>();
            var mesh = Prim(t, root.transform, Vector3.zero, Vector3.one, mat, "Malla", false);
            fx.rend = mesh.GetComponent<Renderer>();
            return AssetUtil.SavePrefab<MeshFx>(root, $"{PrefabRoot}/Efectos/{name}.prefab");
        }
    }
}
