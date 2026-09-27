using System;
using System.Collections.Generic;
using System.Linq;
using ClashDefense.Core;

namespace ClashDefense.Sim
{
    /// <summary>Pruebas del núcleo contra los criterios de validación de GDS-001.0 … GDS-001.6. Sin dependencias.</summary>
    static class Tests
    {
        static int passed, failed;
        static readonly List<string> failures = new List<string>();
        const float Dt = Match.Step;

        static void Check(bool ok, string name, string detail = "")
        {
            if (ok) passed++; else { failed++; failures.Add($"{name} {detail}"); }
            Console.WriteLine($"  [{(ok ? "OK" : "FALLA")}] {name}{(detail.Length > 0 ? " — " + detail : "")}");
        }

        static bool Near(float a, float b, float tol) => Math.Abs(a - b) <= tol;

        // --------------------------------------------------------------- helpers
        static BalanceData B() => DataLoader.Balance();
        static LevelData L() => DataLoader.Level();

        /// <summary>Una partida sin tutorial, ya en la oleada 1 (pasó la cuenta regresiva).</summary>
        static Match StartedMatch(BalanceData b, LevelData l, List<SimEvent> log, Action<Match> duringCountdown = null)
        {
            var m = new Match(b, l, tutorial: false);
            m.Begin();
            duringCountdown?.Invoke(m);
            while (m.State == MatchState.Countdown) { m.Tick(); }
            m.DrainEvents(log);
            return m;
        }

        static void RunUntil(Match m, List<SimEvent> log, Func<bool> stop, float maxSeconds = 900f)
        {
            int max = (int)(maxSeconds / Dt);
            for (int i = 0; i < max && !stop(); i++) { m.Tick(); m.DrainEvents(log); }
        }

        static BalanceData SingleWave(string seq, float interval = 1f)
        {
            var b = B();
            b.waves = new[] { new WaveData { sequence = seq, spawnInterval = interval } };
            return b;
        }

        /// <summary>Punto al costado del recorrido, a 'offset' del eje, a distancia d.</summary>
        static Vec2 Beside(Match m, float d, float offset)
        {
            var p = m.Path.Evaluate(d);
            var dir = m.Path.Direction(d);
            return p + new Vec2(-dir.z, dir.x) * offset;
        }

