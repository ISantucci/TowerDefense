using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClashDefense.Core;

namespace ClashDefense.Sim
{
    /// <summary>
    /// Instrumento de LDS-002.6: juega los seis niveles del Mundo 1 con jugadores automáticos, con las torres que el
    /// jugador tiene al llegar a cada nivel por primera vez, y mide duración, filtraciones, estrellas y oro sin usar.
    /// Cota optimista (reacción 0,5 s, mejores lugares): no reemplaza el playtest.
    /// </summary>
    static class World1
    {
        public static BalanceData Balance() => DataLoader.Balance("balance_w1.json");
        public static LevelData Level(string id) => DataLoader.Level($"level_{id}.json");
        public static CampaignData Campaign() => DataLoader.Load<CampaignData>("campaign_w1.json");

        public static readonly string[] Levels = { "m1_n1", "m1_n2", "m1_n3", "m1_n4", "m1_n5", "m1_n6" };
        static readonly string[] Unlocks = { "mortero", "bombardera", "electrica", "infernal", "oro", "lanzallamas" };
        static readonly string[] Initial = { "arqueras", "canon", "mago" };

        /// <summary>Todas las apariciones del nivel, oleada por oleada, con el mismo programa que usa la partida.</summary>
        public static IEnumerable<SpawnEntry> AllSpawns(BalanceData bal, LevelData lvl)
        {
            var starts = LevelGeometry.RoutePoints(lvl).Select(p => p[0]).ToList();
            foreach (var w in lvl.waves) foreach (var s in WaveSchedule.Build(w, bal.waveRules, starts)) yield return s;
        }

        public static List<string> TowersAt(int levelIndex)
        {
            var l = new List<string>(Initial);
            for (int i = 0; i < levelIndex; i++) l.Add(Unlocks[i]);
            return l;
        }

        // ------------------------------------------------------------------ planes
        static string[] Repeat(string[] cycle, int n) => Enumerable.Range(0, n).Select(i => cycle[i % cycle.Length]).ToArray();

        public static Dictionary<string, string[]> Plans(int li)
        {
            var towers = TowersAt(li);
            var p = new Dictionary<string, string[]>();
            string[] comp;
            switch (li)
            {
                case 0: comp = new[] { "arqueras", "canon", "^", "mago", "^", "arqueras", "canon", "^", "mago", "^" }; break;
                case 1: comp = new[] { "arqueras", "canon", "mortero", "^", "mago", "^", "arqueras", "mortero", "^", "canon" }; break;
                case 2: comp = new[] { "arqueras", "canon", "bombardera", "^", "mago", "arqueras", "^", "mortero", "^", "arqueras" }; break;
                case 3: comp = new[] { "arqueras", "canon", "electrica", "^", "bombardera", "mago", "^", "mortero", "^", "arqueras", "electrica" }; break;
                // w1-0.2 (Doc 05 v2.0 §9.2): desde la primera oleada llegan unidades por las dos puertas o ramales; el jugador competente
                // abre con dos Arqueras (lo más barato que cubre tierra y aire) antes de invertir en torres caras u Oro
                case 4: comp = new[] { "arqueras", "arqueras", "canon", "infernal", "^", "electrica", "bombardera", "^", "arqueras", "mortero", "^", "infernal" }; break;
                default: comp = new[] { "arqueras", "arqueras", "canon", "oro", "infernal", "^", "electrica", "oro", "bombardera", "^", "arqueras", "mortero", "^", "infernal", "^", "mago" }; break;
            }
            p["competente"] = Repeat(comp, 400);
            p["sin_mejoras"] = Repeat(comp.Where(c => c != "^").ToArray(), 400);
            p["solo_arqueras"] = Repeat(new[] { "arqueras", "arqueras", "^" }, 400);
            p["solo_canon"] = new[] { "arqueras" }.Concat(Repeat(new[] { "canon", "canon", "^" }, 400)).ToArray();
            p["lento"] = Repeat(comp, 400); // mismo plan con reacción de 4 s: un jugador que duda
            // Doc 05 §15: la Torre de oro no debe ser compra obligatoria → el mismo plan sin Oro
            if (comp.Contains("oro")) p["sin_oro"] = Repeat(comp.Where(c => c != "oro").ToArray(), 400);
            return p;
        }

