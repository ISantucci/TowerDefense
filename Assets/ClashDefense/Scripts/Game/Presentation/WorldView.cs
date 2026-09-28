using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Dibuja las entidades del núcleo sobre la escena del nivel y traduce los eventos a feedback visible
    /// (UXS-001.1, .2, .4 y UXS-002.4, .5). Solo lee el Match: nunca lo modifica.
    /// TL-003: el mapa lo dibuja la escena del nivel (LevelView); acá quedan torres, enemigos, proyectiles y efectos,
    /// todos desde prefabs y reciclados (no se crea ni se destruye nada por disparo), con materiales compartidos.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        const float ProjectileHeight = 1.1f;

        PresentationLibrary lib;
        Camera cam;
        LevelData level;
        LevelStage stage;
        Match match;
        BalanceDefinition visuals;
        Transform entityRoot, fxRoot, poolRoot;
        Vector3 basePos, firstSpawn;

        readonly Dictionary<int, EnemyVisual> enemies = new Dictionary<int, EnemyVisual>();
        readonly Dictionary<int, TowerVisual> towers = new Dictionary<int, TowerVisual>();
        readonly Dictionary<int, ProjectileVisual> projectiles = new Dictionary<int, ProjectileVisual>();
        readonly Dictionary<int, MeshFx> fires = new Dictionary<int, MeshFx>();
        readonly Dictionary<int, Beam> beams = new Dictionary<int, Beam>();

        // reservas por prefab
        readonly Dictionary<Component, object> pools = new Dictionary<Component, object>();
        readonly Dictionary<Component, Component> origin = new Dictionary<Component, Component>();

        // efectos vivos
        readonly List<EnemyVisual> dying = new List<EnemyVisual>();
        readonly List<FloatingText> texts = new List<FloatingText>();
        readonly List<RingFx> rings = new List<RingFx>();
        readonly List<MeshFx> meshes = new List<MeshFx>();
        readonly List<LineFx> lines = new List<LineFx>();
        readonly List<TowerAnim> towerAnims = new List<TowerAnim>();

        // anillos fijos
        RingFx highlight, selRing, selMinRing, previewRing;
        Vector3 highlightPos; float highlightRadius; bool highlightOn;
        // fantasma de colocación: uno por tipo de torre, reusado
        readonly Dictionary<string, Ghost> ghosts = new Dictionary<string, Ghost>();
        Ghost ghost;
        // base
        float baseHit = -1f; Vector3 baseOrigin; MaterialPropertyBlock baseMpb;
        readonly List<int> gone = new List<int>();

        public Vector3 BasePosition => basePos;

        struct TowerAnim { public TowerVisual V; public float Elapsed, Duration; public bool Shrink; public int Id; }

        sealed class Beam { public LineFx Line; public int TargetId, Stage; public float LastShot; public Vector3 From; }

        sealed class Ghost { public TowerVisual Visual; public RingFx Range, Min, Foot; }

        // ------------------------------------------------------------------ arranque
        public void Init(PresentationLibrary library, Camera camera, LevelStage levelStage)
        {
            lib = library; cam = camera; stage = levelStage;
            entityRoot = new GameObject("Entidades").transform; entityRoot.SetParent(transform, false);
            fxRoot = new GameObject("Efectos").transform; fxRoot.SetParent(transform, false);
            poolRoot = new GameObject("Reserva").transform; poolRoot.SetParent(transform, false);
            baseMpb = new MaterialPropertyBlock();
            highlight = StaticRing("Resaltado"); selRing = StaticRing("Seleccion"); selMinRing = StaticRing("SeleccionMinimo"); previewRing = StaticRing("VistaPrevia");
            SetHighlight(false); ShowSelection(null, 0f); ShowPreview(null, 0f);
        }

        RingFx StaticRing(string name)
        {
            var r = Instantiate(lib.ring, fxRoot);
            r.name = name;
            r.gameObject.SetActive(false);
            return r;
        }

        /// <summary>El nivel que se ve: de acá salen la base, la entrada y la pista del tutorial.</summary>
        public void SetLevel(LevelData lvl)
        {
            level = lvl;
            var routes = LevelGeometry.RoutePoints(lvl);
            basePos = routes.Count > 0 ? ToWorld(routes[0][routes[0].Length - 1]) : Vector3.zero;
            var spawns = LevelGeometry.Spawns(lvl);
            firstSpawn = spawns.Count > 0 ? ToWorld(spawns[0]) : Vector3.zero;
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

        // ------------------------------------------------------------------ reservas
        T Take<T>(T prefab, Transform parent) where T : Component
        {
            if (prefab == null) return null;
            if (!pools.TryGetValue(prefab, out var p)) { p = new PrefabPool<T>(prefab, poolRoot); pools[prefab] = p; }
            var x = ((PrefabPool<T>)p).Get();
            if (parent != null && x.transform.parent != parent) x.transform.SetParent(parent, false);
            origin[x] = prefab;
            return x;
        }

        void Give<T>(T x) where T : Component
        {
            if (x == null) return;
            if (origin.TryGetValue(x, out var prefab) && pools.TryGetValue(prefab, out var p)) ((PrefabPool<T>)p).Release(x);
            else Destroy(x.gameObject);
        }

        // ------------------------------------------------------------------ partida
        public void Bind(Match m, BalanceDefinition visualsFrom)
        {
            Clear();
            match = m;
            visuals = visualsFrom;
        }

        public void Clear()
        {
            foreach (var e in enemies.Values) Give(e);
            foreach (var e in dying) Give(e);
            foreach (var t in towers.Values) Give(t);
            foreach (var a in towerAnims) if (a.Shrink) Give(a.V);
            foreach (var p in projectiles.Values) Give(p);
            foreach (var f in fires.Values) Give(f);
            foreach (var b in beams.Values) Give(b.Line);
            foreach (var t in texts) Give(t);
            foreach (var r in rings) Give(r);
            foreach (var m in meshes) Give(m);
            foreach (var l in lines) Give(l);
            enemies.Clear(); dying.Clear(); towers.Clear(); towerAnims.Clear(); projectiles.Clear(); fires.Clear(); beams.Clear();
            texts.Clear(); rings.Clear(); meshes.Clear(); lines.Clear();
            EndBaseHit();
            if (selRing != null) { ShowSelection(null, 0f); ShowPreview(null, 0f); SetHighlight(false); }
            HideGhost();
            match = null;
        }

        public void OnEvent(SimEvent e)
        {
            if (match == null) return;
            switch (e.Type)
            {
                case SimEventType.EnemySpawned: SpawnEnemy(e.EnemyId); break;
                case SimEventType.EnemyDamaged: if (enemies.TryGetValue(e.EnemyId, out var ed)) ed.Flash(e.Int1 == 1 ? 0.03f : 0.08f); break;
                case SimEventType.AttackImmune:
                    Spark(ToWorld(e.Pos) + Vector3.up * 0.9f);
                    FloatText(ToWorld(e.Pos) + Vector3.up * 1.6f, e.Int2 == 1 ? "CALENTANDO" : "INMUNE", lib.immune, 5f, 0.7f);
                    break;
                case SimEventType.ArmorBroken:
                    if (enemies.TryGetValue(e.EnemyId, out var eb))
                    {
                        var shell = eb.BreakShell();
                        if (shell != null && lib.armorShell != null)
                        {
                            var r = shell.GetComponentInChildren<Renderer>(true);
                            var fx = Take(lib.armorShell, fxRoot);
                            fx.Shell(shell, r != null && r.sharedMaterial != null ? r.sharedMaterial.color : Color.gray);
                            meshes.Add(fx);
                        }
                    }
                    break;
                case SimEventType.EnemyKilled:
                    if (enemies.TryGetValue(e.EnemyId, out var ek))
                    {
                        bool boss = ek.Enemy != null && ek.Enemy.Type.miniboss;
                        var from = ek.transform.position;
                        ek.BeginDeath(false, basePos, boss);
                        dying.Add(ek);
                        enemies.Remove(e.EnemyId);
                        if (boss) { AreaBlast(from, 3.5f, lib.gold); AreaBlast(from, 2.2f, lib.text); }
                    }
                    FloatText(ToWorld(e.Pos) + Vector3.up * 1.2f, FloatingText.Plus(e.Int1), lib.gold, e.Int1 >= 100 ? 9f : 5f, 0.8f);
                    break;
                case SimEventType.EnemyReachedBase:
                    if (enemies.TryGetValue(e.EnemyId, out var ea)) { ea.BeginDeath(true, basePos, false); dying.Add(ea); enemies.Remove(e.EnemyId); }
                    break;
                case SimEventType.BaseDamaged:
                    HitBase();
                    FloatText(basePos + Vector3.up * 3.0f, $"-{e.Int1 - e.Int2} {DisplayName(e.Text)}", lib.life, 6f, 1.2f);
                    break;
                case SimEventType.TowerBuilt: BuildTower(e.TowerId); break;
                case SimEventType.TowerUpgraded:
                    if (towers.TryGetValue(e.TowerId, out var tu)) { tu.SetLevel(2); PulseRing(tu.transform.position, Mathf.Max(1.5f, tu.Tower.Stats.range)); }
                    break;
                case SimEventType.TowerSold:
                    if (towers.TryGetValue(e.TowerId, out var ts)) { towerAnims.Add(new TowerAnim { V = ts, Duration = 0.2f, Shrink = true, Id = e.TowerId }); towers.Remove(e.TowerId); }
                    if (beams.TryGetValue(e.TowerId, out var bs)) { Give(bs.Line); beams.Remove(e.TowerId); }
                    FloatText(ToWorld(e.Pos) + Vector3.up * 2.2f, FloatingText.Plus(e.Int1), lib.gold, 5f, 0.8f);
                    break;
                case SimEventType.Shot: Shoot(e); break;
                case SimEventType.ProjectileImpact:
                    if (projectiles.TryGetValue(e.Int1, out var pv)) { Give(pv); projectiles.Remove(e.Int1); }
                    if (e.Float1 > 0f) AreaBlast(ToWorld(e.Pos), e.Float1, e.Text == "mortero" ? lib.fire : e.Text == "bombardera" ? lib.bomb : lib.orb);
                    break;
                case SimEventType.GoldCollected:
                    FloatText(ToWorld(e.Pos) + Vector3.up * 2.4f, FloatingText.Plus(e.Int1), lib.gold, 7f, 1.0f);
                    break;
                case SimEventType.GoldStoredFull:
                    FloatText(ToWorld(e.Pos) + Vector3.up * 2.6f, "¡LLENA!", lib.gold, 5f, 1.2f);
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
            var def = visuals != null ? visuals.Enemy(e.Type.id) : null;
            if (def == null || def.prefab == null) { Debug.LogWarning($"[ClashDefense] El enemigo '{e.Type.id}' no tiene prefab en el balance {visuals?.name}"); return; }
            var v = Take(def.prefab, entityRoot);
            v.name = $"Enemigo_{e.Type.id}_{id}";
            v.transform.position = ToWorld(e.Position);
            v.Bind(e);
            enemies[id] = v;
        }

        // ------------------------------------------------------------------ torres
        void BuildTower(int id)
        {
            var t = match.GetTower(id);
            if (t == null) return;
            var def = visuals != null ? visuals.Tower(t.Type.id) : null;
            if (def == null || def.prefab == null) { Debug.LogWarning($"[ClashDefense] La torre '{t.Type.id}' no tiene prefab en el balance {visuals?.name}"); return; }
            var v = Take(def.prefab, entityRoot);
            v.name = $"Torre_{t.Type.id}_{id}";
            v.transform.SetPositionAndRotation(ToWorld(t.Position), Quaternion.identity);
            v.Bind(t);
            v.transform.localScale = new Vector3(1f, 0.2f, 1f);
            towers[id] = v;
            towerAnims.Add(new TowerAnim { V = v, Duration = 0.15f, Id = id });
        }

        public int PickTower(Vector3 world, float extra = 0.4f)
        {
            if (match == null) return 0;
            int best = 0; float bestD = float.MaxValue;
            var list = match.Towers;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                float dx = world.x - t.Position.x, dz = world.z - t.Position.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d <= t.Type.footprintRadius + extra && d < bestD) { bestD = d; best = t.Id; }
            }
            return best;
        }

        public void ShowSelection(Tower t, float range)
        {
            bool on = t != null && range > 0f;
            selRing.gameObject.SetActive(on);
            bool min = on && t.Stats.minRange > 0f;
            selMinRing.gameObject.SetActive(min);
            if (!on) return;
            var p = ToWorld(t.Position);
            selRing.ShowStatic(p, range, lib.valid, 0.1f, true, lib.edge, 0.22f, 0.07f);
            if (min) selMinRing.ShowStatic(p, t.Stats.minRange, lib.invalid, 0.08f, true, lib.edge, 0.18f, 0.07f);
        }

        /// <summary>Anillo del alcance N2 antes de pagar la mejora (UXS-001.3).</summary>
        public void ShowPreview(Tower t, float range)
        {
            bool on = t != null && range > 0f;
            previewRing.gameObject.SetActive(on);
            if (!on) return;
            previewRing.ShowStatic(ToWorld(t.Position), range, lib.accent, 0.1f, true, lib.edge, 0.2f, 0.075f);
        }

        // ------------------------------------------------------------------ fantasma de colocación (UXS-001.2)
        public void ShowGhost(TowerTypeData type, Vector3 pos, bool valid)
        {
            if (ghost == null || ghost.Visual == null || !ghosts.ContainsKey(type.id) || ghosts[type.id] != ghost)
            {
                HideGhost();
                if (!ghosts.TryGetValue(type.id, out ghost)) { ghost = MakeGhost(type); ghosts[type.id] = ghost; }
            }
            if (ghost == null) return;
            ghost.Visual.gameObject.SetActive(true);
            ghost.Visual.transform.position = pos;
            var col = valid ? lib.valid : lib.invalid;
            if (ghost.Range != null) ghost.Range.SetColor(col);
            ghost.Foot.SetColor(col);
        }

        Ghost MakeGhost(TowerTypeData type)
        {
            var def = visuals != null ? visuals.Tower(type.id) : null;
            if (def == null || def.prefab == null) return null;
            var g = new Ghost { Visual = Instantiate(def.prefab, fxRoot) };
            g.Visual.name = "Fantasma_" + type.id;
            g.Visual.MakeGhost(lib.ghostMaterial, 0.45f);
            var root = g.Visual.transform;
            float r = type.levels[0].range;
            if (r > 0f) { g.Range = Instantiate(lib.ring, root); g.Range.ShowStatic(root.position, r, lib.valid, 0.12f, true, lib.edge, 0.24f, 0.07f); }
            // cada anillo de color lleva debajo un borde oscuro: el rojo y el blanco no se leen solos sobre el terreno (UXS-002.4, Ley 2)
            if (type.levels[0].minRange > 0f) { g.Min = Instantiate(lib.ring, root); g.Min.ShowStatic(root.position, type.levels[0].minRange, lib.invalid, 0.08f, true, lib.edge, 0.18f, 0.07f); }
            // el pie de la torre siempre marca válido o inválido, aunque no tenga alcance (Oro)
            g.Foot = Instantiate(lib.ring, root); g.Foot.name = "Pie";
            g.Foot.ShowStatic(root.position, type.footprintRadius + 0.15f, lib.valid, 0.1f, true, lib.edge, 0.2f, 0.07f);
            foreach (var ring in root.GetComponentsInChildren<RingFx>(true)) ring.transform.localPosition = Vector3.zero;
            return g;
        }

        public void HideGhost()
        {
            if (ghost != null && ghost.Visual != null) ghost.Visual.gameObject.SetActive(false);
            ghost = null;
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
            highlight.gameObject.SetActive(on);
            if (on) highlight.ShowStatic(pos, radius, lib.gold, 0.18f, true, lib.edge, 0.35f, 0.08f);
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
            var def = visuals != null ? visuals.Tower(e.Text) : null;
            if (def == null || def.projectile == null) return;
            var go = Take(def.projectile, fxRoot);
            go.transform.SetPositionAndRotation(ToWorld(e.Pos) + Vector3.up * ProjectileHeight, Quaternion.identity);
            projectiles[e.Int1] = go;
            if (e.Text == "mortero")
            {
                var tower = match.GetTower(e.TowerId);
                ImpactMarker(ToWorld(e.Aim), e.Float1, tower != null ? tower.Stats.areaRadius : 2f);
            }
        }

        void ImpactMarker(Vector3 at, float seconds, float radius)
        {
            var r = Take(lib.ring, fxRoot);
            r.Impact(at, radius, seconds, lib.fire, lib.edge);
            rings.Add(r);
        }

        void Lightning(Vector3 from, Vector3 to, int link)
        {
            var l = Take(lib.lightning, fxRoot);
            l.Lightning(from, to, link, lib.lightningColor);
            lines.Add(l);
        }

        void InfernoBeam(SimEvent e)
        {
            if (!beams.TryGetValue(e.TowerId, out var b) || b.Line == null)
            {
                b = new Beam { Line = Take(lib.infernoBeam, fxRoot) };
                beams[e.TowerId] = b;
            }
            b.TargetId = e.EnemyId;
            b.Stage = e.Int2;
            b.LastShot = Time.time;
            b.From = ToWorld(e.Pos) + Vector3.up * 2.1f;
        }

        void FlameBurst(Vector3 from, Vector3 to, float width)
        {
            var m = Take(lib.flameBurst, fxRoot);
            m.Flame(from, to, width, lib.fire);
            meshes.Add(m);
        }

        void AreaBlast(Vector3 pos, float radius, Color color)
        {
            var r = Take(lib.ring, fxRoot);
            r.Blast(pos, radius, color);
            rings.Add(r);
        }

        void PulseRing(Vector3 pos, float radius)
        {
            var r = Take(lib.ring, fxRoot);
            r.Pulse(pos, radius, lib.accent);
            rings.Add(r);
        }

        void Spark(Vector3 pos)
        {
            var m = Take(lib.spark, fxRoot);
            m.Spark(pos, lib.immune);
            meshes.Add(m);
        }

        void FloatText(Vector3 pos, string text, Color color, float size, float dur)
        {
            var t = Take(lib.floatingText, fxRoot);
            t.Play(pos, cam != null ? cam.transform.rotation : Quaternion.identity, text, color, size, dur);
            texts.Add(t);
        }

        void HitBase()
        {
            var view = stage != null ? stage.View : null;
            if (view == null || view.BaseTransform == null) return;
            if (baseHit < 0f) baseOrigin = view.BaseTransform.position;
            baseHit = 0f;
        }

        void EndBaseHit()
        {
            if (baseHit < 0f) return;
            baseHit = -1f;
            var view = stage != null ? stage.View : null;
            if (view == null || view.BaseTransform == null) return;
            view.BaseTransform.position = baseOrigin;
            var rends = view.BaseRenderers;
            for (int i = 0; i < rends.Count; i++) if (rends[i] != null) rends[i].SetPropertyBlock(null);
        }

        void TickBaseHit(float dt)
        {
            if (baseHit < 0f) return;
            var view = stage != null ? stage.View : null;
            if (view == null || view.BaseTransform == null) { baseHit = -1f; return; }
            baseHit += dt;
            float t = Mathf.Clamp01(baseHit / 0.18f);
            if (t >= 1f) { EndBaseHit(); return; }
            var rends = view.BaseRenderers;
            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i];
                if (r == null) continue;
                var orig = r.sharedMaterial != null ? r.sharedMaterial.color : Color.white;
                baseMpb.SetColor(TowerVisual.ColorId, Color.Lerp(lib.life, orig, t));
                r.SetPropertyBlock(baseMpb);
            }
            view.BaseTransform.position = baseOrigin + new Vector3(Mathf.Sin(t * 60f) * 0.12f * (1f - t), 0, 0);
        }

        // ------------------------------------------------------------------ cuadro a cuadro
        void Update()
        {
            if (cam == null) return;
            float dt = Time.deltaTime;
            float time = Time.time;
            var camRot = cam.transform.rotation;

            if (match != null)
            {
                // recorridos por índice: un foreach sobre IReadOnlyList crea basura en cada cuadro (Vaultrum: GC Alloc por frame)
                var enemyList = match.Enemies;
                for (int i = 0; i < enemyList.Count; i++)
                {
                    var e = enemyList[i];
                    if (!enemies.TryGetValue(e.Id, out var v)) continue;
                    var d = match.RouteOf(e).Direction(e.Distance);
                    v.Tick(ToWorld(e.Position), new Vector3(d.x, 0f, d.z), camRot, time);
                }
                var projList = match.Projectiles;
                for (int i = 0; i < projList.Count; i++)
                {
                    var p = projList[i];
                    if (!projectiles.TryGetValue(p.Id, out var go)) continue;
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
                    var dir = to - pos;
                    if (go.FaceDirection && dir.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
                // rayo infernal: sigue a su objetivo mientras la torre dispara; ancho y color por etapa (feedback de calentamiento)
                gone.Clear();
                foreach (var kv in beams)
                {
                    var b = kv.Value;
                    if (b.Line == null) { gone.Add(kv.Key); continue; }
                    var target = match.GetEnemy(b.TargetId);
                    bool live = target != null && target.Alive && time - b.LastShot < 0.25f;
                    if (!live) { b.Line.Hide(); continue; }
                    float aimY = enemies.TryGetValue(target.Id, out var tv) ? tv.AimHeight : 0.8f;
                    float s = Mathf.Clamp01((b.Stage - 1) / 3f);
                    var c = Color.Lerp(lib.infernoColor, lib.lightningColor, s);
                    b.Line.Beam(b.From, ToWorld(target.Position) + Vector3.up * aimY, Mathf.Lerp(0.07f, 0.3f, s) * (1f + 0.15f * Mathf.Sin(time * 30f)), c);
                }
                foreach (var k in gone) beams.Remove(k);
                // fuego en el piso (Doc 04 §6.6): se dibuja desde el estado del núcleo
                gone.Clear();
                foreach (var kv in fires) gone.Add(kv.Key);
                var fireList = match.Fires;
                for (int i = 0; i < fireList.Count; i++)
                {
                    var f = fireList[i];
                    if (!fires.TryGetValue(f.Id, out var fx))
                    {
                        fx = Take(lib.fireOnGround, fxRoot);
                        fx.Fire(ToWorld(f.A), ToWorld(f.B), f.HalfWidth, lib.fire);
                        fires[f.Id] = fx;
                    }
                    gone.Remove(f.Id);
                    fx.FireAlpha((0.25f + 0.2f * Mathf.Sin(time * 14f + f.Id)) * Mathf.Clamp01(f.TimeLeft / Mathf.Max(0.01f, f.Duration) * 2f));
                }
                foreach (var k in gone) { Give(fires[k]); fires.Remove(k); }
                foreach (var tv in towers.Values) tv.Animate(time);
            }

            for (int i = towerAnims.Count - 1; i >= 0; i--)
            {
                var a = towerAnims[i];
                a.Elapsed += dt;
                float k = Mathf.Clamp01(a.Elapsed / a.Duration);
                if (a.V != null)
                {
                    if (a.Shrink) a.V.transform.localScale = Vector3.one * (1f - k);
                    else a.V.transform.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, k), 1f);
                }
                if (k >= 1f)
                {
                    if (a.Shrink) Give(a.V); else if (a.V != null) a.V.transform.localScale = Vector3.one;
                    towerAnims.RemoveAt(i);
                }
                else towerAnims[i] = a;
            }

            for (int i = dying.Count - 1; i >= 0; i--)
                if (!dying[i].TickDeath(dt)) { Give(dying[i]); dying.RemoveAt(i); }
            for (int i = texts.Count - 1; i >= 0; i--)
                if (!texts[i].Tick(dt)) { Give(texts[i]); texts.RemoveAt(i); }
            for (int i = rings.Count - 1; i >= 0; i--)
                if (!rings[i].Tick(dt)) { Give(rings[i]); rings.RemoveAt(i); }
            for (int i = meshes.Count - 1; i >= 0; i--)
                if (!meshes[i].Tick(dt)) { Give(meshes[i]); meshes.RemoveAt(i); }
            for (int i = lines.Count - 1; i >= 0; i--)
                if (!lines[i].Tick(dt)) { Give(lines[i]); lines.RemoveAt(i); }
            TickBaseHit(dt);

            if (highlightOn && highlight != null)
                highlight.SetRadius(highlightRadius * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f)), 0.08f);
        }
    }
}
