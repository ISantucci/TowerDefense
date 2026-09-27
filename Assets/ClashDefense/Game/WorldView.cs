using System;
using System.Collections.Generic;
using ClashDefense.Core;
using TMPro;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Dibuja el mapa de un nivel (LDS-001.1, LDS-002.6) y las entidades del núcleo, y traduce los eventos a feedback visible
    /// (UXS-001.1, .2, .4 y UXS-002.4, .5). Solo lee el Match: nunca lo modifica.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        const float FlyerHeight = 1.6f;        // GDS-001.4, supuesto S4 (presentación)
        const float DragonHeight = 2.3f;
        const float ProjectileHeight = 1.1f;

        Visuals vis;
        Camera cam;
        LevelData level;
        Match match;
        Transform mapRoot, entityRoot, fxRoot;
        GameObject baseGo;
        readonly List<Material> baseMats = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();
        Vector3 basePos;
        Vector3 firstSpawn;

        readonly Dictionary<int, EnemyView> enemies = new Dictionary<int, EnemyView>();
        readonly Dictionary<int, TowerView> towers = new Dictionary<int, TowerView>();
        readonly Dictionary<int, GameObject> projectiles = new Dictionary<int, GameObject>();
        readonly Dictionary<int, GameObject> fireViews = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Beam> beams = new Dictionary<int, Beam>();
        readonly List<Fx> fx = new List<Fx>();

        LineRenderer highlight, highlightUnder;
        Vector3 highlightPos; float highlightRadius; bool highlightOn;
        LineRenderer selRing, selRingUnder, selMinRing, selMinRingUnder, previewRing, previewRingUnder;
        GameObject ghost; string ghostType; LineRenderer ghostRing, ghostRingUnder, ghostMinRing;

        public Vector3 BasePosition => basePos;

        // ------------------------------------------------------------------ construcción del mapa
        public void BuildMap(Visuals visuals, LevelData lvl, Camera camera)
        {
            vis = visuals; level = lvl; cam = camera;
            Clear();
            if (mapRoot != null) Destroy(mapRoot.gameObject);
            if (entityRoot == null) { entityRoot = new GameObject("Entidades").transform; entityRoot.SetParent(transform, false); }
            if (fxRoot == null) { fxRoot = new GameObject("Efectos").transform; fxRoot.SetParent(transform, false); BuildRings(); }
            mapRoot = new GameObject("Mapa").transform; mapRoot.SetParent(transform, false);
            baseMats.Clear(); baseColors.Clear();

            Palette.Theme(lvl.theme, out var terrain, out var outside, out var pathColor, out var rockColor);
            var a = lvl.buildArea;
            var areaCenter = new Vector3((a.minX + a.maxX) * 0.5f, -0.05f, (a.minZ + a.maxZ) * 0.5f);
            vis.Prim(PrimitiveType.Cube, mapRoot, areaCenter + new Vector3(0, -0.03f, 0), new Vector3(a.maxX - a.minX + 40f, 0.1f, a.maxZ - a.minZ + 30f), vis.Solid(outside), "FueraDelArea");
            vis.Prim(PrimitiveType.Cube, mapRoot, areaCenter, new Vector3(a.maxX - a.minX, 0.1f, a.maxZ - a.minZ), vis.Solid(terrain), "AreaConstruible");

            // caminos: un tramo por segmento único (los recorridos que comparten tramo no se dibujan dos veces)
            var camino = vis.Solid(pathColor);
            var seen = new HashSet<string>();
            var joints = new HashSet<string>();
            var routes = LevelGeometry.RoutePoints(lvl);
            foreach (var path in routes)
            {
                for (int i = 1; i < path.Length; i++)
                {
                    string key = SegKey(path[i - 1], path[i]);
                    if (!seen.Add(key)) continue;
                    var p0 = ToWorld(path[i - 1]); var p1 = ToWorld(path[i]);
                    var mid = (p0 + p1) * 0.5f; var dir = p1 - p0;
                    var seg = vis.Prim(PrimitiveType.Cube, mapRoot, mid + Vector3.up * 0.012f, new Vector3(lvl.pathWidth, 0.02f, dir.magnitude), camino, "Camino");
                    seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
                for (int i = 1; i < path.Length - 1; i++)
                    if (joints.Add(PtKey(path[i])))
                        vis.Prim(PrimitiveType.Cylinder, mapRoot, ToWorld(path[i]) + Vector3.up * 0.012f, new Vector3(lvl.pathWidth, 0.01f, lvl.pathWidth), camino, "Junta");
            }

            // zonas bloqueadas: rocas del continente (UXS-002.2)
            if (lvl.blocked != null)
                foreach (var b in lvl.blocked)
                {
                    if (b == null || b.radius <= 0f) continue;
                    var rock = vis.Solid(rockColor);
                    vis.Prim(PrimitiveType.Sphere, mapRoot, new Vector3(b.x, 0.25f, b.z), new Vector3(b.radius * 2f, 0.9f, b.radius * 2f), rock, "Roca");
                    vis.Prim(PrimitiveType.Sphere, mapRoot, new Vector3(b.x + b.radius * 0.3f, 0.55f, b.z - b.radius * 0.2f), new Vector3(b.radius * 1.1f, 0.8f, b.radius * 1.1f), vis.Solid(rockColor * 1.15f), "Roca");
                }

            // entradas: un arco oscuro por punto de aparición
            var ent = vis.Solid(Palette.Entrada);
            float half = lvl.pathWidth * 0.5f + 0.3f;
            var spawns = LevelGeometry.Spawns(lvl);
            firstSpawn = spawns.Count > 0 ? ToWorld(spawns[0]) : Vector3.zero;
            foreach (var path in routes)
            {
                var s = ToWorld(path[0]);
                if (!joints.Add("E" + PtKey(path[0]))) continue;
                var arch = new GameObject("Entrada").transform; arch.SetParent(mapRoot, false);
                arch.position = s; arch.rotation = Quaternion.LookRotation((ToWorld(path[1]) - s).normalized, Vector3.up);
                vis.Prim(PrimitiveType.Cube, arch, new Vector3(-half, 1.1f, 0), new Vector3(0.5f, 2.2f, 0.6f), ent);
                vis.Prim(PrimitiveType.Cube, arch, new Vector3(half, 1.1f, 0), new Vector3(0.5f, 2.2f, 0.6f), ent);
                vis.Prim(PrimitiveType.Cube, arch, new Vector3(0, 2.3f, 0), new Vector3(half * 2f + 0.5f, 0.4f, 0.6f), ent);
            }

            // base: bloque oscuro de piedra con almenas y una puerta por cada llegada (UXS-001.1, Doc 03 §9)
            basePos = ToWorld(routes[0][routes[0].Length - 1]);
            baseGo = new GameObject("Base");
            baseGo.transform.SetParent(mapRoot, false);
            baseGo.transform.position = basePos;
            float sz = lvl.baseRadius * 2f;
            AddBasePart(PrimitiveType.Cube, new Vector3(0, 0.9f, 0), new Vector3(sz, 1.8f, sz), Palette.Base);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -sz * 0.5f : sz * 0.5f, z = (i & 2) == 0 ? -sz * 0.5f : sz * 0.5f;
                AddBasePart(PrimitiveType.Cube, new Vector3(x, 2.05f, z), new Vector3(0.6f, 0.5f, 0.6f), Palette.BaseAlmena);
            }
            foreach (var d in LevelGeometry.ArrivalDirections(lvl))
            {
                var ld = new Vector3(d.x, 0, d.z);
                AddBasePart(PrimitiveType.Cube, -ld * (sz * 0.5f) + new Vector3(0, 0.55f, 0), new Vector3(1.0f, 1.1f, 1.0f), Palette.Entrada);
            }
        }

        static string PtKey(Vec2 p) => $"{Mathf.RoundToInt(p.x * 100)}:{Mathf.RoundToInt(p.z * 100)}";
        static string SegKey(Vec2 a, Vec2 b) { string ka = PtKey(a), kb = PtKey(b); return string.CompareOrdinal(ka, kb) < 0 ? ka + "|" + kb : kb + "|" + ka; }

        void BuildRings()
        {
            highlightUnder = vis.Ring(fxRoot, 1f, Palette.Zocalo, 0.35f, 0.07f); highlight = vis.Ring(fxRoot, 1f, Palette.Oro, 0.18f, 0.08f);
            SetHighlight(false);
            selRingUnder = vis.Ring(fxRoot, 1f, Palette.Zocalo, 0.22f, 0.06f); selRing = vis.Ring(fxRoot, 1f, Palette.Valido, 0.1f, 0.07f);
            selMinRingUnder = vis.Ring(fxRoot, 1f, Palette.Zocalo, 0.18f, 0.06f); selMinRing = vis.Ring(fxRoot, 1f, Palette.Invalido, 0.08f, 0.07f);
            previewRingUnder = vis.Ring(fxRoot, 1f, Palette.Zocalo, 0.2f, 0.06f); previewRing = vis.Ring(fxRoot, 1f, Palette.Acento, 0.1f, 0.075f);
            ShowSelection(null, 0f); ShowPreview(null, 0f);
        }

        void AddBasePart(PrimitiveType t, Vector3 local, Vector3 scale, Color c)
        {
            var m = vis.Unique(c, false);
            vis.Prim(t, baseGo.transform, local, scale, m);
            baseMats.Add(m); baseColors.Add(c);
        }

        static Vector3 ToWorld(Vec2 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Límites del contenido para el encuadre de la cámara (Doc 03 §20: todo el mapa, todas las entradas y la base).</summary>
        public static Bounds ContentBounds(LevelData lvl)
        {
            var a = lvl.buildArea;
            var b = new Bounds(new Vector3((a.minX + a.maxX) * 0.5f, 1f, (a.minZ + a.maxZ) * 0.5f), new Vector3(a.maxX - a.minX, 2.5f, a.maxZ - a.minZ));
            foreach (var path in LevelGeometry.RoutePoints(lvl)) foreach (var p in path) b.Encapsulate(new Vector3(p.x, 0f, p.z));
            b.Expand(new Vector3(1.5f, 0f, 1.5f));
            return b;
        }

        // ------------------------------------------------------------------ partida
        public void Bind(Match m)
        {
            Clear();
            match = m;
        }

        public void Clear()
        {
            foreach (var e in enemies.Values) if (e.Root != null) Destroy(e.Root);
            foreach (var t in towers.Values) if (t.Root != null) Destroy(t.Root);
            foreach (var p in projectiles.Values) if (p != null) Destroy(p);
            foreach (var f in fireViews.Values) if (f != null) Destroy(f);
            foreach (var b in beams.Values) if (b.Line != null) Destroy(b.Line.gameObject);
            enemies.Clear(); towers.Clear(); projectiles.Clear(); fireViews.Clear(); beams.Clear();
            foreach (var f in fx) f.End?.Invoke();
            fx.Clear();
            if (selRing != null) { ShowSelection(null, 0f); ShowPreview(null, 0f); SetHighlight(false); }
            HideGhost();
            match = null;
        }

        public void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.EnemySpawned: SpawnEnemy(e.EnemyId); break;
                case SimEventType.EnemyDamaged: if (enemies.TryGetValue(e.EnemyId, out var ed)) ed.Flash = e.Int1 == 1 ? Mathf.Max(ed.Flash, 0.03f) : 0.08f; break;
                case SimEventType.AttackImmune:
                    Spark(ToWorld(e.Pos) + Vector3.up * 0.9f);
                    FloatText(ToWorld(e.Pos) + Vector3.up * 1.6f, e.Int2 == 1 ? "CALENTANDO" : "INMUNE", Palette.Inmune, 5f, 0.7f);
                    break;
                case SimEventType.ArmorBroken: if (enemies.TryGetValue(e.EnemyId, out var eb)) DropShell(eb); break;
                case SimEventType.EnemyKilled:
                    if (enemies.TryGetValue(e.EnemyId, out var ek)) { KillEnemy(ek, false); enemies.Remove(e.EnemyId); }
                    FloatText(ToWorld(e.Pos) + Vector3.up * 1.2f, "+" + e.Int1, Palette.Oro, e.Int1 >= 100 ? 9f : 5f, 0.8f);
                    break;
                case SimEventType.EnemyReachedBase:
                    if (enemies.TryGetValue(e.EnemyId, out var ea)) { KillEnemy(ea, true); enemies.Remove(e.EnemyId); }
                    break;
                case SimEventType.BaseDamaged:
                    HitBase();
                    FloatText(basePos + Vector3.up * 3.0f, $"-{e.Int1 - e.Int2} {DisplayName(e.Text)}", Palette.Vida, 6f, 1.2f);
                    break;
                case SimEventType.TowerBuilt: BuildTower(e.TowerId); break;
                case SimEventType.TowerUpgraded: if (towers.TryGetValue(e.TowerId, out var tu)) { tu.SetLevel(2, vis); PulseRing(tu.Root.transform.position, Mathf.Max(1.5f, tu.Tower.Stats.range)); } break;
                case SimEventType.TowerSold:
                    if (towers.TryGetValue(e.TowerId, out var ts)) { Shrink(ts.Root, 0.2f); towers.Remove(e.TowerId); }
                    if (beams.TryGetValue(e.TowerId, out var bs)) { if (bs.Line != null) Destroy(bs.Line.gameObject); beams.Remove(e.TowerId); }
                    FloatText(ToWorld(e.Pos) + Vector3.up * 2.2f, "+" + e.Int1, Palette.Oro, 5f, 0.8f);
                    break;
                case SimEventType.Shot: Shoot(e); break;
                case SimEventType.ProjectileImpact:
                    if (projectiles.TryGetValue(e.Int1, out var pg)) { Destroy(pg); projectiles.Remove(e.Int1); }
                    if (e.Float1 > 0f) AreaBlast(ToWorld(e.Pos), e.Float1, e.Text == "mortero" ? Palette.Fuego : e.Text == "bombardera" ? Palette.Bombardera : Palette.Orbe);
                    break;
                case SimEventType.GoldCollected:
                    FloatText(ToWorld(e.Pos) + Vector3.up * 2.4f, "+" + e.Int1, Palette.Oro, 7f, 1.0f);
                    break;
                case SimEventType.GoldStoredFull:
                    FloatText(ToWorld(e.Pos) + Vector3.up * 2.6f, "¡LLENA!", Palette.Oro, 5f, 1.2f);
                    break;
            }
        }

        string DisplayName(string enemyId)
        {
            if (match != null) foreach (var t in match.Balance.enemies) if (t.id == enemyId) return t.displayName;
            return enemyId;
        }

        // ------------------------------------------------------------------ enemigos
        void SpawnEnemy(int id)
        {
            var e = match.GetEnemy(id);
            if (e == null) return;
            var v = new EnemyView { Enemy = e, MaxHp = e.Type.hp, MaxArmor = e.Type.armor, Flyer = e.Layer == Layer.Air };
            v.Root = new GameObject($"Enemigo_{e.Type.id}_{id}");
            v.Root.transform.SetParent(entityRoot, false);
            v.Root.transform.position = ToWorld(e.Position);
            var body = new GameObject("Cuerpo").transform; body.SetParent(v.Root.transform, false);
            v.Body = body;
            Color c = Palette.ForEnemy(e.Type.id);
            float barY = 1.45f, barW = 0.9f;
            switch (e.Type.id)
            {
                case "esqueleto":
                    v.Add(vis, PrimitiveType.Capsule, body, new Vector3(0, 0.4f, 0), new Vector3(0.32f, 0.4f, 0.32f), c);
                    v.Add(vis, PrimitiveType.Sphere, body, new Vector3(0, 0.95f, 0), new Vector3(0.36f, 0.36f, 0.36f), c);
                    break;
                case "esbirro":
                    body.localPosition = new Vector3(0, FlyerHeight, 0);
                    v.Add(vis, PrimitiveType.Sphere, body, Vector3.zero, new Vector3(0.65f, 0.55f, 0.65f), c);
                    v.Add(vis, PrimitiveType.Cube, body, new Vector3(0.55f, 0.05f, 0), new Vector3(0.7f, 0.06f, 0.35f), c);
                    v.Add(vis, PrimitiveType.Cube, body, new Vector3(-0.55f, 0.05f, 0), new Vector3(0.7f, 0.06f, 0.35f), c);
                    AirShadow(v, 0.9f);
                    barY = FlyerHeight + 0.8f;
                    break;
                case "dragon":
                    body.localPosition = new Vector3(0, DragonHeight, 0);
                    v.Add(vis, PrimitiveType.Sphere, body, Vector3.zero, new Vector3(1.2f, 0.9f, 1.6f), c);
                    v.Add(vis, PrimitiveType.Sphere, body, new Vector3(0, 0.3f, 0.95f), new Vector3(0.7f, 0.6f, 0.7f), c);
                    v.Add(vis, PrimitiveType.Cube, body, new Vector3(1.15f, 0.2f, 0), new Vector3(1.6f, 0.08f, 0.9f), Palette.Dragon * 0.8f);
                    v.Add(vis, PrimitiveType.Cube, body, new Vector3(-1.15f, 0.2f, 0), new Vector3(1.6f, 0.08f, 0.9f), Palette.Dragon * 0.8f);
                    v.Add(vis, PrimitiveType.Cube, body, new Vector3(0, 0, -1.1f), new Vector3(0.25f, 0.2f, 0.9f), c);
                    AirShadow(v, 2.2f);
                    barY = DragonHeight + 1.2f; barW = 2.2f;
                    break;
                case "blindado":
                    v.Add(vis, PrimitiveType.Capsule, body, new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.45f, 0.5f), Palette.Duende);
                    v.Shell = vis.Prim(PrimitiveType.Cube, body, new Vector3(0, 0.5f, 0), new Vector3(0.85f, 0.75f, 0.85f), vis.Unique(Palette.Metal, false), "Armadura");
                    v.Register(v.Shell.GetComponent<Renderer>(), Palette.Metal);
                    break;
                case "tanque":
                    v.Add(vis, PrimitiveType.Capsule, body, new Vector3(0, 0.62f, 0), new Vector3(0.85f, 0.62f, 0.85f), c);
                    v.Add(vis, PrimitiveType.Cube, body, new Vector3(0, 0.95f, 0), new Vector3(1.15f, 0.3f, 0.6f), Palette.TanquePlaca);
                    barY = 1.85f;
                    break;
                case "gigante":
                    v.Add(vis, PrimitiveType.Capsule, body, new Vector3(0, 1.2f, 0), new Vector3(1.6f, 1.2f, 1.6f), c);
                    v.Add(vis, PrimitiveType.Cylinder, body, new Vector3(0, 0.95f, 0), new Vector3(1.7f, 0.12f, 1.7f), Palette.TanquePlaca);
                    v.Add(vis, PrimitiveType.Sphere, body, new Vector3(0, 2.55f, 0.1f), new Vector3(0.9f, 0.9f, 0.9f), c);
                    barY = 3.4f; barW = 2.2f;
                    break;
                default:
                    v.Add(vis, PrimitiveType.Capsule, body, new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.45f, 0.5f), c);
                    break;
            }
            v.BarRoot = new GameObject("Barras").transform; v.BarRoot.SetParent(v.Root.transform, false);
            v.BarRoot.localPosition = new Vector3(0, barY, 0);
            v.HpFill = Bar(v.BarRoot, 0f, Palette.Texto, barW);
            if (v.MaxArmor > 0f) { v.ArmorFill = Bar(v.BarRoot, 0.16f, Palette.Metal, barW); v.ArmorBar = v.ArmorFill.parent.gameObject; }
            // llama de la quemadura (UXS-002.4): visible solo mientras se quema
            var flame = vis.Prim(PrimitiveType.Sphere, v.Root.transform, new Vector3(0, barY - 0.35f, 0), new Vector3(0.35f, 0.5f, 0.35f), vis.Unique(Palette.Fuego, true), "Quemadura");
            flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flame.SetActive(false);
            v.Flame = flame;
            enemies[id] = v;
        }

        void AirShadow(EnemyView v, float size)
        {
            var shadow = vis.Prim(PrimitiveType.Cylinder, v.Root.transform, new Vector3(0, 0.03f, 0), new Vector3(size, 0.01f, size), vis.Unique(Palette.Sombra, true), "SombraAerea");
            shadow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Transform Bar(Transform parent, float y, Color fill, float w)
        {
            var root = new GameObject("Barra").transform; root.SetParent(parent, false); root.localPosition = new Vector3(0, y, 0);
            var bg = vis.Prim(PrimitiveType.Quad, root, Vector3.zero, new Vector3(w, 0.12f, 1f), vis.Line(Palette.Zocalo));
            bg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float inner = w - 0.06f;
            var pivot = new GameObject("Pivote").transform; pivot.SetParent(root, false); pivot.localPosition = new Vector3(-inner * 0.5f, 0, -0.01f);
            var f = vis.Prim(PrimitiveType.Quad, pivot, new Vector3(inner * 0.5f, 0, 0), new Vector3(inner, 0.07f, 1f), vis.Line(fill));
            f.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return pivot;
        }

        void DropShell(EnemyView v)
        {
            if (v.Shell == null) return;
            var shell = v.Shell; v.Shell = null;
            v.Unregister(shell.GetComponent<Renderer>());
            if (v.ArmorBar != null) { Destroy(v.ArmorBar); v.ArmorBar = null; v.ArmorFill = null; }
            shell.transform.SetParent(fxRoot, true);
            var r = shell.GetComponent<Renderer>();
            var m = vis.Unique(Palette.Metal, true); r.sharedMaterial = m;
            var start = shell.transform.position;
            var spin = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0.3f, UnityEngine.Random.Range(-1f, 1f)).normalized;
            fx.Add(new Fx(0.5f, t =>
            {
                shell.transform.position = start + new Vector3(0, 1.2f * t - 2.5f * t * t, 0) + spin * t * 0.8f;
                shell.transform.Rotate(spin, 720f * Time.deltaTime, Space.World);
                var c = m.color; c.a = 1f - t; m.color = c;
            }, () => Destroy(shell)));
        }

        void KillEnemy(EnemyView v, bool arrived)
        {
            var root = v.Root;
            if (root == null) return;
            var from = root.transform.position;
            var baseScale = root.transform.localScale;
            fx.Add(new Fx(arrived ? 0.35f : v.Enemy.Type.miniboss ? 0.6f : 0.2f, t =>
            {
                if (root == null) return;
                if (arrived) root.transform.position = Vector3.Lerp(from, basePos, t);
                root.transform.localScale = baseScale * (1f - t);
            }, () => { if (root != null) Destroy(root); }));
            if (!arrived && v.Enemy.Type.miniboss) { AreaBlast(from, 3.5f, Palette.Oro); AreaBlast(from, 2.2f, Palette.Texto); }
        }

        // ------------------------------------------------------------------ torres
        void BuildTower(int id)
        {
            var t = match.GetTower(id);
            if (t == null) return;
            var tv = new TowerView { Tower = t };
            tv.Root = new GameObject($"Torre_{t.Type.id}_{id}");
            tv.Root.transform.SetParent(entityRoot, false);
            tv.Root.transform.position = ToWorld(t.Position);
            tv.Build(vis, t.Type, 1, false);
            towers[id] = tv;
            var root = tv.Root;
            fx.Add(new Fx(0.15f, k => { if (root != null) root.transform.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, k), 1f); }, () => { if (root != null) root.transform.localScale = Vector3.one; }));
        }

        public int PickTower(Vector3 world, float extra = 0.4f)
        {
            if (match == null) return 0;
            int best = 0; float bestD = float.MaxValue;
            foreach (var t in match.Towers)
            {
                float d = Vector2.Distance(new Vector2(world.x, world.z), new Vector2(t.Position.x, t.Position.z));
                if (d <= t.Type.footprintRadius + extra && d < bestD) { bestD = d; best = t.Id; }
            }
            return best;
        }

        public void ShowSelection(Tower t, float range)
        {
            bool on = t != null && range > 0f;
            selRing.gameObject.SetActive(on); selRingUnder.gameObject.SetActive(on);
            selMinRing.gameObject.SetActive(on && t.Stats.minRange > 0f); selMinRingUnder.gameObject.SetActive(on && t.Stats.minRange > 0f);
            if (!on) return;
            var p = ToWorld(t.Position);
            selRing.transform.position = p; selRingUnder.transform.position = p; selMinRing.transform.position = p; selMinRingUnder.transform.position = p;
            Visuals.SetRing(selRing, range, 0.07f); Visuals.SetRing(selRingUnder, range, 0.06f);
            if (t.Stats.minRange > 0f) { Visuals.SetRing(selMinRing, t.Stats.minRange, 0.07f); Visuals.SetRing(selMinRingUnder, t.Stats.minRange, 0.06f); }
        }

        /// <summary>Anillo del alcance N2 antes de pagar la mejora (UXS-001.3).</summary>
        public void ShowPreview(Tower t, float range)
        {
            bool on = t != null && range > 0f;
            previewRing.gameObject.SetActive(on); previewRingUnder.gameObject.SetActive(on);
            if (!on) return;
            previewRing.transform.position = ToWorld(t.Position); previewRingUnder.transform.position = ToWorld(t.Position);
            Visuals.SetRing(previewRing, range, 0.075f); Visuals.SetRing(previewRingUnder, range, 0.065f);
        }

        // ------------------------------------------------------------------ fantasma de colocación (UXS-001.2)
        public void ShowGhost(TowerTypeData type, Vector3 pos, bool valid)
        {
            if (ghost == null || ghostType != type.id)
            {
                HideGhost();
                ghostType = type.id;
                ghost = new GameObject("Fantasma");
                ghost.transform.SetParent(fxRoot, false);
                var tv = new TowerView();
                tv.Root = ghost;
                tv.Build(vis, type, 1, true);
                float r = type.levels[0].range;
                if (r > 0f)
                {
                    ghostRingUnder = vis.Ring(ghost.transform, r, Palette.Zocalo, 0.24f, 0.06f);
                    ghostRing = vis.Ring(ghost.transform, r, Palette.Valido, 0.12f, 0.07f);
                }
                // cada anillo de color lleva debajo un borde oscuro: el rojo y el blanco no se leen solos sobre el terreno (UXS-002.4, Ley 2)
                if (type.levels[0].minRange > 0f)
                {
                    vis.Ring(ghost.transform, type.levels[0].minRange, Palette.Zocalo, 0.18f, 0.06f);
                    ghostMinRing = vis.Ring(ghost.transform, type.levels[0].minRange, Palette.Invalido, 0.08f, 0.07f);
                }
                // el pie de la torre siempre marca válido o inválido, aunque no tenga alcance (Oro)
                vis.Ring(ghost.transform, type.footprintRadius + 0.15f, Palette.Zocalo, 0.2f, 0.06f, 32);
                var foot = vis.Ring(ghost.transform, type.footprintRadius + 0.15f, Palette.Valido, 0.1f, 0.07f, 32);
                foot.name = "Pie";
            }
            ghost.SetActive(true);
            ghost.transform.position = pos;
            var col = valid ? Palette.Valido : Palette.Invalido;
            if (ghostRing != null) ghostRing.startColor = ghostRing.endColor = col;
            var pie = ghost.transform.Find("Pie")?.GetComponent<LineRenderer>();
            if (pie != null) pie.startColor = pie.endColor = col;
        }

        public void HideGhost()
        {
            if (ghost != null) Destroy(ghost);
            ghost = null; ghostType = null; ghostRing = null; ghostRingUnder = null; ghostMinRing = null;
        }

        // ------------------------------------------------------------------ tutorial (GDS-001.6)
        public void Highlight(string what)
        {
            if (level == null) return;
            switch (what)
            {
                case "base": SetHighlight(true, basePos, level.baseRadius + 1.2f); break;
                case "entrada": SetHighlight(true, firstSpawn + new Vector3(2f, 0, 0), 2.5f); break;
                case "pista":
                    if (level.tutorialHint != null && level.tutorialHint.radius > 0f) SetHighlight(true, new Vector3(level.tutorialHint.x, 0, level.tutorialHint.z), level.tutorialHint.radius);
                    else SetHighlight(false);
                    break;
                default: SetHighlight(false); break;
            }
        }

        void SetHighlight(bool on, Vector3 pos = default, float radius = 1f)
        {
            highlightOn = on; highlightPos = pos; highlightRadius = radius;
            if (highlight == null) return;
            highlight.gameObject.SetActive(on); highlightUnder.gameObject.SetActive(on);
            if (on) { highlight.transform.position = pos; highlightUnder.transform.position = pos; }
        }

        // ------------------------------------------------------------------ combate
        void Shoot(SimEvent e)
        {
            towers.TryGetValue(e.TowerId, out var tv);
            if (tv != null) tv.Aim(ToWorld(e.Aim));
            switch (e.Text)
            {
                case "electrica": Lightning(ToWorld(e.Pos) + Vector3.up * (e.Int2 <= 1 ? 1.9f : 0.9f), ToWorld(e.Aim) + Vector3.up * 0.9f, e.Int2); return;
                case "infernal": InfernoBeam(e); return;
                case "lanzallamas": FlameBurst(ToWorld(e.Pos), ToWorld(e.Aim), e.Float2); return;
            }
            if (e.Int1 == 0) return;
            var go = new GameObject("Proyectil_" + e.Text);
            go.transform.SetParent(fxRoot, false);
            go.transform.position = ToWorld(e.Pos) + Vector3.up * ProjectileHeight;
            switch (e.Text)
            {
                case "arqueras": vis.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.08f, 0.08f, 0.55f), vis.Solid(Palette.Flecha)); break;
                case "canon": vis.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.32f, 0.32f, 0.32f), vis.Solid(Palette.Bala)); break;
                case "bombardera": vis.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.42f, 0.42f, 0.42f), vis.Solid(Palette.Bala)); break;
                case "mortero":
                    vis.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), vis.Solid(Palette.Bala));
                    ImpactMarker(ToWorld(e.Aim), e.Float1, match != null && match.GetTower(e.TowerId) != null ? match.GetTower(e.TowerId).Stats.areaRadius : 2f);
                    break;
                default: vis.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.38f, 0.38f, 0.38f), vis.Solid(Palette.Orbe)); break;
            }
            projectiles[e.Int1] = go;
        }

        /// <summary>Punto de impacto previsto del Mortero (Doc 04 §18): un anillo que se cierra mientras cae el proyectil.</summary>
        void ImpactMarker(Vector3 at, float seconds, float radius)
        {
            var under = vis.Ring(fxRoot, radius, Palette.Zocalo, 0.24f, 0.085f, 48);   // borde oscuro: el naranja solo no despega del terreno (UXS-002.4)
            var ring = vis.Ring(fxRoot, radius, Palette.Fuego, 0.12f, 0.09f, 48);
            ring.transform.position = at; under.transform.position = at;
            fx.Add(new Fx(Mathf.Max(0.2f, seconds), t =>
            {
                float r = radius * (1.15f - 0.15f * t);
                Visuals.SetRing(ring, r, 0.09f); Visuals.SetRing(under, r, 0.085f);
                var c = Palette.Fuego; c.a = 0.35f + 0.6f * t; ring.startColor = ring.endColor = c;
                var u = Palette.Zocalo; u.a = 0.35f + 0.5f * t; under.startColor = under.endColor = u;
            }, () => { Destroy(ring.gameObject); Destroy(under.gameObject); }));
        }

        void Lightning(Vector3 from, Vector3 to, int link)
        {
            var go = new GameObject("Rayo");
            go.transform.SetParent(fxRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = vis.Line(Color.white);
            lr.widthMultiplier = link <= 1 ? 0.14f : 0.1f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            const int n = 7;
            lr.positionCount = n;
            var side = Vector3.Cross((to - from).normalized, Vector3.up);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                var p = Vector3.Lerp(from, to, k);
                if (i > 0 && i < n - 1) p += side * UnityEngine.Random.Range(-0.25f, 0.25f) + Vector3.up * UnityEngine.Random.Range(-0.15f, 0.15f);
                lr.SetPosition(i, p);
            }
            fx.Add(new Fx(0.16f, t => { var c = Palette.Rayo; c.a = 1f - t; lr.startColor = lr.endColor = c; }, () => Destroy(go)));
        }

        void InfernoBeam(SimEvent e)
        {
            if (!beams.TryGetValue(e.TowerId, out var b) || b.Line == null)
            {
                var go = new GameObject("RayoInfernal");
                go.transform.SetParent(fxRoot, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.sharedMaterial = vis.Line(Color.white);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b = new Beam { Line = lr };
                beams[e.TowerId] = b;
            }
            b.TargetId = e.EnemyId;
            b.Stage = e.Int2;
            b.LastShot = Time.time;
            b.From = ToWorld(e.Pos) + Vector3.up * 2.1f;
        }

        void FlameBurst(Vector3 from, Vector3 to, float width)
        {
            var dir = to - from;
            var go = vis.Prim(PrimitiveType.Cube, fxRoot, (from + to) * 0.5f + Vector3.up * 0.8f, new Vector3(Mathf.Max(0.3f, width), 0.4f, dir.magnitude), vis.Unique(Palette.Fuego, true), "Llamarada");
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (dir.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var m = go.GetComponent<Renderer>().sharedMaterial;
            fx.Add(new Fx(0.35f, t =>
            {
                var c = Palette.Fuego; c.a = 0.85f * (1f - t); m.color = c;
                go.transform.localScale = new Vector3(Mathf.Max(0.3f, width) * (1f + 0.4f * t), 0.4f * (1f - t * 0.5f), dir.magnitude);
            }, () => Destroy(go)));
        }

        void AreaBlast(Vector3 pos, float radius, Color color)
        {
            var ring = vis.Ring(fxRoot, 0.1f, color, 0.15f, 0.1f, 48);
            ring.transform.position = pos;
            fx.Add(new Fx(0.25f, t =>
            {
                Visuals.SetRing(ring, Mathf.Lerp(0.2f, radius, t), 0.1f);
                var c = color; c.a = 1f - t; ring.startColor = ring.endColor = c;
            }, () => Destroy(ring.gameObject)));
        }

        void PulseRing(Vector3 pos, float radius)
        {
            var ring = vis.Ring(fxRoot, radius, Palette.Acento, 0.2f, 0.09f);
            ring.transform.position = pos;
            fx.Add(new Fx(0.5f, t => { var c = Palette.Acento; c.a = 1f - t; ring.startColor = ring.endColor = c; }, () => Destroy(ring.gameObject)));
        }

        void Spark(Vector3 pos)
        {
            var m = vis.Unique(Palette.Inmune, true);
            var s = vis.Prim(PrimitiveType.Sphere, fxRoot, pos, Vector3.one * 0.2f, m, "Chispa");
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fx.Add(new Fx(0.18f, t => { s.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 0.9f, t); var c = m.color; c.a = 1f - t; m.color = c; }, () => Destroy(s)));
        }

        void HitBase()
        {
            var origin = baseGo.transform.position;
            fx.Add(new Fx(0.18f, t =>
            {
                for (int i = 0; i < baseMats.Count; i++) baseMats[i].color = Color.Lerp(Palette.Vida, baseColors[i], t);
                baseGo.transform.position = origin + new Vector3(Mathf.Sin(t * 60f) * 0.12f * (1f - t), 0, 0);
            }, () =>
            {
                for (int i = 0; i < baseMats.Count; i++) baseMats[i].color = baseColors[i];
                baseGo.transform.position = origin;
            }));
        }

        void Shrink(GameObject go, float dur)
        {
            if (go == null) return;
            var s0 = go.transform.localScale;
            fx.Add(new Fx(dur, t => { if (go != null) go.transform.localScale = s0 * (1f - t); }, () => { if (go != null) Destroy(go); }));
        }

        void FloatText(Vector3 pos, string text, Color color, float size, float dur)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(fxRoot, false);
            go.transform.position = pos;
            go.transform.rotation = cam.transform.rotation;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(20, 22, 28, 255);
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
            fx.Add(new Fx(dur, t =>
            {
                go.transform.position = pos + Vector3.up * (t * 1.2f);
                var c = color; c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f; tmp.color = c;
            }, () => Destroy(go)));
        }

        // ------------------------------------------------------------------ cuadro a cuadro
        readonly List<int> gone = new List<int>();
        void Update()
        {
            if (cam == null) return;
            var camRot = cam.transform.rotation;

            if (match != null)
            {
                foreach (var e in match.Enemies)
                {
                    if (!enemies.TryGetValue(e.Id, out var v) || v.Root == null) continue;
                    var p = ToWorld(e.Position);
                    var dir = match.RouteOf(e).Direction(e.Distance);
                    v.Root.transform.position = p;
                    v.Body.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z), Vector3.up);
                    if (v.BarRoot != null) v.BarRoot.rotation = camRot;
                    v.HpFill.localScale = new Vector3(Mathf.Clamp01(e.Hp / Mathf.Max(1f, v.MaxHp)), 1f, 1f);
                    if (v.ArmorFill != null) v.ArmorFill.localScale = new Vector3(Mathf.Clamp01(e.Armor / Mathf.Max(1f, v.MaxArmor)), 1f, 1f);
                    if (v.Flame != null)
                    {
                        bool burning = e.Burning;
                        if (v.Flame.activeSelf != burning) v.Flame.SetActive(burning);
                        if (burning) v.Flame.transform.localScale = new Vector3(0.35f, 0.5f + 0.15f * Mathf.Sin(Time.time * 18f + e.Id), 0.35f);
                    }
                    if (v.Flash > 0f) { v.Flash -= Time.deltaTime; v.Tint(v.Flash > 0f ? Color.white : (Color?)null); }
                }
                foreach (var p in match.Projectiles)
                {
                    if (!projectiles.TryGetValue(p.Id, out var go) || go == null) continue;
                    if (p.Kind == ProjectileKind.Lob)
                    {
                        float k = p.Progress;
                        float h = ProjectileHeight + 4.5f * 4f * k * (1f - k);
                        go.transform.position = ToWorld(p.Position) + Vector3.up * h;
                        continue;
                    }
                    var to = ToWorld(p.LastTargetPosition) + Vector3.up * ProjectileHeight;
                    var pos = ToWorld(p.Position) + Vector3.up * ProjectileHeight;
                    go.transform.position = pos;
                    var d = to - pos;
                    if (d.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                }
                // rayo infernal: sigue a su objetivo mientras la torre dispara; ancho y color por etapa (feedback de calentamiento)
                gone.Clear();
                foreach (var kv in beams)
                {
                    var b = kv.Value;
                    var target = match.GetEnemy(b.TargetId);
                    bool live = target != null && target.Alive && Time.time - b.LastShot < 0.25f;
                    if (b.Line == null) { gone.Add(kv.Key); continue; }
                    b.Line.enabled = live;
                    if (!live) continue;
                    var tp = ToWorld(target.Position) + Vector3.up * (target.Layer == Layer.Air ? (target.Type.id == "dragon" ? DragonHeight : FlyerHeight) : 0.8f);
                    b.Line.SetPosition(0, b.From); b.Line.SetPosition(1, tp);
                    float s = Mathf.Clamp01((b.Stage - 1) / 3f);
                    b.Line.widthMultiplier = Mathf.Lerp(0.07f, 0.3f, s) * (1f + 0.15f * Mathf.Sin(Time.time * 30f));
                    var c = Color.Lerp(Palette.Infernal, Palette.Rayo, s); b.Line.startColor = c; b.Line.endColor = Color.Lerp(c, Color.white, 0.4f);
                }
                foreach (var k in gone) beams.Remove(k);
                // fuego en el piso (Doc 04 §6.6): se dibuja desde el estado del núcleo
                gone.Clear();
                foreach (var kv in fireViews) gone.Add(kv.Key);
                foreach (var f in match.Fires)
                {
                    if (!fireViews.TryGetValue(f.Id, out var go))
                    {
                        var a = ToWorld(f.A); var bb = ToWorld(f.B); var dir = bb - a;
                        go = vis.Prim(PrimitiveType.Cube, fxRoot, (a + bb) * 0.5f + Vector3.up * 0.06f, new Vector3(f.HalfWidth * 2f, 0.04f, dir.magnitude), vis.Unique(Palette.Fuego, true), "FuegoEnElPiso");
                        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        if (dir.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                        fireViews[f.Id] = go;
                    }
                    gone.Remove(f.Id);
                    var mat = go.GetComponent<Renderer>().sharedMaterial;
                    var col = Palette.Fuego; col.a = (0.25f + 0.2f * Mathf.Sin(Time.time * 14f + f.Id)) * Mathf.Clamp01(f.TimeLeft / Mathf.Max(0.01f, f.Duration) * 2f);
                    mat.color = col;
                }
                foreach (var k in gone) { if (fireViews[k] != null) Destroy(fireViews[k]); fireViews.Remove(k); }
                foreach (var tv in towers.Values) tv.Animate(Time.time);
            }

            if (highlightOn && highlight != null)
            {
                float r = highlightRadius * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f));
                Visuals.SetRing(highlight, r, 0.08f); Visuals.SetRing(highlightUnder, r, 0.07f);
            }

            for (int i = fx.Count - 1; i >= 0; i--)
            {
                if (i >= fx.Count) continue;
                var f = fx[i];
                f.Elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(f.Elapsed / f.Duration);
                try { f.Step?.Invoke(t); } catch (Exception) { }
                if (t >= 1f) { fx.RemoveAt(i); try { f.End?.Invoke(); } catch (Exception) { } }
            }
        }

        // ------------------------------------------------------------------ tipos internos
        sealed class Fx
        {
            public float Duration, Elapsed;
            public Action<float> Step;
            public Action End;
            public Fx(float d, Action<float> step, Action end) { Duration = Mathf.Max(0.01f, d); Step = step; End = end; }
        }

        sealed class Beam
        {
            public LineRenderer Line;
            public int TargetId, Stage;
            public float LastShot;
            public Vector3 From;
        }

        sealed class EnemyView
        {
            public Enemy Enemy;
            public GameObject Root, Shell, ArmorBar, Flame;
            public Transform Body, BarRoot, HpFill, ArmorFill;
            public float MaxHp, MaxArmor, Flash;
            public bool Flyer;
            readonly List<Renderer> rends = new List<Renderer>();
            readonly List<Color> colors = new List<Color>();

            public void Add(Visuals vis, PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c)
            {
                var go = vis.Prim(t, parent, pos, scale, vis.Unique(c, false));
                Register(go.GetComponent<Renderer>(), c);
            }
            public void Register(Renderer r, Color c) { rends.Add(r); colors.Add(c); }
            public void Unregister(Renderer r) { int i = rends.IndexOf(r); if (i >= 0) { rends.RemoveAt(i); colors.RemoveAt(i); } }
            public void Tint(Color? c)
            {
                for (int i = 0; i < rends.Count; i++) if (rends[i] != null) rends[i].sharedMaterial.color = c ?? colors[i];
            }
        }
    }

    /// <summary>Silueta de una torre con primitivas (UXS-001.2, UXS-002.4): zócalo oscuro + cuerpo por familia; N2 más alta y con banda.</summary>
    public sealed class TowerView
    {
        public Tower Tower;
        public GameObject Root;
        Transform body, aim, orb, coins, fullMark;
        GameObject band;
        Visuals vis;
        TowerTypeData type;
        bool ghost;

        Material Mat(Color c) => ghost ? vis.Unique(Palette.WithAlpha(c, 0.45f), true) : vis.Solid(c);

        public void Build(Visuals v, TowerTypeData t, int level, bool isGhost)
        {
            vis = v; type = t; ghost = isGhost;
            vis.Prim(PrimitiveType.Cylinder, Root.transform, new Vector3(0, 0.08f, 0), new Vector3(t.footprintRadius * 2f, 0.08f, t.footprintRadius * 2f), Mat(Palette.Zocalo), "Zocalo");
            body = new GameObject("Cuerpo").transform; body.SetParent(Root.transform, false);
            Color c = Palette.ForTower(t.id);
            switch (t.id)
            {
                case "arqueras":
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.0f, 0), new Vector3(0.7f, 0.85f, 0.7f), Mat(c));
                    var roof = vis.Prim(PrimitiveType.Cube, body, new Vector3(0, 2.05f, 0), new Vector3(0.62f, 0.62f, 0.62f), Mat(c));
                    roof.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                    break;
                case "canon":
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.45f, 0), new Vector3(1.35f, 0.3f, 1.35f), Mat(c));
                    aim = new GameObject("Caño").transform; aim.SetParent(body, false); aim.localPosition = new Vector3(0, 0.85f, 0);
                    var barrel = vis.Prim(PrimitiveType.Cylinder, aim, new Vector3(0, 0, 0.45f), new Vector3(0.32f, 0.45f, 0.32f), Mat(Palette.Bala));
                    barrel.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    break;
                case "mortero":
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.35f, 0), new Vector3(1.6f, 0.25f, 1.6f), Mat(c));
                    aim = new GameObject("Tubo").transform; aim.SetParent(body, false); aim.localPosition = new Vector3(0, 0.75f, 0);
                    var tube = vis.Prim(PrimitiveType.Cylinder, aim, new Vector3(0, 0.35f, 0.25f), new Vector3(0.6f, 0.45f, 0.6f), Mat(Palette.Bala));
                    tube.transform.localRotation = Quaternion.Euler(40f, 0, 0);
                    break;
                case "bombardera":
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.7f, 0), new Vector3(1.0f, 0.6f, 1.0f), Mat(c));
                    vis.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.55f, 0), new Vector3(0.7f, 0.7f, 0.7f), Mat(Palette.Bala));
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.95f, 0), new Vector3(0.08f, 0.12f, 0.08f), Mat(Palette.Fuego));
                    break;
                case "electrica":
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.9f, 0), new Vector3(0.45f, 0.9f, 0.45f), Mat(Palette.PanelClaro));
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.2f, 0), new Vector3(0.95f, 0.05f, 0.95f), Mat(c));
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.55f, 0), new Vector3(0.8f, 0.05f, 0.8f), Mat(c));
                    orb = vis.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.95f, 0), new Vector3(0.55f, 0.55f, 0.55f), Mat(c)).transform;
                    break;
                case "infernal":
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.7f, 0), new Vector3(1.0f, 0.7f, 1.0f), Mat(Palette.Bala));
                    var crystal = vis.Prim(PrimitiveType.Cube, body, new Vector3(0, 1.85f, 0), new Vector3(0.6f, 0.9f, 0.6f), Mat(c));
                    crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    orb = crystal.transform;
                    break;
                case "oro":
                    vis.Prim(PrimitiveType.Cube, body, new Vector3(0, 0.55f, 0), new Vector3(1.2f, 0.8f, 0.9f), Mat(Palette.Canon * 0.7f));
                    vis.Prim(PrimitiveType.Cube, body, new Vector3(0, 1.0f, 0), new Vector3(1.25f, 0.15f, 0.95f), Mat(c));
                    coins = new GameObject("Monedas").transform; coins.SetParent(body, false); coins.localPosition = new Vector3(0, 1.1f, 0);
                    for (int i = 0; i < 3; i++) vis.Prim(PrimitiveType.Cylinder, coins, new Vector3((i - 1) * 0.3f, 0.06f + (i == 1 ? 0.12f : 0f), 0), new Vector3(0.28f, 0.06f, 0.28f), Mat(c));
                    fullMark = vis.Prim(PrimitiveType.Sphere, body, new Vector3(0, 2.1f, 0), new Vector3(0.45f, 0.45f, 0.45f), Mat(c)).transform;
                    fullMark.gameObject.SetActive(false);
                    break;
                case "lanzallamas":
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.6f, 0), new Vector3(1.05f, 0.5f, 1.05f), Mat(c));
                    vis.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.15f, 0), new Vector3(0.8f, 0.6f, 0.8f), Mat(Palette.Bala));
                    aim = new GameObject("Boquilla").transform; aim.SetParent(body, false); aim.localPosition = new Vector3(0, 1.15f, 0);
                    var noz = vis.Prim(PrimitiveType.Cylinder, aim, new Vector3(0, 0, 0.55f), new Vector3(0.22f, 0.4f, 0.22f), Mat(c));
                    noz.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    break;
                default:
                    vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.75f, 0), new Vector3(0.85f, 0.6f, 0.85f), Mat(c));
                    orb = vis.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.9f, 0), new Vector3(0.6f, 0.6f, 0.6f), Mat(Palette.Orbe)).transform;
                    break;
            }
            SetLevel(level, v);
        }

        public void SetLevel(int level, Visuals v)
        {
            vis = v;
            body.localScale = new Vector3(1f, level >= 2 ? 1.2f : 1f, 1f);
            if (level >= 2 && band == null)
            {
                float y, w;
                switch (type.id)
                {
                    case "canon": y = 0.55f; w = 1.45f; break;
                    case "mortero": y = 0.45f; w = 1.7f; break;
                    case "arqueras": y = 1.2f; w = 0.95f; break;
                    case "oro": y = 0.75f; w = 1.3f; break;
                    default: y = 0.95f; w = 1.1f; break;
                }
                band = vis.Prim(PrimitiveType.Cylinder, body, new Vector3(0, y, 0), new Vector3(w, 0.08f, w), Mat(Palette.Banda), "BandaN2");
            }
        }

        public void Aim(Vector3 target)
        {
            if (aim == null) return;
            var d = target - aim.position; d.y = 0;
            if (d.sqrMagnitude > 1e-4f) aim.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        public void Animate(float time)
        {
            int seed = Tower != null ? Tower.Id : 0;
            if (orb != null)
            {
                if (type.id == "infernal") orb.localRotation = Quaternion.Euler(0f, 45f + time * 40f, 0f);
                else orb.localPosition = new Vector3(0, 1.9f + 0.12f * Mathf.Sin(time * 2.5f + seed), 0);
            }
            if (coins != null && Tower != null)
            {
                float k = Tower.Stats.goldCapacity > 0f ? Mathf.Clamp01(Tower.Stored / Tower.Stats.goldCapacity) : 0f;
                coins.localScale = new Vector3(0.6f + 0.5f * k, 0.3f + 2.2f * k, 0.6f + 0.5f * k);
                bool full = Tower.Full;
                if (fullMark.gameObject.activeSelf != full) fullMark.gameObject.SetActive(full);
                if (full) fullMark.localPosition = new Vector3(0, 2.1f + 0.18f * Mathf.Abs(Mathf.Sin(time * 5f)), 0);
            }
        }
    }
}
