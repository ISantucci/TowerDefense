using System;
using System.Collections.Generic;
using ClashDefense.Core;
using TMPro;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Dibuja el mapa gris (LDS-001.1) y las entidades del núcleo, y traduce los eventos a feedback visible
    /// (UXS-001.1, .2, .4). Solo lee el Match: nunca lo modifica.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        const float FlyerHeight = 1.6f;        // GDS-001.4, supuesto S4 (presentación)
        const float ProjectileHeight = 1.1f;

        Visuals vis;
        Camera cam;
        LevelData level;
        Match match;
        Transform mapRoot, entityRoot, fxRoot;
        GameObject baseGo;
        readonly List<Renderer> baseRenderers = new List<Renderer>();
        readonly List<Material> baseMats = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();
        Vector3 basePos;

        readonly Dictionary<int, EnemyView> enemies = new Dictionary<int, EnemyView>();
        readonly Dictionary<int, TowerView> towers = new Dictionary<int, TowerView>();
        readonly Dictionary<int, GameObject> projectiles = new Dictionary<int, GameObject>();
        readonly List<Fx> fx = new List<Fx>();

        LineRenderer highlight, highlightUnder;
        Vector3 highlightPos; float highlightRadius; bool highlightOn;
        LineRenderer selRing, selRingUnder, previewRing;
        GameObject ghost; string ghostType; LineRenderer ghostRing, ghostRingUnder;

        public Vector3 BasePosition => basePos;

        // ------------------------------------------------------------------ construcción del mapa
        public void BuildMap(Visuals visuals, LevelData lvl, Camera camera)
        {
            vis = visuals; level = lvl; cam = camera;
            mapRoot = new GameObject("Mapa").transform; mapRoot.SetParent(transform, false);
            entityRoot = new GameObject("Entidades").transform; entityRoot.SetParent(transform, false);
            fxRoot = new GameObject("Efectos").transform; fxRoot.SetParent(transform, false);

            var a = lvl.buildArea;
            var areaCenter = new Vector3((a.minX + a.maxX) * 0.5f, -0.05f, (a.minZ + a.maxZ) * 0.5f);
            vis.Prim(PrimitiveType.Cube, mapRoot, areaCenter + new Vector3(0, -0.03f, 0), new Vector3(a.maxX - a.minX + 30f, 0.1f, a.maxZ - a.minZ + 24f), vis.Solid(Palette.FueraDelArea), "FueraDelArea");
            vis.Prim(PrimitiveType.Cube, mapRoot, areaCenter, new Vector3(a.maxX - a.minX, 0.1f, a.maxZ - a.minZ), vis.Solid(Palette.Terreno), "AreaConstruible");

            // camino: un tramo por segmento + juntas redondas (ancho del nivel)
            var path = lvl.path;
            var camino = vis.Solid(Palette.Camino);
            for (int i = 1; i < path.Length; i++)
            {
                var p0 = ToWorld(path[i - 1]); var p1 = ToWorld(path[i]);
                var mid = (p0 + p1) * 0.5f; var dir = p1 - p0;
                var seg = vis.Prim(PrimitiveType.Cube, mapRoot, mid + Vector3.up * 0.012f, new Vector3(lvl.pathWidth, 0.02f, dir.magnitude), camino, "Camino");
                seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            for (int i = 1; i < path.Length - 1; i++)
                vis.Prim(PrimitiveType.Cylinder, mapRoot, ToWorld(path[i]) + Vector3.up * 0.012f, new Vector3(lvl.pathWidth, 0.01f, lvl.pathWidth), camino, "Junta");

            if (lvl.blocked != null)
                foreach (var b in lvl.blocked)
                    vis.Prim(PrimitiveType.Sphere, mapRoot, new Vector3(b.x, 0.2f, b.z), new Vector3(b.radius * 2f, 0.8f, b.radius * 2f), vis.Solid(Palette.Bloqueado), "ZonaBloqueada");

            // entrada: arco oscuro
            var start = ToWorld(path[0]); var startDir = (ToWorld(path[1]) - start).normalized;
            var arch = new GameObject("Entrada").transform; arch.SetParent(mapRoot, false);
            arch.position = start; arch.rotation = Quaternion.LookRotation(startDir, Vector3.up);
            var ent = vis.Solid(Palette.Entrada);
            float half = lvl.pathWidth * 0.5f + 0.3f;
            vis.Prim(PrimitiveType.Cube, arch, new Vector3(-half, 1.1f, 0), new Vector3(0.5f, 2.2f, 0.6f), ent);
            vis.Prim(PrimitiveType.Cube, arch, new Vector3(half, 1.1f, 0), new Vector3(0.5f, 2.2f, 0.6f), ent);
            vis.Prim(PrimitiveType.Cube, arch, new Vector3(0, 2.3f, 0), new Vector3(half * 2f + 0.5f, 0.4f, 0.6f), ent);

            // base: bloque oscuro de piedra con almenas (UXS-001.1)
            basePos = ToWorld(path[path.Length - 1]);
            baseGo = new GameObject("Base");
            baseGo.transform.SetParent(mapRoot, false);
            baseGo.transform.position = basePos;
            float s = lvl.baseRadius * 2f;
            AddBasePart(PrimitiveType.Cube, new Vector3(0, 0.9f, 0), new Vector3(s, 1.8f, s), Palette.Base);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -s * 0.5f : s * 0.5f, z = (i & 2) == 0 ? -s * 0.5f : s * 0.5f;
                AddBasePart(PrimitiveType.Cube, new Vector3(x, 2.05f, z), new Vector3(0.6f, 0.5f, 0.6f), Palette.BaseAlmena);
            }
            var lastDir = (basePos - ToWorld(path[path.Length - 2])).normalized;
            AddBasePart(PrimitiveType.Cube, -lastDir * (s * 0.5f) + new Vector3(0, 0.55f, 0), new Vector3(1.0f, 1.1f, 1.0f), Palette.Entrada);

            // anillos de uso general
            highlightUnder = vis.Ring(fxRoot, 1f, Palette.Zocalo, 0.35f, 0.07f); highlight = vis.Ring(fxRoot, 1f, Palette.Oro, 0.18f, 0.08f);
            SetHighlight(false);
            selRingUnder = vis.Ring(fxRoot, 1f, Palette.Zocalo, 0.22f, 0.06f); selRing = vis.Ring(fxRoot, 1f, Palette.Valido, 0.1f, 0.07f);
            previewRing = vis.Ring(fxRoot, 1f, Palette.Acento, 0.1f, 0.075f);
            ShowSelection(null, 0f); ShowPreview(null, 0f);
        }

        void AddBasePart(PrimitiveType t, Vector3 local, Vector3 scale, Color c)
        {
            var m = vis.Unique(c, false);
            var go = vis.Prim(t, baseGo.transform, local, scale, m);
            baseRenderers.Add(go.GetComponent<Renderer>()); baseMats.Add(m); baseColors.Add(c);
        }

        static Vector3 ToWorld(Vec2 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Límites del contenido para el encuadre de la cámara.</summary>
        public static Bounds ContentBounds(LevelData lvl)
        {
            var a = lvl.buildArea;
            var b = new Bounds(new Vector3((a.minX + a.maxX) * 0.5f, 1f, (a.minZ + a.maxZ) * 0.5f), new Vector3(a.maxX - a.minX, 2.5f, a.maxZ - a.minZ));
            foreach (var p in lvl.path) b.Encapsulate(new Vector3(p.x, 0f, p.z));
            b.Encapsulate(new Vector3(lvl.path[lvl.path.Length - 1].x + lvl.baseRadius, 2.3f, lvl.path[lvl.path.Length - 1].z));
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
            enemies.Clear(); towers.Clear(); projectiles.Clear();
            foreach (var f in fx) f.End?.Invoke();
            fx.Clear();
            ShowSelection(null, 0f); ShowPreview(null, 0f); HideGhost(); SetHighlight(false);
            match = null;
        }

        public void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.EnemySpawned: SpawnEnemy(e.EnemyId); break;
                case SimEventType.EnemyDamaged: if (enemies.TryGetValue(e.EnemyId, out var ed)) ed.Flash = 0.08f; break;
                case SimEventType.AttackImmune:
                    Spark(ToWorld(e.Pos) + Vector3.up * 0.9f);
                    FloatText(ToWorld(e.Pos) + Vector3.up * 1.6f, "INMUNE", Palette.Inmune, 5f, 0.7f);
                    break;
                case SimEventType.ArmorBroken: if (enemies.TryGetValue(e.EnemyId, out var eb)) DropShell(eb); break;
                case SimEventType.EnemyKilled:
                    if (enemies.TryGetValue(e.EnemyId, out var ek)) { KillEnemy(ek, false); enemies.Remove(e.EnemyId); }
                    FloatText(ToWorld(e.Pos) + Vector3.up * 1.2f, "+" + e.Int1, Palette.Oro, 5f, 0.8f);
                    break;
                case SimEventType.EnemyReachedBase:
                    if (enemies.TryGetValue(e.EnemyId, out var ea)) { KillEnemy(ea, true); enemies.Remove(e.EnemyId); }
                    break;
                case SimEventType.BaseDamaged:
                    HitBase();
                    FloatText(basePos + Vector3.up * 3.0f, $"-{e.Int1 - e.Int2} {DisplayName(e.Text)}", Palette.Vida, 6f, 1.2f);
                    break;
                case SimEventType.TowerBuilt: BuildTower(e.TowerId); break;
                case SimEventType.TowerUpgraded: if (towers.TryGetValue(e.TowerId, out var tu)) { tu.SetLevel(2, vis); PulseRing(tu.Root.transform.position, tu.Tower.Stats.range); } break;
                case SimEventType.TowerSold:
                    if (towers.TryGetValue(e.TowerId, out var ts)) { Shrink(ts.Root, 0.2f); towers.Remove(e.TowerId); }
                    FloatText(ToWorld(e.Pos) + Vector3.up * 2.2f, "+" + e.Int1, Palette.Oro, 5f, 0.8f);
                    break;
                case SimEventType.Shot: Shoot(e); break;
                case SimEventType.ProjectileImpact:
                    if (projectiles.TryGetValue(e.Int1, out var pg)) { Destroy(pg); projectiles.Remove(e.Int1); }
                    if (e.Float1 > 0f) AreaBlast(ToWorld(e.Pos), e.Float1);
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
                    var shadow = vis.Prim(PrimitiveType.Cylinder, v.Root.transform, new Vector3(0, 0.03f, 0), new Vector3(0.9f, 0.01f, 0.9f), vis.Unique(Palette.Sombra, true), "SombraAerea");
                    shadow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    break;
                case "blindado":
                    v.Add(vis, PrimitiveType.Capsule, body, new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.45f, 0.5f), Palette.Duende);
                    v.Shell = vis.Prim(PrimitiveType.Cube, body, new Vector3(0, 0.5f, 0), new Vector3(0.85f, 0.75f, 0.85f), vis.Unique(Palette.Metal, false), "Armadura");
                    v.Register(v.Shell.GetComponent<Renderer>(), Palette.Metal);
                    break;
                default:
                    v.Add(vis, PrimitiveType.Capsule, body, new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.45f, 0.5f), c);
                    break;
            }
            float barY = v.Flyer ? FlyerHeight + 0.8f : 1.45f;
            v.BarRoot = new GameObject("Barras").transform; v.BarRoot.SetParent(v.Root.transform, false);
            v.BarRoot.localPosition = new Vector3(0, barY, 0);
            v.HpFill = Bar(v.BarRoot, 0f, Palette.Texto);
            if (v.MaxArmor > 0f) { v.ArmorFill = Bar(v.BarRoot, 0.16f, Palette.Metal); v.ArmorBar = v.ArmorFill.parent.gameObject; }
            enemies[id] = v;
        }

        Transform Bar(Transform parent, float y, Color fill)
        {
            var root = new GameObject("Barra").transform; root.SetParent(parent, false); root.localPosition = new Vector3(0, y, 0);
            var bg = vis.Prim(PrimitiveType.Quad, root, Vector3.zero, new Vector3(0.9f, 0.12f, 1f), vis.Line(Palette.Zocalo));
            bg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pivot = new GameObject("Pivote").transform; pivot.SetParent(root, false); pivot.localPosition = new Vector3(-0.42f, 0, -0.01f);
            var f = vis.Prim(PrimitiveType.Quad, pivot, new Vector3(0.42f, 0, 0), new Vector3(0.84f, 0.07f, 1f), vis.Line(fill));
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
            fx.Add(new Fx(arrived ? 0.35f : 0.2f, t =>
            {
                if (root == null) return;
                if (arrived) root.transform.position = Vector3.Lerp(from, basePos, t);
                root.transform.localScale = baseScale * (1f - t);
            }, () => { if (root != null) Destroy(root); }));
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
            bool on = t != null;
            selRing.gameObject.SetActive(on); selRingUnder.gameObject.SetActive(on);
            if (!on) return;
            var p = ToWorld(t.Position);
            selRing.transform.position = p; selRingUnder.transform.position = p;
            Visuals.SetRing(selRing, range, 0.07f); Visuals.SetRing(selRingUnder, range, 0.06f);
        }

        /// <summary>Anillo del alcance N2 antes de pagar la mejora (UXS-001.3).</summary>
        public void ShowPreview(Tower t, float range)
        {
            bool on = t != null;
            previewRing.gameObject.SetActive(on);
            if (!on) return;
            previewRing.transform.position = ToWorld(t.Position);
            Visuals.SetRing(previewRing, range, 0.075f);
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
                ghostRingUnder = vis.Ring(ghost.transform, type.levels[0].range, Palette.Zocalo, 0.24f, 0.06f);
                ghostRing = vis.Ring(ghost.transform, type.levels[0].range, Palette.Valido, 0.12f, 0.07f);
            }
            ghost.SetActive(true);
            ghost.transform.position = pos;
            ghostRing.startColor = ghostRing.endColor = valid ? Palette.Valido : Palette.Invalido;
        }

        public void HideGhost()
        {
            if (ghost != null) Destroy(ghost);
            ghost = null; ghostType = null;
        }

        // ------------------------------------------------------------------ tutorial (GDS-001.6)
        public void Highlight(string what)
        {
            if (level == null) return;
            switch (what)
            {
                case "base": SetHighlight(true, basePos, level.baseRadius + 1.2f); break;
                case "entrada": SetHighlight(true, ToWorld(level.path[0]) + new Vector3(2f, 0, 0), 2.5f); break;
                case "pista":
                    if (level.tutorialHint != null) SetHighlight(true, new Vector3(level.tutorialHint.x, 0, level.tutorialHint.z), level.tutorialHint.radius);
                    else SetHighlight(false);
                    break;
                default: SetHighlight(false); break;
            }
        }

        void SetHighlight(bool on, Vector3 pos = default, float radius = 1f)
        {
            highlightOn = on; highlightPos = pos; highlightRadius = radius;
            highlight.gameObject.SetActive(on); highlightUnder.gameObject.SetActive(on);
            if (on) { highlight.transform.position = pos; highlightUnder.transform.position = pos; }
        }

        // ------------------------------------------------------------------ combate
        void Shoot(SimEvent e)
        {
            if (towers.TryGetValue(e.TowerId, out var tv) && match != null)
            {
                var target = match.GetEnemy(e.EnemyId);
                if (target != null) tv.Aim(ToWorld(target.Position));
            }
            var go = new GameObject("Proyectil_" + e.Text);
            go.transform.SetParent(fxRoot, false);
            go.transform.position = ToWorld(e.Pos) + Vector3.up * ProjectileHeight;
            switch (e.Text)
            {
                case "arqueras": vis.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.08f, 0.08f, 0.55f), vis.Solid(Palette.Flecha)); break;
                case "canon": vis.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.32f, 0.32f, 0.32f), vis.Solid(Palette.Bala)); break;
                default: vis.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.38f, 0.38f, 0.38f), vis.Solid(Palette.Orbe)); break;
            }
            projectiles[e.Int1] = go;
        }

        void AreaBlast(Vector3 pos, float radius)
        {
            var ring = vis.Ring(fxRoot, 0.1f, Palette.Orbe, 0.15f, 0.1f, 48);
            ring.transform.position = pos;
            fx.Add(new Fx(0.25f, t =>
            {
                Visuals.SetRing(ring, Mathf.Lerp(0.2f, radius, t), 0.1f);
                var c = Palette.Orbe; c.a = 1f - t; ring.startColor = ring.endColor = c;
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
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
            fx.Add(new Fx(dur, t =>
            {
                go.transform.position = pos + Vector3.up * (t * 1.2f);
                var c = color; c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f; tmp.color = c;
            }, () => Destroy(go)));
        }

        // ------------------------------------------------------------------ cuadro a cuadro
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
                    var dir = match.Path.Direction(e.Distance);
                    v.Root.transform.position = p;
                    v.Body.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z), Vector3.up);
                    if (v.BarRoot != null) v.BarRoot.rotation = camRot;
                    v.HpFill.localScale = new Vector3(Mathf.Clamp01(e.Hp / Mathf.Max(1f, v.MaxHp)), 1f, 1f);
                    if (v.ArmorFill != null) v.ArmorFill.localScale = new Vector3(Mathf.Clamp01(e.Armor / Mathf.Max(1f, v.MaxArmor)), 1f, 1f);
                    if (v.Flash > 0f) { v.Flash -= Time.deltaTime; v.Tint(v.Flash > 0f ? Color.white : (Color?)null); }
                }
                foreach (var p in match.Projectiles)
                {
                    if (!projectiles.TryGetValue(p.Id, out var go) || go == null) continue;
                    var to = ToWorld(p.LastTargetPosition) + Vector3.up * ProjectileHeight;
                    var pos = ToWorld(p.Position) + Vector3.up * ProjectileHeight;
                    go.transform.position = pos;
                    var d = to - pos;
                    if (d.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                }
                foreach (var tv in towers.Values) tv.Animate(Time.time);
            }

            if (highlightOn)
            {
                float r = highlightRadius * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f));
                Visuals.SetRing(highlight, r, 0.08f); Visuals.SetRing(highlightUnder, r, 0.07f);
            }

            for (int i = fx.Count - 1; i >= 0; i--)
            {
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

        sealed class EnemyView
        {
            public Enemy Enemy;
            public GameObject Root, Shell, ArmorBar;
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

    /// <summary>Silueta de una torre con primitivas (UXS-001.2): zócalo oscuro + cuerpo por familia; N2 más alta y con banda.</summary>
    public sealed class TowerView
    {
        public Tower Tower;
        public GameObject Root;
        Transform body, aim, orb;
        GameObject band;
        Visuals vis;
        TowerTypeData type;
        bool ghost;

        Material Mat(Color c) => ghost ? vis.Unique(Palette.WithAlpha(c, 0.45f), true) : vis.Solid(c);

        public void Build(Visuals v, TowerTypeData t, int level, bool isGhost)
        {
            vis = v; type = t; ghost = isGhost;
            var plinth = vis.Prim(PrimitiveType.Cylinder, Root.transform, new Vector3(0, 0.08f, 0), new Vector3(t.footprintRadius * 2f, 0.08f, t.footprintRadius * 2f), Mat(Palette.Zocalo), "Zocalo");
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
                float y = type.id == "canon" ? 0.55f : type.id == "arqueras" ? 1.2f : 0.95f;
                float w = type.id == "canon" ? 1.45f : 0.95f;
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
            if (orb != null) orb.localPosition = new Vector3(0, 1.9f + 0.12f * Mathf.Sin(time * 2.5f + (Tower != null ? Tower.Id : 0)), 0);
        }
    }
}
