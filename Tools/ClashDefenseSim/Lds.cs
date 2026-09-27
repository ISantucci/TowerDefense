using System;
using System.Collections.Generic;
using System.Linq;
using ClashDefense.Core;

namespace ClashDefense.Sim
{
    /// <summary>Instrumento del LDS: presión de la oleada 1 según dónde se pone la Arqueras del tutorial.</summary>
    static class Lds
    {
        public static int Run()
        {
            var probe = new Match(DataLoader.Balance(), DataLoader.Level(), false);
            var spots = Bot.RankSpots(probe, probe.GetTowerType("arqueras"));
            var picks = new (string name, Vec2 pos, float cover)[]
            {
                ("mejor", spots[0].pos, spots[0].cover),
                ("p75", spots[spots.Count / 4].pos, spots[spots.Count / 4].cover),
                ("mediana", spots[spots.Count / 2].pos, spots[spots.Count / 2].cover),
                ("pista", HintPos(probe), probe.Path.CoverageWithin(HintPos(probe), 8f, 0.25f)),
                ("x0", new Vec2(0f, 3.5f), probe.Path.CoverageWithin(new Vec2(0f, 3.5f), 8f, 0.25f)),
                ("x-4", new Vec2(-4f, 3.5f), probe.Path.CoverageWithin(new Vec2(-4f, 3.5f), 8f, 0.25f)),
            };
            Console.WriteLine("Oleada 1 con UNA Arqueras (tutorial) y segunda compra automática al llegar a 100 de oro (reacción 1 s)");
            Console.WriteLine($"{"lugar",-8} {"pos",-12} {"cubre",6}  {"5ª baja",7} {"1ª filtr.",9} {"filtr. sin 2ª",13} {"filtr. con 2ª",13}");
            foreach (var p in picks)
            {
                var (k5, firstLeak, leaksAlone) = Wave1(p.pos, false);
                var (_, _, leaksWith) = Wave1(p.pos, true);
                Console.WriteLine($"{p.name,-8} {p.pos,-12} {p.cover,6:0.0}  {Fmt(k5),7} {Fmt(firstLeak),9} {leaksAlone,13} {leaksWith,13}");
            }
            return 0;
        }

        public static Vec2 HintPos(Match m) => m.Level.tutorialHint != null ? m.Level.tutorialHint.Center : new Vec2(4.5f, 3.5f);

        static string Fmt(float t) => t < 0 ? "—" : t.ToString("0.0") + " s";

        static (float kill5, float firstLeak, int leaks) Wave1(Vec2 pos, bool buySecond)
        {
            var b = DataLoader.Balance();
            b.waves = new[] { b.waves[0] };
            var m = new Match(b, DataLoader.Level(), false);
            var log = new List<SimEvent>();
            m.Begin();
            m.TryBuild("arqueras", pos, out _, out _);
            float waveStart = -1, affordable = -1;
            bool bought = false;
            var arch = m.GetTowerType("arqueras");
            var ranked = Bot.RankSpots(m, arch);
            while (!m.Ended)
            {
                m.Tick();
                m.DrainEvents(log);
                if (waveStart < 0 && m.State == MatchState.Wave) waveStart = m.ActiveTime;
                if (buySecond && !bought && m.Gold >= 100)
                {
                    if (affordable < 0) affordable = m.ActiveTime;
                    if (m.ActiveTime - affordable >= 1f)
                        foreach (var s in ranked) if (m.TryBuild("arqueras", s.pos, out _, out _)) { bought = true; break; }
                }
            }
            var kills = log.Where(e => e.Type == SimEventType.EnemyKilled).ToList();
            var leaks = log.Where(e => e.Type == SimEventType.EnemyReachedBase).ToList();
            return (kills.Count >= 5 ? kills[4].Time - waveStart : -1f, leaks.Count > 0 ? leaks[0].Time - waveStart : -1f, leaks.Count);
        }
    }
}
