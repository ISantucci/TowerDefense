using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClashDefense.Core;

namespace ClashDefense.Sim
{
    static class Program
    {
        static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            string cmd = args.Length > 0 ? args[0] : "test";
            if (args.Length > 1 && !args[1].StartsWith("--")) DataLoader.DataDir = args[1];
            switch (cmd)
            {
                case "test": { int a = Tests.Run(); int b = Tests1.RunAll(); return a == 0 && b == 0 ? 0 : 1; }
                case "bots": return Bots(args.Skip(2).ToArray());
                case "mapa": return Map();
                case "lds": return Lds.Run();
                case "mundo1": return World1.Run(args.Skip(2).ToArray());
                case "oleadas": return World1.Waves(args.Length > 2 ? args[2] : "m1_n1");
                case "economia": return Economy.Run(args.Skip(1).ToArray());
                default:
                    Console.WriteLine("uso: dotnet run -- test|bots|mapa|lds|mundo1|oleadas|economia [carpeta de datos] [nivel] [--salida carpeta]");
                    return 2;
            }
        }

        static int Map()
        {
            var m = new Match(DataLoader.Balance(), DataLoader.Level(), false);
            Console.WriteLine($"Nivel {m.Level.id}: recorrido L = {m.Path.Length:0.0} u");
            foreach (var e in m.Balance.enemies)
                Console.WriteLine($"  {e.id,-10} v = {m.Path.Length / e.travelTime:0.00} u/s" + (e.armor > 0 ? $" (expuesto {m.Path.Length / e.travelTimeExposed:0.00})" : ""));
            float vD = m.Path.Length / m.Balance.enemies.First(e => e.code == "D").travelTime;
            foreach (var t in m.Balance.towers)
            {
                var spots = Bot.RankSpots(m, t);
                float straight = 2f * t.levels[0].range;
                Console.WriteLine($"\n{t.id} (alcance {t.levels[0].range}): {spots.Count} lugares válidos; recta de referencia {straight:0.0} u");
                var chosen = new List<(Vec2 pos, float cover)>();
                foreach (var s in spots)
                {
                    if (chosen.Any(c => Vec2.Distance(c.pos, s.pos) < 3f)) continue;
                    chosen.Add(s);
                    if (chosen.Count == 6) break;
                }
                foreach (var s in chosen)
                    Console.WriteLine($"  {s.pos,-14} cubre {s.cover,5:0.0} u  ({s.cover / straight:0.00}× recta)  exposición D {s.cover / vD:0.0} s");
                float median = spots[spots.Count / 2].cover;
                Console.WriteLine($"  mediana de los lugares válidos: {median:0.0} u");
            }
            return 0;
        }

        static int Bots(string[] only)
        {
            var bal = DataLoader.Balance();
            var lvl = DataLoader.Level();
            Console.WriteLine($"Bots — balance {bal.version}, nivel {lvl.id}");
            Console.WriteLine($"{"estrategia",-22} {"resultado",-9} {"★",2} {"vida",4} {"ol.",3} {"duración",8} {"sin usar",8}  filtrados por oleada   torres");
            int bad = 0;
            foreach (var kv in Bot.Strategies)
            {
                if (only.Length > 0 && !only.Contains(kv.Key)) continue;
                foreach (bool tut in new[] { false })
                {
                    var m = new Match(DataLoader.Balance(), DataLoader.Level(), tut);
                    var log = new List<SimEvent>();
                    m.Begin();
                    var r = Bot.Play(m, log, kv.Value, lvl, 0.5f, tut, kv.Key);
                    Console.WriteLine($"{kv.Key,-22} {r.Outcome,-9} {r.Stars,2} {r.Hp,4} {r.WaveReached,3} {MatchRecorder.Clock(r.Duration),8} {r.Unspent,8}  [{string.Join(" ", r.LeaksPerWave)}]  {r.Towers}");
                    if (r.Report.checks.Any(c => !c.StartsWith("OK"))) { bad++; Console.WriteLine("   " + string.Join(" | ", r.Report.checks)); }
                }
            }
            return bad == 0 ? 0 : 1;
        }
    }
}
