using System;
using System.Collections.Generic;
using System.Linq;
using ClashDefense.Core;

namespace ClashDefense.Sim
{
    /// <summary>
    /// Jugadores automáticos para medir el balance antes del playtest (QA / Métricas).
    /// Modelo de jugador: ejecuta un plan de órdenes en secuencia, cada una apenas le alcanza el oro,
    /// con una demora de reacción. Construye en el mejor lugar libre para esa torre (máxima cobertura del recorrido).
    /// Es optimista respecto de un humano: no se distrae ni duda. Sirve de cota, no de predicción.
    /// </summary>
    static class Bot
    {
        public sealed class Order { public string Kind; public string Tower; public int Index; public override string ToString() => Kind == "B" ? $"+{Tower}" : $"^{Tower}{Index}"; }
        static Order Bd(string t) => new Order { Kind = "B", Tower = t };
        static Order Up(string t, int i) => new Order { Kind = "U", Tower = t, Index = i };

        static IEnumerable<Order> Many(string t, int n) { for (int i = 0; i < n; i++) yield return Bd(t); }

        public static readonly Dictionary<string, Order[]> Strategies = new Dictionary<string, Order[]>
        {
            // criterio del Doc 05 §11: ninguna torre resuelve sola las cinco oleadas
            ["solo_arqueras"]       = Many("arqueras", 12).Concat(Enumerable.Range(0, 12).Select(i => Up("arqueras", i))).ToArray(),
            ["arqueras+canones"]    = new[] { Bd("arqueras") }.Concat(Many("canon", 11)).Concat(Enumerable.Range(0, 11).Select(i => Up("canon", i))).ToArray(),
            ["arqueras+magos"]      = new[] { Bd("arqueras") }.Concat(Many("mago", 11)).Concat(Enumerable.Range(0, 11).Select(i => Up("mago", i))).ToArray(),
            // jugador que combina
            ["mixta"]               = new[] { Bd("arqueras"), Bd("arqueras"), Bd("canon"), Bd("mago"), Up("arqueras", 0), Bd("canon"), Up("arqueras", 1), Bd("mago"), Up("canon", 0), Bd("arqueras"), Up("mago", 0), Bd("canon"), Up("canon", 1), Bd("mago"), Up("mago", 1), Bd("arqueras"), Up("canon", 2), Up("arqueras", 2), Up("mago", 2), Up("arqueras", 3) },
            ["mixta_sin_mejoras"]   = new[] { Bd("arqueras"), Bd("arqueras"), Bd("canon"), Bd("mago"), Bd("canon"), Bd("arqueras"), Bd("mago"), Bd("canon"), Bd("arqueras"), Bd("mago"), Bd("canon"), Bd("arqueras"), Bd("mago"), Bd("canon") },
            ["mixta_mejora_primero"]= new[] { Bd("arqueras"), Up("arqueras", 0), Bd("canon"), Up("canon", 0), Bd("arqueras"), Bd("mago"), Up("arqueras", 1), Up("mago", 0), Bd("canon"), Up("canon", 1), Bd("arqueras"), Up("arqueras", 2), Bd("mago"), Up("mago", 1), Bd("canon"), Up("canon", 2) },
            // la cota baja: solo la torre del tutorial
            ["una_arqueras"]        = new[] { Bd("arqueras") },
        };

        public sealed class Result
        {
            public string Strategy; public string Outcome; public int Stars, Hp, WaveReached; public float Duration; public int Unspent;
            public int[] LeaksPerWave; public float[] WaveDurations; public string Towers; public MatchReport Report;
        }

        /// <summary>Lugares candidatos por tipo de torre, ordenados por cobertura del recorrido.</summary>
        public static List<(Vec2 pos, float cover)> RankSpots(Match m, TowerTypeData t, int level = 0, float step = 0.5f)
        {
            var list = new List<(Vec2, float)>();
            var a = m.Level.buildArea;
            float range = t.levels[level].range;
            for (float x = a.minX; x <= a.maxX; x += step)
                for (float z = a.minZ; z <= a.maxZ; z += step)
                {
                    var p = new Vec2(x, z);
                    if (m.CheckGeometry(t, p) != RejectReason.None) continue;
                    list.Add((p, m.Path.CoverageWithin(p, range, 0.25f)));
                }
            return list.OrderByDescending(s => s.Item2).ToList();
        }