        // ------------------------------------------------------------------ bot
        public sealed class Result
        {
            public string Outcome; public int Stars, Hp, Wave; public float Duration; public int Unspent, Collected;
            public int[] Leaks; public string Towers; public MatchReport Report; public float[] WaveDur;
            public float[] Depth; // por oleada: fracción máxima del recorrido que alcanzó un enemigo (1 = llegó)
        }

        public static List<(Vec2 pos, float cover)> RankSpots(Match m, TowerTypeData t, float step = 0.5f)
        {
            var list = new List<(Vec2, float)>();
            var a = m.Level.buildArea;
            var s = t.levels[0];
            for (float x = a.minX; x <= a.maxX; x += step)
                for (float z = a.minZ; z <= a.maxZ; z += step)
                {
                    var p = new Vec2(x, z);
                    if (m.CheckGeometry(t, p) != RejectReason.None) continue;
                    float cover = 0f;
                    if (t.attack == AttackKind.Gold) cover = -m.Routes.Sum(r => r.CoverageWithin(p, 8f, 0.5f)); // la de oro va donde no estorba
                    else foreach (var r in m.Routes)
                        {
                            cover += r.CoverageWithin(p, s.range, 0.25f);
                            if (s.minRange > 0f) cover -= r.CoverageWithin(p, s.minRange, 0.25f);
                        }
                    list.Add((p, cover));
                }
            return list.OrderByDescending(s2 => s2.Item2).ToList();
        }

        public static Result Play(Match m, string[] plan, float reaction, List<SimEvent> log = null)
        {
            log = log ?? new List<SimEvent>();
            var cache = new Dictionary<string, List<(Vec2 pos, float cover)>>();
            int next = 0;
            float readySince = -1f;
            var rec = new MatchRecorder();
            int guard = (int)(3600f / Match.Step);
            m.Begin();
            int drained = 0;
            var depth = new Dictionary<int, float>();
            while (!m.Ended && guard-- > 0)
            {
                foreach (var e in m.Enemies)
                {
                    float f = e.Distance / m.RouteOf(e).Length;
                    if (!depth.TryGetValue(e.Wave, out var d) || f > d) depth[e.Wave] = Math.Min(1f, f);
                }
                if (m.State == MatchState.Tutorial)
                {
                    if (m.Tutorial == TutorialStep.SelectTower) m.NotifyTowerSelected(m.Level.tutorialTowerId);
                    else if (m.Tutorial == TutorialStep.PlaceTower) Build(m, m.Level.tutorialTowerId, cache);
                    else m.TutorialNext();
                }
                else if (m.State == MatchState.Wave || m.State == MatchState.Interval || m.State == MatchState.Countdown)
                {
                    // recoger oro: el jugador mira la torre cuando se llena (con su demora)
                    foreach (var t in m.Towers.ToList())
                        if (t.Type.attack == AttackKind.Gold && t.Full) m.TryCollect(t.Id, out _, out _);
                    while (next < plan.Length)
                    {
                        string o = plan[next];
                        int cost;
                        Tower up = null;
                        if (o == "^")
                        {
                            up = m.Towers.FirstOrDefault(t => t.CanUpgrade && t.Type.attack != AttackKind.Gold) ?? m.Towers.FirstOrDefault(t => t.CanUpgrade);
                            if (up == null) { next++; continue; }
                            cost = m.UpgradeCost(up);
                        }
                        else
                        {
                            var tt = m.GetTowerType(o);
                            if (tt == null) { next++; continue; }
                            cost = tt.cost;
                        }
                        if (m.Gold < cost) { readySince = -1f; break; }
                        if (readySince < 0) readySince = m.ActiveTime;
                        if (m.ActiveTime - readySince < reaction) break;
                        if (up != null) m.TryUpgrade(up.Id, out _);
                        else Build(m, o, cache);
                        next++;
                        readySince = -1f;
                        break;
                    }
                }
                m.Tick();
                m.DrainEvents(log);
                for (; drained < log.Count; drained++) rec.Consume(log[drained]);
            }
            var rep = rec.Build(m);
            return new Result
            {
                Outcome = m.Result ?? "sin fin", Stars = m.Stars, Hp = m.BaseHp, Wave = m.WaveNumber, Duration = m.ActiveTime,
                Unspent = m.Gold, Collected = rep.gold.collected, Report = rep,
                Leaks = rep.waves.Select(w => w.leaked).ToArray(), WaveDur = rep.waves.Select(w => w.duration).ToArray(),
                Depth = rep.waves.Select(w => depth.TryGetValue(w.n, out var d) ? d : 0f).ToArray(),
                Towers = string.Join(" ", rep.towersAtEnd.Where(t => t.level1 + t.level2 > 0).Select(t => $"{Abbr(t.type)}{t.level1}+{t.level2}")),
            };
        }

