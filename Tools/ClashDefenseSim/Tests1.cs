using System;
using System.Collections.Generic;
using System.Linq;
using ClashDefense.Core;

namespace ClashDefense.Sim
{
    /// <summary>Pruebas del núcleo del Mundo 1 contra GDS-002.0 … GDS-002.6 y LDS-002.6. Se corren con las del P0 ("test").</summary>
    static class Tests1
    {
        const float Dt = Match.Step;
        static int passed, failed;
        public static readonly List<string> Failures = new List<string>();
        public static int Passed => passed;
        public static int Failed => failed;

        static void Check(bool ok, string name, string detail = "")
        {
            if (ok) passed++; else { failed++; Failures.Add(name + " — " + detail); }
            Console.WriteLine($"  [{(ok ? "OK" : "FALLA")}] {name}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");
        }

        static bool Near(float a, float b, float tol) => Math.Abs(a - b) <= tol;
        static BalanceData B() => World1.Balance();

        /// <summary>Nivel de prueba: un camino recto de 60 u de oeste a este (z = 0) y un área amplia.</summary>
        static LevelData Straight(string seq, float interval = 1f, string lanes = null, bool twoRoutes = false)
        {
            var l = new LevelData
            {
                id = "prueba", displayName = "prueba", pathWidth = 2f, baseRadius = 1f,
                buildArea = new RectData { minX = -40, minZ = -20, maxX = 40, maxZ = 20 },
                routes = twoRoutes
                    ? new[] { new RouteData { points = new[] { new Vec2(-30, 0), new Vec2(30, 0) } },
                              new RouteData { points = new[] { new Vec2(-30, 0), new Vec2(-30, 10), new Vec2(30, 10), new Vec2(30, 0) } } }
                    : new[] { new RouteData { points = new[] { new Vec2(-30, 0), new Vec2(30, 0) } } },
                waves = new[] { new WaveData { sequence = seq, spawnInterval = interval, lanes = lanes } },
                blocked = new CircleData[0],
            };
            return l;
        }

        static Match Started(LevelData l, List<SimEvent> log, BalanceData b = null, MatchOptions o = null)
        {
            var m = new Match(b ?? B(), l, o ?? new MatchOptions());
            m.Begin();
            m.DrainEvents(log);
            return m;
        }

        static void Run(Match m, List<SimEvent> log, Func<bool> stop, float max = 600f)
        {
            float end = m.ActiveTime + max;
            while (!m.Ended && !stop() && m.ActiveTime < end) { m.Tick(); m.DrainEvents(log); }
        }

        static void ToWave(Match m, List<SimEvent> log) => Run(m, log, () => m.State == MatchState.Wave, 10f);