        // --------------------------------------------------------------- casos
        public static int Run()
        {
            Console.WriteLine("Pruebas del núcleo — ClashDefense P0");

            // GDS-001.5 CA1-2: secuencias y oro
            {
                var b = B();
                var codes = b.enemies.ToDictionary(e => e.code);
                int[] counts = { 10, 14, 22, 22, 32 };
                int[] golds = { 200, 232, 376, 436, 650 };
                string[] comp = { "D10", "D8 E6", "D8 E8 V6", "A4 D6 E8 V4", "A6 D8 E10 V8" };
                for (int w = 0; w < 5; w++)
                {
                    var seq = WaveSequence.Expand(b.waves[w].sequence);
                    int gold = seq.Sum(c => codes[c].gold);
                    string c2 = string.Join(" ", seq.GroupBy(x => x).OrderBy(g => g.Key).Select(g => g.Key + g.Count()));
                    Check(seq.Count == counts[w] && gold == golds[w] && c2 == comp[w], $"GDS-001.5 oleada {w + 1}", $"{seq.Count} enemigos, {c2}, oro {gold}");
                }
                var w3 = WaveSequence.Expand(b.waves[2].sequence);
                Check(w3.IndexOf("V") == 4, "GDS-001.5 CA4 primer V = quinto de la oleada 3", $"índice {w3.IndexOf("V")}");
            }

            // RQ-001.8 CA3: datos inválidos
            {
                var b = B(); b.towers[0].levels[0].range = 0; b.waves[1].sequence = "D Z";
                var errs = DataValidator.Validate(b, L());
                Check(errs.Any(e => e.Contains("range")) && errs.Any(e => e.Contains("'Z'")), "RQ-001.8 CA3 datos inválidos se reportan", string.Join(" | ", errs));
                Check(DataValidator.Validate(B(), L()).Count == 0, "RQ-001.8 datos del Doc 05 validan");
                bool threw = false;
                try { new Match(b, L(), false); } catch (InvalidOperationException) { threw = true; }
                Check(threw, "RQ-001.8 una partida no arranca con datos inválidos");
            }

            // GDS-001.1 CA1: tiempos de recorrido
            foreach (var (code, secs) in new[] { ("D", 30f), ("E", 24f), ("V", 26f), ("A", 38f) })
            {
                var log = new List<SimEvent>();
                var m = StartedMatch(SingleWave(code), L(), log);
                float spawn = log.First(e => e.Type == SimEventType.EnemySpawned).Time;
                RunUntil(m, log, () => log.Any(e => e.Type == SimEventType.EnemyReachedBase));
                float arrive = log.First(e => e.Type == SimEventType.EnemyReachedBase).Time;
                Check(Near(arrive - spawn, secs, Dt * 1.5f), $"GDS-001.1 recorrido {code} = {secs} s", $"{arrive - spawn:0.000} s");
            }

            // GDS-001.1 CA3: diez D sin defensa → derrota en el décimo
            {
                var log = new List<SimEvent>();
                var m = StartedMatch(B(), L(), log);
                RunUntil(m, log, () => m.Ended);
                int arrivals = log.Count(e => e.Type == SimEventType.EnemyReachedBase);
                Check(m.State == MatchState.Defeat && m.BaseHp == 0 && arrivals == 10, "GDS-001.1 CA3 diez D → derrota en el décimo", $"{m.State}, vida {m.BaseHp}, llegadas {arrivals}");
                Check(log.Count(e => e.Type == SimEventType.EnemyKilled) == 0 && m.Gold == 100, "GDS-001.1 regla 4 llegar no paga oro", $"oro {m.Gold}");
            }

            // GDS-001.2 CA2: tiempo de muerte de un D por torre
            foreach (var (tower, hits, secs) in new[] { ("arqueras", 4, 1.95f), ("canon", 2, 1.5f), ("mago", 3, 2.5f) })
            {
                // enemigo lento para aislar la regla de daño de la geometría (Doc 05 §6.1 mide desde el primer impacto)
                var b = SingleWave("D"); b.economy.startGold = 1000; b.enemies[0].travelTime = 600f;
                var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log, mm =>
                {
                    Check(mm.TryBuild(tower, Beside(mm, 8f, 2.5f), out _, out var r), $"construir {tower} en la cuenta regresiva", r.ToString());
                });
                RunUntil(m, log, () => m.Ended);
                var dmg = log.Where(e => e.Type == SimEventType.EnemyDamaged).ToList();
                var kill = log.First(e => e.Type == SimEventType.EnemyKilled);
                Check(dmg.Count == hits && Near(kill.Time - dmg[0].Time, secs, 0.02f), $"GDS-001.2 CA2 D muere con {hits} de {tower}", $"{dmg.Count} impactos, {kill.Time - dmg[0].Time:0.000} s");
            }

            // GDS-001.2 CA3: el Cañón nunca dispara a un aéreo
            {
                var b = SingleWave("V"); b.economy.startGold = 1000;
                var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log, mm => mm.TryBuild("canon", Beside(mm, 10f, 2.5f), out _, out _));
                RunUntil(m, log, () => m.Ended);
                Check(!log.Any(e => e.Type == SimEventType.Shot), "GDS-001.2 CA3 Cañón no elige aéreos", $"disparos {log.Count(e => e.Type == SimEventType.Shot)}");
            }