        static string Abbr(string id) => id.Substring(0, Math.Min(3, id.Length));

        static bool Build(Match m, string type, Dictionary<string, List<(Vec2 pos, float cover)>> cache)
        {
            var tt = m.GetTowerType(type);
            if (tt == null) return false;
            if (!cache.TryGetValue(type, out var spots)) { spots = RankSpots(m, tt); cache[type] = spots; }
            if (type == m.Level.tutorialTowerId && m.State == MatchState.Tutorial && m.Level.tutorialHint != null && m.Level.tutorialHint.radius > 0f)
                if (m.TryBuild(type, m.Level.tutorialHint.Center, out _, out _)) return true;
            foreach (var s in spots)
                if (m.CheckPlacement(type, s.pos) == RejectReason.None && m.TryBuild(type, s.pos, out _, out _)) return true;
            return false;
        }

        // ------------------------------------------------------------------ comando
        public static int Run(string[] only)
        {
            var bal = Balance();
            Console.WriteLine($"Mundo 1 — balance {bal.version}. Bots con las torres que el jugador tiene la primera vez que llega a cada nivel.");
            int bad = 0;
            for (int li = 0; li < Levels.Length; li++)
            {
                string id = Levels[li];
                if (only.Length > 0 && !only.Contains(id)) continue;
                var lvl = Level(id);
                var errs = DataValidator.Validate(bal, lvl);
                if (errs.Count > 0) { Console.WriteLine($"{id}: DATOS INVÁLIDOS\n  " + string.Join("\n  ", errs)); bad++; continue; }
                var probe = new Match(bal, lvl, false);
                int enemies = 0, gold = 0;
                foreach (var s in AllSpawns(bal, lvl)) { enemies++; gold += bal.enemies.First(e => e.code == s.Code).gold; }
                Console.WriteLine($"\n{id} «{lvl.displayName}» · {lvl.waves.Length} oleadas · {enemies} enemigos · {gold} de oro posible · recorridos {string.Join("/", probe.Routes.Select(r => r.Length.ToString("0")))} u · torres {string.Join(",", TowersAt(li))}");
                Console.WriteLine($"  {"estrategia",-14} {"resultado",-9} {"★",1} {"vida",4} {"ol.",3} {"duración",8} {"sin usar",8} {"recog.",6}  filtrados por oleada   torres");
                foreach (var kv in Plans(li))
                {
                    var opts = new MatchOptions { Tutorial = !string.IsNullOrEmpty(lvl.tutorialTowerId), AllowedTowers = TowersAt(li) };
                    var m = new Match(Balance(), Level(id), opts);
                    var r = Play(m, kv.Value, kv.Key == "lento" ? 4f : 0.5f);
                    Console.WriteLine($"  {kv.Key,-14} {r.Outcome,-9} {r.Stars,1} {r.Hp,4} {r.Wave,3} {MatchRecorder.Clock(r.Duration),8} {r.Unspent,8} {r.Collected,6}  [{string.Join(" ", r.Leaks)}] prof [{string.Join(" ", r.Depth.Select(d => (d * 10).ToString("0")))}]  {r.Towers}");
                    if (r.Report.checks.Any(c => !c.StartsWith("OK"))) { bad++; Console.WriteLine("   " + string.Join(" | ", r.Report.checks)); }
                }
            }
            return bad == 0 ? 0 : 1;
        }

        /// <summary>Duración de cada oleada con el plan competente (para afinar el LDS).</summary>
        public static int Waves(string id)
        {
            int li = Array.IndexOf(Levels, id);
            var lvl = Level(id);
            var m = new Match(Balance(), lvl, new MatchOptions { Tutorial = false, AllowedTowers = TowersAt(li) });
            var r = Play(m, Plans(li)["competente"], 0.5f);
            Console.WriteLine($"{id}: {r.Outcome} ★{r.Stars} vida {r.Hp} en {MatchRecorder.Clock(r.Duration)}");
            for (int i = 0; i < r.WaveDur.Length; i++) Console.WriteLine($"  oleada {i + 1}: {r.WaveDur[i],5:0.0} s · filtrados {r.Leaks[i]} · profundidad {r.Depth[i]:0.00}");
            return 0;
        }
    }
}