        public static Result Play(Match m, List<SimEvent> log, Order[] plan, LevelData level, float reaction = 0.5f, bool tutorial = false, string name = "")
        {
            var cache = new Dictionary<string, List<(Vec2 pos, float cover)>>();
            var built = new Dictionary<string, List<int>>();
            int next = 0;
            float readySince = -1f;
            var rec = new MatchRecorder();
            int guard = (int)(1800f / Match.Step);

            while (!m.Ended && guard-- > 0)
            {
                if (m.State == MatchState.Tutorial)
                {
                    if (m.Tutorial == TutorialStep.SelectTower) m.NotifyTowerSelected(level.tutorialTowerId);
                    else if (m.Tutorial == TutorialStep.PlaceTower) { TryOrder(m, Bd(level.tutorialTowerId), cache, built); next = Math.Max(next, 1); }
                    else m.TutorialNext();
                }
                else if (next < plan.Length && (m.State == MatchState.Countdown || m.State == MatchState.Wave || m.State == MatchState.Interval))
                {
                    var o = plan[next];
                    int cost = o.Kind == "B" ? m.GetTowerType(o.Tower).cost
                             : (built.TryGetValue(o.Tower, out var l) && o.Index < l.Count && m.GetTower(l[o.Index]) != null ? m.UpgradeCost(m.GetTower(l[o.Index])) : -1);
                    if (cost < 0) next++; // la orden no aplica (torre inexistente): se saltea
                    else if (m.Gold >= cost)
                    {
                        if (readySince < 0) readySince = m.ActiveTime;
                        if (m.ActiveTime - readySince >= reaction)
                        {
                            if (TryOrder(m, o, cache, built)) next++;
                            else next++; // sin lugar: se saltea
                            readySince = -1f;
                        }
                    }
                    else readySince = -1f;
                }
                m.Tick();
                int before = log.Count;
                m.DrainEvents(log);
                for (int i = before; i < log.Count; i++) rec.Consume(log[i]);
            }
            var rep = rec.Build(m);
            return new Result
            {
                Strategy = name, Outcome = m.Result ?? "sin fin", Stars = m.Stars, Hp = m.BaseHp, WaveReached = m.WaveNumber,
                Duration = m.ActiveTime, Unspent = m.Gold, Report = rep,
                LeaksPerWave = rep.waves.Select(w => w.leaked).ToArray(),
                WaveDurations = rep.waves.Select(w => w.duration).ToArray(),
                Towers = string.Join(" ", rep.towersAtEnd.Where(t => t.level1 + t.level2 > 0).Select(t => $"{t.type}:{t.level1}+{t.level2}")),
            };
        }

        static bool TryOrder(Match m, Order o, Dictionary<string, List<(Vec2 pos, float cover)>> cache, Dictionary<string, List<int>> built)
        {
            if (o.Kind == "U")
            {
                var id = built[o.Tower][o.Index];
                return m.TryUpgrade(id, out _);
            }
            var type = m.GetTowerType(o.Tower);
            if (!cache.TryGetValue(o.Tower, out var spots)) { spots = RankSpots(m, type); cache[o.Tower] = spots; }
            foreach (var s in spots)
            {
                if (m.CheckPlacement(o.Tower, s.pos) != RejectReason.None) continue;
                if (m.TryBuild(o.Tower, s.pos, out var t, out _))
                {
                    if (!built.TryGetValue(o.Tower, out var l)) { l = new List<int>(); built[o.Tower] = l; }
                    l.Add(t.Id);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Guion fijo para la prueba de determinismo; con pausas intercaladas si se pide.</summary>
        public static void Scripted(Match m, List<SimEvent> log, bool withPauses)
        {
            var plan = new (float t, Action a)[]
            {
                (0.5f,  () => m.TryBuild("arqueras", new Vec2(0f, 3.5f), out _, out _)),
                (20f,   () => m.TryBuild("arqueras", new Vec2(-6f, 3.5f), out _, out _)),
                (40f,   () => m.TryUpgrade(1, out _)),
                (60f,   () => m.TryBuild("canon", new Vec2(6f, -3.5f), out _, out _)),
                (80f,   () => m.TrySell(2, out _, out _)),
                (85f,   () => m.TryBuild("mago", new Vec2(11f, 3.5f), out _, out _)),
            };
            int k = 0, guard = (int)(1500f / Match.Step), ticks = 0;
            while (!m.Ended && guard-- > 0)
            {
                while (k < plan.Length && m.ActiveTime >= plan[k].t) { plan[k].a(); k++; }
                if (withPauses && ticks % 700 == 350)
                {
                    m.Pause();
                    for (int i = 0; i < 90; i++) m.Tick();
                    m.Resume();
                }
                m.Tick();
                ticks++;
                m.DrainEvents(log);
            }
        }
    }
}