        public static int RunAll()
        {
            Console.WriteLine("\nPruebas del núcleo — Mundo 1\n");

            // ---------------- datos
            {
                var b = B();
                var errs = DataValidator.ValidateBalance(b);
                foreach (var id in World1.Levels) errs.AddRange(DataValidator.ValidateLevel(b, World1.Level(id)));
                errs.AddRange(Progression.Validate(World1.Campaign(), b));
                Check(errs.Count == 0, "GDS-002.0 balance, seis niveles y campaña del Mundo 1 validan", string.Join(" | ", errs));
                var p0 = DataLoader.Balance();
                string[] same = { "arqueras", "canon", "mago" };
                bool equal = same.All(id =>
                {
                    var a = p0.towers.First(t => t.id == id); var c = b.towers.First(t => t.id == id);
                    return a.cost == c.cost && a.metalEfficiency == c.metalEfficiency && a.levels.Zip(c.levels, (x, y) => x.damage == y.damage && x.interval == y.interval && x.range == y.range && x.areaRadius == y.areaRadius).All(z => z);
                }) && new[] { "D", "E", "V", "A" }.All(code =>
                {
                    var a = p0.enemies.First(e => e.code == code); var c = b.enemies.First(e => e.code == code);
                    return a.hp == c.hp && a.armor == c.armor && a.travelTime == c.travelTime && a.baseDamage == c.baseDamage && a.gold == c.gold;
                });
                Check(equal, "PRJ-001 las torres iniciales y los cuatro enemigos del Doc 05 no cambiaron en el Mundo 1");
                var bad = new LevelData { id = "x", routes = new[] { new RouteData { points = new[] { new Vec2(0, 0), new Vec2(5, 0) } }, new RouteData { points = new[] { new Vec2(0, 0), new Vec2(0, 5) } } },
                    pathWidth = 2, baseRadius = 1, buildArea = new RectData { minX = -1, maxX = 1, minZ = -1, maxZ = 1 }, waves = new[] { new WaveData { sequence = "D Z", spawnInterval = 1, lanes = "0 3" } } };
                var be = DataValidator.ValidateLevel(b, bad);
                Check(be.Any(e => e.Contains("no termina en la base")) && be.Any(e => e.Contains("'Z'")) && be.Any(e => e.Contains("lane '3'")), "GDS-002.6 un nivel mal armado se reporta", string.Join(" | ", be));
            }

            // ---------------- oleadas: lugar vacío y reparto entre recorridos
            {
                var log = new List<SimEvent>();
                var m = Started(Straight("D . . D"), log);
                Run(m, log, () => log.Count(e => e.Type == SimEventType.EnemySpawned) >= 2, 20f);
                var sp = log.Where(e => e.Type == SimEventType.EnemySpawned).ToList();
                Check(sp.Count == 2 && Near(sp[1].Time - sp[0].Time, 3f, Dt * 1.5f), "LDS-002.6 '.' deja un lugar vacío (D . . D = 3 intervalos)", sp.Count > 1 ? $"{sp[1].Time - sp[0].Time:0.00} s" : "");
                var log2 = new List<SimEvent>();
                var m2 = Started(Straight("D x8", 0.5f, "0 x2 1 x2", true), log2);
                Run(m2, log2, () => log2.Count(e => e.Type == SimEventType.EnemySpawned) >= 8, 20f);
                var lanes = string.Join("", log2.Where(e => e.Type == SimEventType.EnemySpawned).Select(e => e.Int2));
                Check(lanes == "00110011", "Doc 03 §9 reparto 2/2 entre recorridos, determinista", lanes);
                // prioridad por distancia restante en SU recorrido: el del recorrido corto va primero aunque haya caminado menos
                var arch = Started(Straight("D D", 3f, "1 0", true), new List<SimEvent>());
                ToWave(arch, new List<SimEvent>());
                var l3 = new List<SimEvent>();
                Run(arch, l3, () => arch.Enemies.Count == 2 && arch.Enemies.All(e => e.Distance > 12f), 20f);
                var eLong = arch.Enemies.First(e => e.Route == 1); var eShort = arch.Enemies.First(e => e.Route == 0);
                Check(eLong.Distance > eShort.Distance && eShort.Remaining < eLong.Remaining, "Doc 03 §14 la distancia restante se mide sobre el recorrido asignado", $"largo d={eLong.Distance:0.0} r={eLong.Remaining:0.0} · corto d={eShort.Distance:0.0} r={eShort.Remaining:0.0}");
                Check(Near(eLong.Speed, eShort.Speed, 1e-4f), "GDS-002.6 la velocidad sale del recorrido de referencia, igual en todos");
            }

            // ---------------- Mortero
            {
                var log = new List<SimEvent>();
                var l = Straight("D", 1f);
                var b = B(); b.economy.startGold = 1000;
                var m = Started(l, log, b);
                m.TryBuild("mortero", new Vec2(-28f, 2.5f), out var mo, out var r0);   // pegado a la entrada: el Duende nace dentro de la distancia mínima
                ToWave(m, log);
                Run(m, log, () => log.Any(e => e.Type == SimEventType.Shot), 30f);
                var shot = log.FirstOrDefault(e => e.Type == SimEventType.Shot);
                var target = shot.EnemyId != 0 ? m.GetEnemy(shot.EnemyId) : null;
                float dAtShot = Vec2.Distance(new Vec2(-28f, 2.5f), shot.Aim);
                Check(mo != null && shot.Type == SimEventType.Shot && dAtShot >= mo.Stats.minRange - 0.05f, "Doc 04 §6.1 el Mortero no dispara dentro de su distancia mínima", $"primer tiro a {dAtShot:0.00} u (mín {mo?.Stats.minRange})");
                Run(m, log, () => log.Any(e => e.Type == SimEventType.ProjectileImpact), 5f);
                var imp = log.First(e => e.Type == SimEventType.ProjectileImpact);
                Check(Near(imp.Time - shot.Time, mo.Stats.flightTime, Dt * 0.5f) && Vec2.Distance(imp.Pos, shot.Aim) < 1e-3f, "Doc 04 §6.1 el proyectil cae en el punto fijado, después de su vuelo", $"vuelo {imp.Time - shot.Time:0.00} s, desvío {Vec2.Distance(imp.Pos, shot.Aim):0.000}");
                // contra un Esqueleto rápido que ya pasó, el tiro falla
                var logM = new List<SimEvent>();
                var bm = B(); bm.economy.startGold = 1000;
                var mm = Started(Straight("E", 1f), logM, bm);
                mm.TryBuild("mortero", new Vec2(0f, 9f), out _, out _);
                ToWave(mm, logM);
                Run(mm, logM, () => logM.Any(e => e.Type == SimEventType.ProjectileImpact), 30f);
                bool missed = !logM.Any(e => e.Type == SimEventType.EnemyDamaged);
                var e1 = mm.Enemies.FirstOrDefault();
                Check(missed || (e1 != null && e1.Hp < 60f), "Doc 04 §6.1 un objetivo rápido puede salir del área antes del impacto", missed ? "falló el primero" : "le pegó");
            }

            // ---------------- Eléctrica
            {
                var log = new List<SimEvent>();
                var b = B(); b.economy.startGold = 1000;
                var m = Started(Straight("D D D D D D", 0.6f), log, b);
                ToWave(m, log);
                Run(m, log, () => m.Enemies.Count == 6, 10f);
                m.TryBuild("electrica", new Vec2(m.Enemies[0].Position.x + 1f, 2.2f), out var el, out _);
                log.Clear();
                Run(m, log, () => log.Any(e => e.Type == SimEventType.Shot), 5f);
                var links = log.Where(e => e.Type == SimEventType.Shot).ToList();
                var hits = log.Where(e => e.Type == SimEventType.EnemyDamaged).Select(e => e.EnemyId).ToList();
                Check(links.Count == hits.Count && hits.Distinct().Count() == hits.Count && hits.Count <= el.Stats.chainJumps + 1 && hits.Count >= 2,
                    "Doc 04 §6.3 la descarga salta y pega una vez por enemigo, hasta su máximo", $"{hits.Count} impactos (máx {el.Stats.chainJumps + 1})");
                Check(links.Skip(1).All(s => Vec2.Distance(s.Pos, s.Aim) <= el.Stats.chainRadius + 0.01f), "Doc 04 §6.3 cada salto queda dentro del radio de rebote");
            }

            // ---------------- Infernal
            {
                var log = new List<SimEvent>();
                var b = B(); b.economy.startGold = 1000;
                b.enemies.First(e => e.code == "T").travelTime = 400f;
                var m = Started(Straight("T", 1f), log, b);
                ToWave(m, log);
                Run(m, log, () => m.Enemies.Count == 1 && m.Enemies[0].Position.x > -27f, 20f);
                var e = m.Enemies[0];
                m.TryBuild("infernal", new Vec2(e.Position.x + 2f, 2.5f), out var inf, out _);
                log.Clear();
                Run(m, log, () => m.ActiveTime > 0 && log.Count(x => x.Type == SimEventType.EnemyDamaged) >= 45, 10f);
                var dmg = log.Where(x => x.Type == SimEventType.EnemyDamaged).Select(x => x.Float1).ToList();
                var s = inf.Stats;
                bool ramp = Near(dmg[0], s.rampDps[0] * s.interval, 0.01f) && Near(dmg[12], s.rampDps[1] * s.interval, 0.01f) && Near(dmg[25], s.rampDps[2] * s.interval, 0.01f) && Near(dmg[40], s.rampDps[3] * s.interval, 0.01f);
                Check(ramp, "Doc 04 §6.4 el daño del Infernal sube por etapas mientras mantiene el objetivo", $"{dmg[0]:0.0} · {dmg[12]:0.0} · {dmg[25]:0.0} · {dmg[40]:0.0}");
                // cambio de objetivo: vuelve a la primera etapa
                var logB = new List<SimEvent>();
                var bb = B(); bb.economy.startGold = 1000;
                var mb = Started(Straight("D . . . . . . . . . . . D", 0.5f), logB, bb);
                ToWave(mb, logB);
                mb.TryBuild("infernal", new Vec2(-22f, 2.5f), out var inf2, out _);
                Run(mb, logB, () => logB.Count(x => x.Type == SimEventType.EnemyKilled) >= 1, 30f);
                Run(mb, logB, () => logB.Count(x => x.Type == SimEventType.Shot && x.TowerId == inf2.Id && x.EnemyId != logB.First(k => k.Type == SimEventType.EnemyKilled).EnemyId) >= 1, 30f);
                var firstOnNew = logB.First(x => x.Type == SimEventType.Shot && x.TowerId == inf2.Id && x.EnemyId != logB.First(k => k.Type == SimEventType.EnemyKilled).EnemyId);
                Check(firstOnNew.Int2 == 1, "Doc 04 §6.4 al cambiar de objetivo la potencia vuelve a la etapa inicial", $"etapa {firstOnNew.Int2}");
                // contra metal: cero hasta la etapa máxima, después rompe armadura
                var logA = new List<SimEvent>();
                var ba = B(); ba.economy.startGold = 1000; ba.enemies.First(x => x.code == "A").travelTime = 400f;
                var ma = Started(Straight("A", 1f), logA, ba);
                ToWave(ma, logA);
                Run(ma, logA, () => ma.Enemies.Count == 1 && ma.Enemies[0].Position.x > -27f, 20f);
                ma.TryBuild("infernal", new Vec2(ma.Enemies[0].Position.x + 2f, 2.5f), out var inf3, out _);
                logA.Clear();
                Run(ma, logA, () => logA.Any(x => x.Type == SimEventType.EnemyDamaged), 10f);
                var firstArmor = logA.First(x => x.Type == SimEventType.EnemyDamaged);
                var shotsBefore = logA.Count(x => x.Type == SimEventType.Shot && x.Time < firstArmor.Time);
                Check(firstArmor.Float2 > 0f && inf3.InfernoMax && Near(shotsBefore * inf3.Stats.interval, inf3.Stats.rampStep * (inf3.Stats.rampDps.Length - 1), 0.11f) && logA.Count(x => x.Type == SimEventType.AttackImmune) == 1 && inf3.KnownImmune.Count == 0,
                    "Doc 04 §6.4/§9 contra metal no daña hasta la etapa máxima, sigue fijado y después rompe la armadura", $"{shotsBefore} tics en cero, primer daño a la armadura {firstArmor.Float2:0.0}");
                var recA = new MatchRecorder();
                foreach (var x in logA) recA.Consume(x);
                var repA = recA.Build(ma);
                Check(logA.Where(x => x.Type == SimEventType.AttackImmune).All(x => x.Int2 == 1) && repA.metal.wastedShots == 0 && repA.metal.firstImmuneHitT < 0f,
                    "BUG-028 el calentamiento de la Infernal no cuenta como disparo desperdiciado", $"desperdiciados {repA.metal.wastedShots}");
            }

            // ---------------- Torre de oro
            {
                var log = new List<SimEvent>();
                var b = B(); b.economy.startGold = 1000; b.economy.baseHp = 100000;
                var m = Started(Straight("D x200", 1f), log, b);
                ToWave(m, log);
                m.TryBuild("oro", new Vec2(0f, 15f), out var g, out _);
                float t0 = m.ActiveTime;
                Run(m, log, () => m.ActiveTime - t0 >= 10f - 1e-4f, 11f);
                Check(Near(g.Stored, 10f * g.Stats.goldPerSecond, 0.05f), "Doc 04 §6.5 la Torre de oro junta a su ritmo", $"{g.Stored:0.00} en 10 s");
                m.Pause(); float before = g.Stored; for (int i = 0; i < 120; i++) m.Tick(); m.Resume();
                Check(Near(before, g.Stored, 1e-4f), "Doc 03 §21 en pausa no se genera oro");
                Run(m, log, () => g.Full, 120f);
                Check(g.Full && Near(g.Stored, g.Stats.goldCapacity, 1e-3f) && log.Count(x => x.Type == SimEventType.GoldStoredFull) == 1, "Doc 04 §6.5 al llegar a la capacidad deja de producir y avisa una vez", $"{g.Stored:0.0}/{g.Stats.goldCapacity}");
                int gold0 = m.Gold;
                Check(m.TryCollect(g.Id, out int got, out _) && got == (int)g.Stats.goldCapacity && m.Gold == gold0 + got && g.Stored < 1f, "Doc 04 §6.5 clic en la torre: el oro pasa al jugador", $"+{got}");
                Run(m, log, () => g.Stored >= 20f, 60f);
                int stored = (int)g.Stored, refund = m.SellRefund(g), gold1 = m.Gold;
                m.TrySell(g.Id, out _, out _);
                Check(m.Gold == gold1 + stored + refund, "GDS-002.4 S16 vender una Torre de oro entrega lo guardado más el reembolso", $"{m.Gold} = {gold1} + {stored} + {refund}");
            }

            // ---------------- Lanzallamas y quemadura
            {
                var log = new List<SimEvent>();
                var b = B(); b.economy.startGold = 1000;
                b.enemies.First(e => e.code == "D").travelTime = 120f;
                var m = Started(Straight("D", 1f), log, b);
                ToWave(m, log);
                Run(m, log, () => m.Enemies.Count == 1 && m.Enemies[0].Position.x > -28.5f, 10f);
                var d = m.Enemies[0];
                m.TryBuild("lanzallamas", new Vec2(d.Position.x + 3f, 3f), out var fl, out _);
                log.Clear();
                Run(m, log, () => log.Any(x => x.Type == SimEventType.Shot), 5f);
                Check(log.Any(x => x.Type == SimEventType.EnemyDamaged && x.EnemyId == d.Id && x.Int1 == 0) && log.Any(x => x.Type == SimEventType.BurnStarted && x.EnemyId == d.Id),
                    "Doc 04 §6.6 la ráfaga daña e incendia al terrestre que alcanza");
                var bs = log.First(x => x.Type == SimEventType.BurnStarted && x.EnemyId == d.Id);
                Check(Near(bs.Float1, fl.Stats.burnDps, 1e-4f) && bs.TowerId == fl.Id, "BUG-034 QuemaduraIniciada lleva el daño por segundo y la torre (contrato de GDS-002.0)", $"{bs.Float1}/s");
                float hp0 = d.Hp;
                var burnLog = new List<SimEvent>();
                Run(m, burnLog, () => !d.Burning || !d.Alive, 10f);
                float burned = burnLog.Where(x => x.Type == SimEventType.EnemyDamaged && x.EnemyId == d.Id && x.Int1 == 1).Sum(x => x.Float1);
                Check(burned > 0f && burned <= fl.Stats.burnDps * (fl.Stats.burnDuration + fl.Stats.interval) + 0.5f, "Doc 04 §10.1 la quemadura daña con el tiempo y no se acumula", $"{burned:0.0} de quemadura");
                var logV = new List<SimEvent>();
                var bv = B(); bv.economy.startGold = 1000; bv.enemies.First(e => e.code == "V").travelTime = 120f;
                var mv = Started(Straight("V", 1f), logV, bv);
                ToWave(mv, logV);
                Run(mv, logV, () => mv.Enemies.Count == 1 && mv.Enemies[0].Position.x > -28.5f, 10f);
                var v = mv.Enemies[0];
                mv.TryBuild("lanzallamas", new Vec2(v.Position.x + 3f, 3f), out _, out _);
                Run(mv, logV, () => logV.Any(x => x.Type == SimEventType.BurnStarted), 5f);
                Check(logV.Any(x => x.Type == SimEventType.BurnStarted && x.EnemyId == v.Id), "Doc 04 §6.6 un aéreo que intercepta la ráfaga recibe el impacto y se incendia");
                var logA = new List<SimEvent>();
                var ba = B(); ba.economy.startGold = 1000; ba.enemies.First(e => e.code == "A").travelTime = 120f;
                var ma = Started(Straight("A", 1f), logA, ba);
                ToWave(ma, logA);
                Run(ma, logA, () => ma.Enemies.Count == 1 && ma.Enemies[0].Position.x > -28.5f, 10f);
                var a = ma.Enemies[0];
                ma.TryBuild("lanzallamas", new Vec2(a.Position.x + 3f, 3f), out _, out _);
                Run(ma, logA, () => logA.Any(x => x.Type == SimEventType.AttackImmune), 5f);
                Check(!a.Burning && Near(a.Armor, 180f, 1e-3f) && logA.Any(x => x.Type == SimEventType.AttackImmune && x.Int1 == 1), "Doc 04 §9 el metal no recibe daño ni quemadura del Lanzallamas");
                // fuego en el piso: un terrestre que pasa después se prende
                var logZ = new List<SimEvent>();
                var bz = B(); bz.economy.startGold = 1000;
                var mz = Started(Straight("D . . . . . . . . . . . . . . . . D", 0.5f), logZ, bz);
                ToWave(mz, logZ);
                mz.TryBuild("lanzallamas", new Vec2(-22f, 3.5f), out var fz, out _);
                Run(mz, logZ, () => logZ.Any(x => x.Type == SimEventType.Shot), 20f);
                var shot = logZ.First(x => x.Type == SimEventType.Shot);
                Check(mz.Fires.Count == 1 && Near(mz.Fires[0].Duration, fz.Stats.burnDuration, 1e-4f), "Doc 04 §6.6 el fuego queda en el piso lo mismo que dura la quemadura");
                // BUG-027: el fuego de una torre vendida sigue prendiendo con SU potencia y avisa
                var logS = new List<SimEvent>();
                var bsld = B(); bsld.economy.startGold = 1000; bsld.enemies.First(e => e.code == "D").travelTime = 60f;
                var ms = Started(Straight("D . . D", 0.5f), logS, bsld);
                ToWave(ms, logS);
                ms.TryBuild("lanzallamas", new Vec2(-24f, 3.5f), out var fs, out _);
                Run(ms, logS, () => ms.Fires.Count > 0, 30f);
                int zoneTower = fs.Id; float zoneDps = ms.Fires[0].BurnDps;
                var second = ms.Enemies.OrderBy(x => x.Id).Last();
                bool secondBurningAtSell = second.Burning;
                ms.TrySell(fs.Id, out _, out _);
                logS.Clear();
                Run(ms, logS, () => logS.Any(x => x.Type == SimEventType.BurnStarted && x.EnemyId == second.Id) || ms.Fires.Count == 0, 20f);
                var bz2 = logS.FirstOrDefault(x => x.Type == SimEventType.BurnStarted && x.EnemyId == second.Id);
                Check(!secondBurningAtSell && ms.Enemies.Count == 2 && bz2.Type == SimEventType.BurnStarted && bz2.TowerId == zoneTower && Near(bz2.Float1, zoneDps, 1e-4f) && Near(second.BurnDps, zoneDps, 1e-4f),
                    "BUG-027 el fuego de una torre vendida prende con su propia potencia y avisa", bz2.Type == SimEventType.BurnStarted ? $"{bz2.Float1}/s de la torre {bz2.TowerId}" : $"sin quemadura (ardía al vender: {secondBurningAtSell}, enemigos {ms.Enemies.Count})");
            }

            // ---------------- regresión de la revisión técnica de EJ-002 (datos y progresión)
            {
                var b = B(); b.timing.burnTick = 0f;
                var errs = DataValidator.ValidateBalance(b);
                Check(errs.Any(e => e.Contains("burnTick")), "BUG-036 sin tick de quemadura, el validador lo reclama (no hay valor por defecto en el código)", string.Join(" | ", errs));
                var l = Straight("D", 1f); l.routes = new[] { l.routes[0], new RouteData { points = new[] { new Vec2(0, 0) } } };
                var le = DataValidator.ValidateLevel(B(), l);
                Check(le.Any(e => e.Contains("menos de 2 puntos")), "BUG-039 un recorrido de menos de 2 puntos se reporta", string.Join(" | ", le));
                var p = new Progression(World1.Campaign(), new SaveData());
                p.RewardFor("m1_n3", 3); p.StarsOf("m1_n4"); p.Peek("m1_n5");
                Check(p.Save.levels.Length == 0, "BUG-037 consultar el mapa no agrega registros al guardado", $"{p.Save.levels.Length} registros");
            }

            // ---------------- mejoras permanentes y progresión
            {
                var b = B();
                var t = b.towers.First(x => x.id == "mortero");
                var mod = StatMods.Apply(t, new List<StatMod> { new StatMod { stat = "damage", op = "mul", value = 1.2f }, new StatMod { stat = "range", op = "add", value = 1f } });
                Check(Near(mod.levels[0].damage, t.levels[0].damage * 1.2f, 0.01f) && Near(mod.levels[1].range, t.levels[1].range + 1f, 1e-4f) && Near(t.levels[0].damage, 90f, 1e-4f),
                    "GDS-002.3 las mejoras permanentes modifican una copia; el balance no se toca");
                var camp = World1.Campaign();
                var p = new Progression(camp, new SaveData());
                Check(p.IsLevelUnlocked("m1_n1") && !p.IsLevelUnlocked("m1_n2") && !p.IsLevelUnlocked("m1_n7") && p.Save.unlockedTowers.SequenceEqual(new[] { "arqueras", "canon", "mago" }),
                    "Doc 02 §4 al empezar: solo el nivel 1 y las tres torres iniciales");
                var r1 = p.ApplyVictory("m1_n1", 2, 70);
                Check(r1.FirstClear && r1.Currency == 30 && r1.TowerUnlocked == "mortero" && p.IsLevelUnlocked("m1_n2"), "Doc 02 §9 victoria: moneda = base × estrellas, torre y nivel siguiente", $"+{r1.Currency} · {r1.TowerUnlocked}");
                var r2 = p.ApplyVictory("m1_n1", 3, 100);
                Check(r2.Currency == 10 + 10 && r2.TowerUnlocked == null && r2.NewRecord, "GDS-002.3 S18 repetir paga 25 % más la mejora del récord", $"+{r2.Currency}");
                var r3 = p.ApplyVictory("m1_n1", 1, 20);
                Check(r3.Currency == 5 && p.StarsOf("m1_n1") == 3, "GDS-002.3 repetir con menos estrellas no baja el récord", $"+{r3.Currency}");
                p.ApplyVictory("m1_n2", 1, 10);
                var r4 = p.ApplyVictory("m1_n3", 1, 10);
                Check(r4.TandaEarned && r4.ContinentCompleted == "m1_c1" && p.Save.tandaPending == 1, "Doc 02 §8 completar la tanda del continente otorga una mejora de torre base");
                Check(!p.ChooseTanda("mortero") && p.ChooseTanda("canon") && p.TandaTier("canon") == 1 && p.Save.tandaPending == 0, "Doc 02 §8 la mejora de tanda va solo a una torre inicial");
                var lo = p.Loadout(false);
                var mt = new Match(b, World1.Level("m1_n4"), lo);
                Check(Near(mt.GetTowerType("canon").levels[0].damage, 60f * 1.15f, 0.01f) && mt.GetTowerType("infernal") == null && mt.GetTowerType("electrica") != null,
                    "GDS-002.0 la partida recibe las torres desbloqueadas con sus mejoras", string.Join(",", mt.TowerTypes.Select(x => x.id)));
                Check(p.CannotBuyReason("infernal_nucleo") == "torre bloqueada" && p.CannotBuyReason("mortero_carga") == "" && p.Buy("mortero_carga") && !p.Buy("mortero_carga"),
                    "Doc 02 §7 la tienda vende mejoras de torres desbloqueadas, una vez cada una", $"quedan {p.Currency}");
                foreach (var id in new[] { "m1_n4", "m1_n5" }) p.ApplyVictory(id, 2, 60);
                var r6 = p.ApplyVictory("m1_n6", 2, 60);
                Check(r6.WorldUnlocked == "m2" && r6.TandaEarned && p.IsTowerUnlocked("lanzallamas") && !p.IsLevelUnlocked("m2_n1"),
                    "Doc 02 §4 completar el nivel 6 abre el Mundo 2 (sus niveles quedan fuera de esta entrega)", r6.WorldUnlocked);
            }

            // ---------------- determinismo con las torres nuevas
            {
                string Play()
                {
                    var b = B(); b.economy.startGold = 2000;
                    var l = World1.Level("m1_n6");
                    var m = new Match(b, l, new MatchOptions());
                    var log = new List<SimEvent>();
                    m.Begin();
                    var plan = new (float t, string type, Vec2 at)[] { (0.5f, "mortero", new Vec2(-8f, 8f)), (1f, "electrica", new Vec2(2f, 3f)), (1.5f, "infernal", new Vec2(-1f, -3f)), (2f, "oro", new Vec2(-18f, 5f)), (2.5f, "lanzallamas", new Vec2(4f, -3f)), (3f, "bombardera", new Vec2(11f, 3f)) };
                    int k = 0;
                    while (!m.Ended && m.ActiveTime < 240f)
                    {
                        while (k < plan.Length && m.ActiveTime >= plan[k].t) { m.TryBuild(plan[k].type, plan[k].at, out _, out _); k++; }
                        foreach (var t in m.Towers.ToList()) if (t.Full) m.TryCollect(t.Id, out _, out _);
                        m.Tick(); m.DrainEvents(log);
                    }
                    return string.Join("\n", log.Select(e => e.ToString()));
                }
                var a = Play(); var c = Play();
                Check(a == c && a.Length > 1000, "GDS-002.0 misma partida con las torres nuevas, mismos eventos", $"{a.Length} chars");
            }

            // ---------------- los seis niveles con el plan competente (LDS-002.6)
            {
                var targets = new Dictionary<string, (float lo, float hi)>
                {
                    ["m1_n1"] = (300, 420), ["m1_n2"] = (360, 480), ["m1_n3"] = (420, 540), ["m1_n4"] = (480, 600), ["m1_n5"] = (540, 660), ["m1_n6"] = (600, 720),
                };
                for (int li = 0; li < World1.Levels.Length; li++)
                {
                    var id = World1.Levels[li];
                    var lvl = World1.Level(id);
                    var m = new Match(B(), lvl, new MatchOptions { Tutorial = li == 0, AllowedTowers = World1.TowersAt(li) });
                    var r = World1.Play(m, World1.Plans(li)["competente"], 0.5f);
                    var (lo, hi) = targets[id];
                    Check(r.Outcome == "victoria" && r.Duration >= lo && r.Duration <= hi && r.Report.checks.All(c => c.StartsWith("OK")),
                        $"LDS-002.6 {id}: el plan competente gana dentro de la duración objetivo", $"{r.Outcome} ★{r.Stars} en {MatchRecorder.Clock(r.Duration)} (objetivo {MatchRecorder.Clock(lo)}–{MatchRecorder.Clock(hi)})");
                }
                var m6 = new Match(B(), World1.Level("m1_n6"), new MatchOptions { AllowedTowers = World1.TowersAt(5) });
                var ra = World1.Play(m6, World1.Plans(5)["solo_arqueras"], 0.5f);
                var m5 = new Match(B(), World1.Level("m1_n5"), new MatchOptions { AllowedTowers = World1.TowersAt(4) });
                var rc = World1.Play(m5, World1.Plans(4)["solo_canon"], 0.5f);
                Check(ra.Outcome == "derrota" && rc.Outcome == "derrota", "Doc 01 una sola torre no resuelve el Mundo 1: Arqueras cae con el metal, Cañón con el aire", $"arqueras {ra.Outcome} · cañón {rc.Outcome}");
            }

            Console.WriteLine($"\n{passed} OK · {failed} FALLA (Mundo 1)");
            return failed;
        }
    }
}
