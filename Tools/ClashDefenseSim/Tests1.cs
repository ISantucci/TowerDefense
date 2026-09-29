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
        static float R2(float v) => (float)Math.Round(v, 2, MidpointRounding.AwayFromZero);
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
            // Estas pruebas son del Doc 05 v2.0 (GDS-004.2). Hasta que los assets lleven w1-0.2 (TL-004, etapa B), Datos/ exporta w1-0.1.
            var version = B().version;
            if (string.CompareOrdinal(version, "w1-0.2") < 0)
            {
                Check(false, "GDS-004.2 los datos del Mundo 1 implementan el Doc 05 v2.0",
                      $"Datos/ es {version}: corré «dotnet run test Propuestas/w1-0.2» o exportá desde Unity después de pasar el balance a los assets");
                Console.WriteLine($"\n{passed} OK · {failed} FALLA (Mundo 1)");
                return failed;
            }

            // ---------------- datos
            {
                var b = B();
                var errs = DataValidator.ValidateBalance(b);
                foreach (var id in World1.Levels) errs.AddRange(DataValidator.ValidateLevel(b, World1.Level(id)));
                errs.AddRange(Progression.Validate(World1.Campaign(), b));
                Check(errs.Count == 0, "GDS-002.0 balance, seis niveles y campaña del Mundo 1 validan", string.Join(" | ", errs));
                // Doc 05 v2.0 §5.3, §6 y §8: el balance w1-0.2 lleva los valores del documento tal cual (GDS-004.2)
                var cost = new Dictionary<string, int> { ["arqueras"] = 100, ["canon"] = 125, ["mago"] = 150, ["mortero"] = 180, ["bombardera"] = 160, ["electrica"] = 170, ["infernal"] = 200, ["oro"] = 175, ["lanzallamas"] = 190 };
                var foes = new Dictionary<string, (float hp, float armor, float travel, int dmg, int gold)>
                {
                    ["D"] = (100, 0, 30, 10, 20), ["E"] = (60, 0, 24, 5, 12), ["V"] = (90, 0, 26, 10, 20), ["T"] = (500, 0, 45, 25, 50),
                    ["A"] = (120, 240, 40, 20, 45), ["G"] = (3000, 0, 70, 60, 160), ["B"] = (2200, 0, 60, 60, 160),
                };
                var diffs = new List<string>();
                foreach (var kv in cost) { var t = b.towers.First(x => x.id == kv.Key); if (t.cost != kv.Value) diffs.Add($"{kv.Key} {t.cost}≠{kv.Value}"); }
                foreach (var kv in foes)
                {
                    var en = b.enemies.First(x => x.code == kv.Key);
                    if (en.hp != kv.Value.hp || en.armor != kv.Value.armor || en.travelTime != kv.Value.travel || en.baseDamage != kv.Value.dmg || en.gold != kv.Value.gold) diffs.Add(en.id);
                }
                var mg = b.towers.First(x => x.id == "mago");
                if (mg.metalEfficiency != 0.5f) diffs.Add("mago metal");
                if (b.economy.startGold != 150 || b.economy.tutorialStartGold != 100) diffs.Add("oro inicial");
                Check(diffs.Count == 0 && b.version == "w1-0.2", "GDS-004.2 el balance w1-0.2 lleva los costos, enemigos y oro inicial del Doc 05 v2.0", string.Join(", ", diffs));
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
                Run(m, log, () => m.ActiveTime > 0 && log.Count(x => x.Type == SimEventType.EnemyDamaged) >= 60, 10f);
                var dmg = log.Where(x => x.Type == SimEventType.EnemyDamaged).Select(x => x.Float1).ToList();
                var s = inf.Stats;
                // Doc 05 v2.0 §6.5: 20 → 55 → 120 por segundo, con umbrales a los 2 y 5 s fijado (tics de 0,1 s)
                bool ramp = Near(dmg[0], 20f * s.interval, 0.01f) && Near(dmg[19], 20f * s.interval, 0.01f) && Near(dmg[20], 55f * s.interval, 0.01f) && Near(dmg[49], 55f * s.interval, 0.01f) && Near(dmg[50], 120f * s.interval, 0.01f);
                Check(ramp, "Doc 05 §6.5 el daño del Infernal sube en tres etapas a los 2 y 5 s de mantener el objetivo", $"{dmg[0]:0.0} · {dmg[19]:0.0} | {dmg[20]:0.0} · {dmg[49]:0.0} | {dmg[50]:0.0}");
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
                float maxAt = inf3.Stats.rampTimes != null ? inf3.Stats.rampTimes[inf3.Stats.rampTimes.Length - 1] : inf3.Stats.rampStep * (inf3.Stats.rampDps.Length - 1);
                Check(firstArmor.Float2 > 0f && inf3.InfernoMax && Near(shotsBefore * inf3.Stats.interval, maxAt, 0.11f) && logA.Count(x => x.Type == SimEventType.AttackImmune) == 1 && !ma.ImmuneKnown("infernal"),
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
                Check(Near(g.Stored, 10f, 1e-3f), "Doc 05 §6.6 la Torre de oro suma 10 al completar cada ciclo de 8 s (a los 10 s: un ciclo)", $"{g.Stored:0.00} en 10 s");
                m.Pause(); float before = g.Stored; for (int i = 0; i < 120; i++) m.Tick(); m.Resume();
                Check(Near(before, g.Stored, 1e-4f), "Doc 03 §21 en pausa no se genera oro");
                Run(m, log, () => g.Full, 120f);
                Check(g.Full && Near(g.Stored, g.Stats.goldCapacity, 1e-3f) && log.Count(x => x.Type == SimEventType.GoldStoredFull) == 1, "Doc 04 §6.5 al llegar a la capacidad deja de producir y avisa una vez", $"{g.Stored:0.0}/{g.Stats.goldCapacity}");
                int gold0 = m.Gold;
                Check(m.TryCollect(g.Id, out int got, out _) && got == (int)g.Stats.goldCapacity && m.Gold == gold0 + got && g.Stored < 1f, "Doc 04 §6.5 clic en la torre: el oro pasa al jugador", $"+{got}");
                Run(m, log, () => g.Stored >= 20f, 60f);
                float keep = g.Stored;
                m.TryUpgrade(g.Id, out _);
                float t1 = m.ActiveTime;
                Run(m, log, () => g.Stored > keep + 1e-3f, 20f);
                Check(Near(g.Stored, keep + 15f, 1e-3f) && Near(m.ActiveTime - t1, 6f, 2 * Dt) && Near(g.Stats.goldCapacity, 90f, 1e-3f),
                    "Doc 05 §6.6 mejorar conserva lo guardado, sube la capacidad y el ciclo recomienza (15 cada 6 s)", $"{keep:0} → {g.Stored:0} en {m.ActiveTime - t1:0.00} s");
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
                Check(!a.Burning && Near(a.Armor, ba.enemies.First(x => x.code == "A").armor, 1e-3f) && logA.Any(x => x.Type == SimEventType.AttackImmune && x.Int1 == 1), "Doc 04 §9 el metal no recibe daño ni quemadura del Lanzallamas");
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
                var ms = Started(Straight("D . . . . D", 0.5f), logS, bsld);   // el segundo nace después de la ráfaga y antes de que se apague el fuego
                ToWave(ms, logS);
                ms.TryBuild("lanzallamas", new Vec2(-24f, 3.5f), out var fs, out _);
                Run(ms, logS, () => ms.Fires.Count > 0 && ms.Enemies.Count == 2, 30f);
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
                float dmg0 = t.levels[0].damage;
                var mod = StatMods.Apply(t, new List<StatMod> { new StatMod { stat = "damage", op = "mul", value = 1.2f }, new StatMod { stat = "range", op = "add", value = 1f } });
                Check(Near(mod.levels[0].damage, dmg0 * 1.2f, 0.01f) && Near(mod.levels[1].range, t.levels[1].range + 1f, 1e-4f) && Near(t.levels[0].damage, dmg0, 1e-4f),
                    "GDS-002.3 las mejoras permanentes modifican una copia; el balance no se toca");
                var camp = World1.Campaign();
                var p = new Progression(camp, new SaveData());
                Check(p.IsLevelUnlocked("m1_n1") && !p.IsLevelUnlocked("m1_n2") && !p.IsLevelUnlocked("m1_n7") && p.Save.unlockedTowers.SequenceEqual(new[] { "arqueras", "canon", "mago" }),
                    "Doc 02 §4 al empezar: solo el nivel 1 y las tres torres iniciales");
                var r1 = p.ApplyVictory("m1_n1", 2, 70);
                Check(r1.FirstClear && r1.Currency == 75 && r1.TowerUnlocked == "mortero" && p.IsLevelUnlocked("m1_n2"), "Doc 05 §12 victoria: moneda del récord (2★ = 75 % de 100), torre y nivel siguiente", $"+{r1.Currency} · {r1.TowerUnlocked}");
                var r2 = p.ApplyVictory("m1_n1", 3, 100);
                Check(r2.Currency == 25 && r2.TowerUnlocked == null && r2.NewRecord, "Doc 05 §12.2 mejorar el récord paga solo la diferencia", $"+{r2.Currency}");
                var r3 = p.ApplyVictory("m1_n1", 1, 20);
                var r3b = p.ApplyVictory("m1_n1", 3, 100);
                Check(r3.Currency == 0 && r3b.Currency == 0 && p.StarsOf("m1_n1") == 3 && p.Peek("m1_n1").currencyEarned == 100, "Doc 05 §12.2 repetir el mismo resultado o uno inferior paga 0; el nivel paga en total su columna de 3★", $"+{r3.Currency} · +{r3b.Currency}");
                var pr = new Progression(camp, new SaveData());
                int a1 = pr.ApplyVictory("m1_n1", 1, 1).Currency; pr.ApplyVictory("m1_n2", 1, 1); pr.ApplyVictory("m1_n3", 1, 1);
                int b1 = pr.ApplyVictory("m1_n4", 1, 1).Currency, b2 = pr.ApplyVictory("m1_n4", 2, 60).Currency, b3 = pr.ApplyVictory("m1_n4", 3, 100).Currency;
                Check(a1 == 50 && b1 == 85 && b2 == 43 && b3 == 42 && b1 + b2 + b3 == 170, "Doc 05 §12.1 las fracciones se redondean con 0,5 hacia arriba y el total coincide con 3★", $"M1–4: {b1} + {b2} + {b3}");
                p.ApplyVictory("m1_n2", 1, 10);
                var r4 = p.ApplyVictory("m1_n3", 1, 10);
                Check(r4.TandaEarned && r4.ContinentCompleted == "m1_c1" && p.Save.tandaPending == 1, "Doc 05 §7.1 completar la tanda del continente entrega una Insignia de maestría");
                Check(!p.ChooseTanda("mortero") && p.ChooseTanda("canon") && p.TandaTier("canon") == 1 && p.Save.tandaPending == 0, "Doc 05 §7.1 la insignia va solo a Arqueras, Cañón o Mago");
                var lo = p.Loadout(false);
                var mt = new Match(b, World1.Level("m1_n4"), lo);
                Check(Near(mt.GetTowerType("canon").levels[0].damage, 66f, 1e-3f) && Near(mt.GetTowerType("canon").levels[1].damage, 99f, 1e-3f) && mt.GetTowerType("infernal") == null && mt.GetTowerType("electrica") != null,
                    "GDS-002.0 la partida recibe las torres desbloqueadas con sus mejoras (Cañón +10 %: 66 y 99)", string.Join(",", mt.TowerTypes.Select(x => x.id)));
                Check(p.CannotBuyReason("infernal_nucleo") == "torre bloqueada" && p.CannotBuyReason("mortero_espoleta") == "" && p.Buy("mortero_espoleta") && !p.Buy("mortero_espoleta"),
                    "Doc 05 §7.2 la tienda vende la mejora de cada torre desbloqueada, una sola vez", $"quedan {p.Currency}");
                var mm = new Match(b, World1.Level("m1_n4"), p.Loadout(false));
                Check(Near(mm.GetTowerType("mortero").levels[0].flightTime, 1.6f, 1e-3f) && Near(mm.GetTowerType("mortero").levels[1].flightTime, 1.44f, 1e-3f), "Doc 05 §7.2 Espoleta rápida: la caída del Mortero tarda 20 % menos en N1 y N2");
                foreach (var id in new[] { "m1_n4", "m1_n5" }) p.ApplyVictory(id, 2, 60);
                var r6 = p.ApplyVictory("m1_n6", 2, 60);
                Check(r6.WorldUnlocked == "m2" && r6.TandaEarned && p.IsTowerUnlocked("lanzallamas") && !p.IsLevelUnlocked("m2_n1"),
                    "Doc 02 §4 completar el nivel 6 abre el Mundo 2 (sus niveles quedan fuera de esta entrega)", r6.WorldUnlocked);
            }

            // ---------------- insignias: tope, redistribución, redondeo y migración de guardados (Doc 05 v2.0 §7.1, §12)
            {
                var camp = World1.Campaign();
                var save = new SaveData { tandaPicks = new[] { "arqueras", "arqueras", "arqueras" }, tandaPending = 0, purchased = new[] { "mortero_carga" }, currency = 999,
                                          levels = new[] { new LevelRecord { id = "m1_n1", stars = 3, wins = 2 }, new LevelRecord { id = "m1_n2", stars = 2, wins = 1 } } };
                var p = new Progression(camp, save);
                Check(p.TandaTier("arqueras") == 2 && p.Save.tandaPending == 1, "Doc 05 §7.1 un guardado con tres insignias en una torre queda en dos y la tercera vuelve a pendiente", $"{p.TandaTier("arqueras")} · pendientes {p.Save.tandaPending}");
                Check(p.Save.purchased.Length == 0 && p.Currency == 100 + 90, "Doc 05 §12 migración: la moneda es la suma de los récords menos lo comprado que la tienda todavía vende", $"{p.Currency}");
                Check(!p.ChooseTanda("arqueras") && p.ChooseTanda("mago") && p.ReturnTanda("mago") && p.Save.tandaPending == 1 && p.ChooseTanda("canon"),
                    "Doc 05 §7.1 como máximo dos por torre y se redistribuyen gratis");
                var mt = new Match(World1.Balance(), World1.Level("m1_n1"), p.Loadout(false));
                var arq = mt.GetTowerType("arqueras"); var arq0 = World1.Balance().towers.First(t => t.id == "arqueras");
                Check(Near(arq.levels[0].damage, 29f, 1e-3f) && Near(arq.levels[1].damage, 35f, 1e-3f) && Near(arq.projectileSpeed, R2(arq0.projectileSpeed * 1.10f), 1e-3f),
                    "Doc 05 §7.1 dos insignias suman +16 % de daño (25 → 29, 30 → 35) y +10 % de velocidad, redondeadas", $"{arq.levels[0].damage} · {arq.levels[1].damage} · {arq.projectileSpeed}");
                var mg = new Match(World1.Balance(), World1.Level("m1_n1"), new MatchOptions { Mods = new Dictionary<string, List<StatMod>> { ["mago"] = camp.tanda.options.First(o => o.tower == "mago").mods.ToList() } }).GetTowerType("mago");
                Check(Near(mg.levels[0].damage, 38f, 1e-3f) && Near(mg.levels[0].areaRadius, 1.84f, 1e-3f), "Doc 05 §7.1 una insignia de Mago: 35 → 38 de daño y 1,75 → 1,84 de área", $"{mg.levels[0].damage} · {mg.levels[0].areaRadius}");
            }

            // ---------------- oleadas por composición (Doc 05 v2.0 §9.2)
            {
                var rules = new WaveRulesData { order = "D E V T A", minibossAfter = 0.75f, minibossGap = 3f };
                var one = new List<Vec2> { new Vec2(0, 0) };
                var sch = WaveSchedule.Build(new WaveData { composition = "D3 E2 V1 T1", spawnInterval = 1f }, rules, one);
                Check(string.Join("", sch.Select(x => x.Code)) == "DEVTDED" && sch.Select(x => x.Time).SequenceEqual(new double[] { 0, 1, 2, 3, 4, 5, 6 }),
                    "Doc 05 §9.2 pasadas G→E→V→T→A, una de cada tipo con cantidad pendiente", string.Join("", sch.Select(x => x.Code)));
                var two = new List<Vec2> { new Vec2(0, 5), new Vec2(0, -5) };
                var sch2 = WaveSchedule.Build(new WaveData { composition = "D4", spawnInterval = 1f, miniboss = "G" }, rules, two);
                Check(string.Join(" ", sch2.Select(x => $"{x.Code}{x.Route}@{x.Time:0}")) == "D0@0 D1@1 D0@2 G0@5 D1@8",
                    "Doc 05 §9.2 entradas alternadas (1.ª a la A) y miniboss por la A tras el 75 % con 3 s antes y después", string.Join(" ", sch2.Select(x => $"{x.Code}{x.Route}@{x.Time:0}")));
                var fork = new List<Vec2> { new Vec2(0, 5), new Vec2(0, 5), new Vec2(0, -5), new Vec2(0, -5) };
                var sch3 = WaveSchedule.Build(new WaveData { composition = "D6", spawnInterval = 1f }, rules, fork);
                Check(string.Join("", sch3.Select(x => x.Route)) == "021302", "GDS-004.2 S2 con ramales, cada entrada reparte los suyos en ciclo", string.Join("", sch3.Select(x => x.Route)));
                var a = WaveSchedule.Build(World1.Level("m1_n6").waves[9], World1.Balance().waveRules, LevelGeometry.RoutePoints(World1.Level("m1_n6")).Select(q => q[0]).ToList());
                var b2 = WaveSchedule.Build(World1.Level("m1_n6").waves[9], World1.Balance().waveRules, LevelGeometry.RoutePoints(World1.Level("m1_n6")).Select(q => q[0]).ToList());
                Check(a.Count == 81 && a.Count(x => x.Miniboss) == 1 && a.Select(x => x.Code + x.Route + x.Time).SequenceEqual(b2.Select(x => x.Code + x.Route + x.Time)),
                    "Doc 05 §10 M1–6 oleada 10: 80 unidades + Bebé dragón, siempre en el mismo orden", $"{a.Count} apariciones");
            }

            // ---------------- oro inicial, inmunidad por tipo, pulsos y cadena (Doc 05 v2.0 §5.1, §8.2, §6.7, §6.4)
            {
                var lvl1 = World1.Level("m1_n1");
                var mT = new Match(B(), lvl1, new MatchOptions { Tutorial = true, AllowedTowers = World1.TowersAt(0) });
                var mR = new Match(B(), lvl1, new MatchOptions { Tutorial = false, AllowedTowers = World1.TowersAt(0) });
                Check(mT.Gold == 100 && mR.Gold == 150, "Doc 05 §5.1 100 de oro con el tutorial, 150 al repetir y en los demás niveles", $"{mT.Gold} · {mR.Gold}");

                var log = new List<SimEvent>();
                var b = B(); b.economy.startGold = 1000; b.enemies.First(e => e.code == "A").travelTime = 200f;
                var m = Started(Straight("A . . . . . . . . . A", 0.5f), log, b);
                m.TryBuild("arqueras", new Vec2(-26f, 3f), out var t1, out _);
                m.TryBuild("arqueras", new Vec2(-22f, 3f), out var t2, out _);
                ToWave(m, log);
                Run(m, log, () => m.Enemies.Count == 2 && m.ImmuneKnown("arqueras"), 20f);
                Run(m, log, () => false, 3f);
                int immune = log.Count(x => x.Type == SimEventType.AttackImmune);
                Check(m.ImmuneKnown("arqueras") && immune == 1 && log.Count(x => x.Type == SimEventType.AttackImmune && x.Int1 == 1) == 1,
                    "Doc 05 §8.2 el primer impacto sin daño enseña a todas las Arqueras del nivel a ignorar el metal", $"impactos sin daño {immune}");

                var logF = new List<SimEvent>();
                var bf = B(); bf.economy.startGold = 1000; bf.enemies.First(e => e.code == "T").travelTime = 400f;
                var mf = Started(Straight("T", 1f), logF, bf);
                ToWave(mf, logF);
                Run(mf, logF, () => mf.Enemies.Count == 1 && mf.Enemies[0].Position.x > -28f, 20f);
                mf.TryBuild("lanzallamas", new Vec2(mf.Enemies[0].Position.x + 2f, 2.5f), out var fl, out _);
                logF.Clear();
                Run(mf, logF, () => false, 2.9f);
                var pulses = logF.Where(x => x.Type == SimEventType.EnemyDamaged && x.Int1 == 0).ToList();
                Check(pulses.Count == 6 && pulses.All(x => Near(x.Float1, 12f, 1e-3f)) && Near(pulses[5].Time - pulses[0].Time, 1.0f, 2 * Dt) && logF.Count(x => x.Type == SimEventType.Shot) == 1,
                    "Doc 05 §6.7 una ráfaga del Lanzallamas son 6 pulsos de 12 cada 0,2 s sobre la misma línea", $"{pulses.Count} pulsos en {(pulses.Count > 1 ? pulses[pulses.Count - 1].Time - pulses[0].Time : 0):0.00} s");

                var logC = new List<SimEvent>();
                var bc = B(); bc.economy.startGold = 1000;
                var mc = Started(Straight("D D D D D D", 0.4f), logC, bc);
                ToWave(mc, logC);
                Run(mc, logC, () => mc.Enemies.Count == 6, 10f);
                mc.TryBuild("electrica", new Vec2(mc.Enemies[0].Position.x + 1f, 2.2f), out var el, out _);
                logC.Clear();
                Run(mc, logC, () => logC.Any(e => e.Type == SimEventType.Shot), 5f);
                Check(logC.Count(e => e.Type == SimEventType.EnemyDamaged) == 3, "Doc 05 §6.4 la descarga de la Eléctrica N1 alcanza como máximo 3 objetivos", $"{logC.Count(e => e.Type == SimEventType.EnemyDamaged)}");
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
                    // la duración del Doc 05 §11 no se cumple con sus oleadas (MET-004.1, decisión abierta del owner): se informa, no se exige
                    Check(r.Outcome == "victoria" && r.Stars == 3 && r.Report.checks.All(c => c.StartsWith("OK")),
                        $"LDS-002.6 {id}: el plan competente gana con 3★", $"{r.Outcome} ★{r.Stars} en {MatchRecorder.Clock(r.Duration)} · objetivo {MatchRecorder.Clock(lo)}–{MatchRecorder.Clock(hi)}{(r.Duration < lo ? " (corto: ver MET-004.1)" : "")}");
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