            // GDS-001.2 CA4: el más adelantado en alcance
            {
                var b = SingleWave("E E E", 0.3f); b.economy.startGold = 1000;
                var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log);
                m.TryBuild("arqueras", Beside(m, 14f, 2.5f), out var t, out _);
                RunUntil(m, log, () => log.Any(e => e.Type == SimEventType.Shot));
                var shot = log.First(e => e.Type == SimEventType.Shot);
                var inRange = m.Enemies.Where(e => Vec2.Distance(e.Position, t.Position) <= 8f).OrderByDescending(e => e.Distance).First();
                Check(shot.EnemyId == inRange.Id, "GDS-001.2 CA4 ataca al más adelantado en alcance", $"disparó a e{shot.EnemyId}, más adelantado e{inRange.Id}");
            }

            // GDS-001.2 CA5: el Mago daña a los tres
            {
                var b = SingleWave("E E E", 0.05f); b.economy.startGold = 1000;
                var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log);
                m.TryBuild("mago", Beside(m, 12f, 2.2f), out _, out _);
                RunUntil(m, log, () => log.Any(e => e.Type == SimEventType.ProjectileImpact));
                RunUntil(m, log, () => true, 0f);
                var imp = log.First(e => e.Type == SimEventType.ProjectileImpact);
                int damaged = log.Count(e => e.Type == SimEventType.EnemyDamaged && e.Time == imp.Time);
                Check(damaged == 3, "GDS-001.2 CA5 un orbe daña a los tres juntos", $"{damaged} dañados");
            }

            // GDS-001.2 CA1: motivos de colocación en orden
            {
                var log = new List<SimEvent>();
                var m = StartedMatch(B(), L(), log);
                var onPath = m.Path.Evaluate(5f);
                Check(m.CheckPlacement("arqueras", onPath) == RejectReason.OnPath, "GDS-001.2 motivo: sobre el camino");
                Check(m.CheckPlacement("arqueras", new Vec2(50, 50)) == RejectReason.OutsideArea, "GDS-001.2 motivo: fuera del área");
                Check(m.CheckPlacement("canon", Beside(m, 5f, 3f)) == RejectReason.NotEnoughGold, "GDS-001.2 motivo: falta oro primero");
                var p = Beside(m, 5f, 3f);
                m.TryBuild("arqueras", p, out _, out _);
                Check(m.CheckPlacement("arqueras", p + new Vec2(0.5f, 0), false) == RejectReason.Overlap, "GDS-001.2 motivo: choca con otra torre");
                var b2 = B(); var l2 = L(); l2.blocked = new[] { new CircleData { x = 0, z = 3.5f, radius = 1.5f } };
                var m2 = new Match(b2, l2, false);
                Check(m2.CheckPlacement("arqueras", new Vec2(0.5f, 3.5f)) == RejectReason.Blocked, "GDS-001.2 motivo: zona bloqueada");
                int goldBefore = m.Gold;
                m.TryBuild("arqueras", onPath, out _, out _);
                Check(m.Gold == goldBefore, "GDS-001.2 un clic inválido no gasta oro");
            }

            // GDS-001.4 CA1: arqueras contra blindado
            {
                var b = SingleWave("A"); b.economy.startGold = 1000;
                var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log);
                m.TryBuild("arqueras", Beside(m, 8f, 2.5f), out var t, out _);
                RunUntil(m, log, () => m.Ended);
                var imm = log.Where(e => e.Type == SimEventType.AttackImmune).ToList();
                float disc = imm.First(e => e.Int1 == 1).Time;
                bool shotAfter = log.Any(e => e.Type == SimEventType.Shot && e.Time > disc);
                Check(imm.Count >= 1 && imm.Count(e => e.Int1 == 1) == 1 && !shotAfter && !log.Any(e => e.Type == SimEventType.EnemyDamaged),
                    "GDS-001.4 CA1 un impacto inmune y no lo vuelve a elegir", $"inmunes {imm.Count}, descubrimientos {imm.Count(e => e.Int1 == 1)}, disparos después {shotAfter}");
            }

            // GDS-001.4 CA2: tres balas de Cañón rompen la armadura, vida intacta
            {
                var b = SingleWave("A"); b.economy.startGold = 1000;
                var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log);
                m.TryBuild("canon", Beside(m, 6f, 2.5f), out _, out _);
                RunUntil(m, log, () => log.Any(e => e.Type == SimEventType.ArmorBroken));
                var armorHits = log.Where(e => e.Type == SimEventType.EnemyDamaged && e.Float2 > 0).ToList();
                var en = m.Enemies.FirstOrDefault();
                Check(armorHits.Count == 3 && en != null && Near(en.Hp, 100f, 0.01f), "GDS-001.4 CA2 tres balas rompen la armadura; el exceso se pierde", $"{armorHits.Count} impactos, vida {en?.Hp}");
            }

            // GDS-001.4 CA3: Mago resta 11,55 por orbe
            {
                var b = SingleWave("A"); b.economy.startGold = 1000;
                var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log);
                m.TryBuild("mago", Beside(m, 1.5f, 2.3f), out _, out _);
                RunUntil(m, log, () => log.Any(e => e.Type == SimEventType.EnemyDamaged));
                var first = log.First(e => e.Type == SimEventType.EnemyDamaged);
                Check(Near(first.Float2, 11.55f, 0.01f), "GDS-001.4 CA3 Mago: 11,55 a la armadura por orbe", $"{first.Float2:0.000}");
                Check((int)Math.Ceiling(180f / 11.55f) == 16, "GDS-001.4 CA3 16 orbes para romperla");
            }

            // GDS-001.4 CA4 / GDS-001.1 CA2: velocidad expuesta conserva lo recorrido
            {
                var b = SingleWave("A"); b.economy.startGold = 1000;
                var brk = DataLoader.Clone(b.towers[1]); brk.id = "rompe"; brk.cost = 1; brk.levels = new[] { new TowerLevelData { damage = 180, interval = 1000f, range = 3f } };
                b.towers = b.towers.Append(brk).ToArray();
                var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log);
                float L2 = m.Path.Length;
                m.TryBuild("rompe", Beside(m, L2 * 0.5f, 2.0f), out _, out _);
                RunUntil(m, log, () => m.Ended);
                var br = log.First(e => e.Type == SimEventType.ArmorBroken);
                var en = log.First(e => e.Type == SimEventType.EnemyReachedBase);
                float spawnT = log.First(e => e.Type == SimEventType.EnemySpawned).Time;
                float dBreak = (br.Time - spawnT) * (L2 / 38f);
                float expected = br.Time + (L2 - dBreak) / (L2 / 30f);
                Check(Near(en.Time, expected, 0.05f), "GDS-001.4 CA4 roto el metal, sigue a ritmo de 30 s desde donde estaba", $"llegó {en.Time:0.00}, esperado {expected:0.00}");
            }

            // GDS-001.3: economía
            {
                var b = SingleWave("D"); var log = new List<SimEvent>();
                var m = StartedMatch(b, L(), log);
                Check(m.Gold == 100, "GDS-001.3 CA1 oro inicial 100");
                var bb = SingleWave("D"); bb.economy.startGold = 10000;
                var m2 = StartedMatch(bb, L(), log);
                int[] up = { 100, 125, 150 }; int[] s1 = { 60, 75, 90 }; int[] s2 = { 120, 150, 180 };
                var ids = new[] { "arqueras", "canon", "mago" };
                for (int i = 0; i < 3; i++)
                {
                    m2.TryBuild(ids[i], Beside(m2, 6f + i * 5f, 3f), out var ta, out _);
                    Check(m2.UpgradeCost(ta) == up[i] && m2.SellRefund(ta) == s1[i], $"GDS-001.3 CA2 {ids[i]} mejora {up[i]} y venta N1 {s1[i]}", $"{m2.UpgradeCost(ta)} / {m2.SellRefund(ta)}");
                    m2.TryUpgrade(ta.Id, out _);
                    Check(ta.Level == 2 && m2.SellRefund(ta) == s2[i], $"GDS-001.3 CA2 {ids[i]} venta N2 {s2[i]}", $"{m2.SellRefund(ta)}");
                }
            }

            // GDS-001.6 CA1 / GDS-001.3 CA4: pausa bloquea acciones
            {
                var log = new List<SimEvent>();
                var m = StartedMatch(B(), L(), log);
                m.Pause();
                bool built = m.TryBuild("arqueras", Beside(m, 5f, 3f), out _, out var r);
                int hp = m.BaseHp; float t = m.ActiveTime;
                for (int i = 0; i < 600; i++) m.Tick();
                Check(!built && r == RejectReason.InvalidState && m.ActiveTime == t, "GDS-001.6 CA1 en pausa no se construye ni avanza el tiempo", r.ToString());
                m.Resume();
                Check(m.State == MatchState.Wave, "GDS-001.6 la pausa vuelve al estado anterior", m.State.ToString());
            }

            // GDS-001.0 / GDS-001.6 CA3: determinismo y pausa neutra
            {
                string RunScripted(bool withPauses)
                {
                    var m = new Match(B(), L(), false);
                    var log = new List<SimEvent>();
                    m.Begin();
                    Bot.Scripted(m, log, withPauses);
                    return string.Join("\n", log.Where(e => e.Type != SimEventType.StateChanged || (e.Text != "Paused" && e.Text2 != "Paused")).Select(e => e.ToString()));
                }
                var a = RunScripted(false); var b1 = RunScripted(false); var c = RunScripted(true);
                Check(a == b1, "GDS-001.0 CA1 misma partida, mismos eventos", $"{a.Length} chars");
                Check(a == c, "GDS-001.6 CA3 las pausas no cambian la partida");
            }

            // GDS-001.6: tutorial
            {
                var m = new Match(B(), L(), tutorial: true);
                var log = new List<SimEvent>();
                m.Begin();
                RunUntil(m, log, () => m.State == MatchState.Tutorial);
                float tAtTutorial = m.ActiveTime;
                m.TutorialNext(); m.TutorialNext(); m.TutorialNext();
                bool other = m.TryBuild("canon", Beside(m, 5f, 3f), out _, out var r1);
                m.NotifyTowerSelected("canon");
                bool stillSelect = m.Tutorial == TutorialStep.SelectTower;
                m.NotifyTowerSelected("arqueras");
                for (int i = 0; i < 300; i++) m.Tick();
                bool frozen = m.ActiveTime == tAtTutorial && !m.Enemies.Any();
                bool wrong = m.TryBuild("mago", Beside(m, 5f, 3f), out _, out var r2);
                bool ok = m.TryBuild("arqueras", Beside(m, 5f, 3f), out _, out _);
                m.TutorialNext();
                bool beforeGoal = m.State == MatchState.Tutorial && m.Tutorial == TutorialStep.Goal;
                int goldAtEnd = m.Gold;
                m.TutorialNext();
                m.DrainEvents(log);
                Check(!other && stillSelect && frozen && !wrong && r2 == RejectReason.TutorialRestricted && ok && beforeGoal,
                    "GDS-001.6 CA2 el tutorial congela, guía y solo deja la Arqueras", $"otro={other} r1={r1} frozen={frozen} wrong={wrong} r2={r2} ok={ok}");
                Check(m.State == MatchState.Wave && goldAtEnd == 0 && log.Any(e => e.Type == SimEventType.WaveStarted), "GDS-001.6 CA2 al cerrar el paso 7 arranca la oleada 1 con 0 de oro", $"{m.State}, oro {goldAtEnd}");
                Check(Near(tAtTutorial, 3.4f, Dt * 1.5f), "GDS-001.6 el tutorial no suma a la duración (3,4 s de cuenta)", $"{tAtTutorial:0.000}");
            }

            // GDS-001.6 regla 1 enmendada (revisión de EJ-001): con tutorial la cuenta no abre la construcción
            {
                var m = new Match(B(), L(), tutorial: true);
                var log = new List<SimEvent>();
                m.Begin();
                bool canSel = m.CanSelectTower("arqueras");
                bool built = m.TryBuild("arqueras", Beside(m, 5f, 3f), out _, out var r);
                RunUntil(m, log, () => m.State == MatchState.Tutorial);
                m.TutorialNext(); m.TutorialNext(); m.TutorialNext();
                m.NotifyTowerSelected("arqueras");
                bool ok = m.TryBuild("arqueras", Beside(m, 5f, 3f), out _, out _);
                m.TutorialNext(); m.TutorialNext();
                Check(!canSel && !built && r == RejectReason.InvalidState && ok && m.State == MatchState.Wave,
                    "GDS-001.6 con tutorial, la cuenta no deja construir y el tutorial llega a la oleada 1", $"canSel={canSel} built={built} r={r} ok={ok} estado={m.State}");

                var n = new Match(B(), L(), tutorial: false);
                n.Begin();
                bool nb = n.TryBuild("arqueras", Beside(n, 5f, 3f), out _, out var nr);
                Check(n.State == MatchState.Countdown && nb, "Doc 03 §5 sin tutorial, en la cuenta se construye", nr.ToString());
            }

            // Registro: el reloj redondea antes de partir en minutos (revisión de EJ-001)
            {
                string a = MatchRecorder.Clock(59.97f), b2 = MatchRecorder.Clock(179.96f), c = MatchRecorder.Clock(178.267f);
                Check(a == "1:00.0" && b2 == "3:00.0" && c == "2:58.3", "RQ-001.7 el reloj no escribe 0:60.0", $"{a} · {b2} · {c}");
            }

            // GDS-001.6 CA4: estrellas
            {
                var m = new Match(B(), L(), false);
                Check(m.ComputeStars(100) == 3 && m.ComputeStars(99) == 2 && m.ComputeStars(50) == 2 && m.ComputeStars(49) == 1 && m.ComputeStars(1) == 1 && m.ComputeStars(0) == 0,
                    "GDS-001.6 CA4 umbrales de estrellas 100/50/1");
            }

            // GDS-001.5 CA2 + intervalo de 3 s + victoria al resolver el último
            {
                var m = new Match(B(), L(), false);
                var log = new List<SimEvent>();
                m.Begin();
                var res = Bot.Play(m, log, Bot.Strategies["mixta"], DataLoader.Level());
                var starts = log.Where(e => e.Type == SimEventType.WaveStarted).ToList();
                var clears = log.Where(e => e.Type == SimEventType.WaveCleared).ToList();
                bool gaps = true;
                for (int i = 0; i + 1 < starts.Count && i < clears.Count; i++) gaps &= Near(starts[i + 1].Time - clears[i].Time, 3f, Dt * 1.5f);
                Check(gaps, "GDS-001.5 intervalo de 3 s entre oleadas", string.Join(", ", starts.Skip(1).Zip(clears, (s, c) => (s.Time - c.Time).ToString("0.00"))));
                bool spawnTimes = true;
                foreach (var s in starts)
                {
                    int n = s.Int1; float iv = m.Balance.waves[n - 1].spawnInterval;
                    var sp = log.Where(e => e.Type == SimEventType.EnemySpawned && e.Int1 == n).Select(e => e.Time - s.Time).ToList();
                    for (int i = 0; i < sp.Count; i++) spawnTimes &= Near(sp[i], i * iv, Dt * 1.01f);
                }
                Check(spawnTimes, "GDS-001.5 CA2 el i-ésimo aparece a i × intervalo");
                if (m.State == MatchState.Victory)
                {
                    var end = log.First(e => e.Type == SimEventType.MatchEnded);
                    var lastResolve = log.Where(e => e.Type == SimEventType.EnemyKilled || e.Type == SimEventType.EnemyReachedBase).Max(e => e.Time);
                    Check(end.Time >= lastResolve, "GDS-001.6 CA5 la victoria espera al último enemigo");
                }
                var rec = new MatchRecorder(); foreach (var e in log) rec.Consume(e);
                var rep = rec.Build(m);
                Check(rep.checks.All(c => c.StartsWith("OK")), "RQ-001.7 CA4 cierres del registro", string.Join(" | ", rep.checks));
            }

            Console.WriteLine();
            Console.WriteLine($"{passed} OK · {failed} FALLA");
            foreach (var f in failures) Console.WriteLine("  FALLA " + f);
            return failed == 0 ? 0 : 1;
        }
    }
}
