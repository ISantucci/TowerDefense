using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClashDefense.Core;

namespace ClashDefense.Sim
{
    /// <summary>
    /// Instrumento de MET-004.1 (SOL-004.1): la economía del Mundo 1 medida con los bots de World1.
    /// Holgura por escalado de las fuentes de oro, sondas degeneradas, destino del gasto, ingreso por oleada
    /// y economía meta (cristales contra tienda). Escribe un CSV con las columnas de metricas.py datos.
    /// Cota optimista: el bot no duda ni se equivoca de lugar.
    /// </summary>
    static class Economy
    {
        static string Version = "w1";
        const int C = 100; // costo de la torre base (Doc 02 §9)
        static readonly string[] SlackPlans = { "competente", "solo_arqueras" };

        public static float[] Factors()
        {
            var l = new List<float>();
            for (int i = 20; i >= 2; i--) l.Add(i * 0.05f);
            return l.ToArray();
        }

        /// <summary>Copia del balance con las fuentes de la partida escaladas por f. El oro inicial no cambia.</summary>
        public static BalanceData Scale(BalanceData b, float f)
        {
            var c = DataLoader.Clone(b);
            foreach (var e in c.enemies)
                if (e.gold > 0) e.gold = Math.Max(1, (int)Math.Round(e.gold * f));
            foreach (var t in c.towers)
                if (t.attack == AttackKind.Gold)
                    foreach (var lv in t.levels) { lv.goldPerSecond *= f; lv.goldPerCycle *= f; }
            return c;
        }

        sealed class Corrida
        {
            public string Level, Plan; public float F; public World1.Result R; public List<SimEvent> Log;
        }

        static Corrida Play(int li, string plan, float f, bool keepLog)
        {
            string id = World1.Levels[li];
            var lvl = World1.Level(id);
            var bal = f >= 0.999f ? World1.Balance() : Scale(World1.Balance(), f);
            var opts = new MatchOptions { Tutorial = !string.IsNullOrEmpty(lvl.tutorialTowerId), AllowedTowers = World1.TowersAt(li) };
            var m = new Match(bal, lvl, opts);
            var log = new List<SimEvent>();
            var r = World1.Play(m, World1.Plans(li)[plan], plan == "lento" ? 4f : 0.5f, log);
            return new Corrida { Level = id, Plan = plan, F = f, R = r, Log = keepLog ? log : null };
        }

        static string Fs(float f) => f.ToString("0.00", CultureInfo.InvariantCulture);
        static string Csv(string s) => s == null ? "" : (s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s);

        /// <summary>f* = el menor f con la condición, que también la cumple en todos los f mayores. Devuelve (f*, hay hueco).</summary>
        static (float fStar, bool gap, bool never) Frontier(List<(float f, bool ok)> scan)
        {
            float fStar = float.NaN; bool broke = false, gap = false;
            foreach (var (f, ok) in scan.OrderByDescending(x => x.f))
            {
                if (!broke && ok) fStar = f;
                else if (!ok) broke = true;
                else if (broke && ok) gap = true;
            }
            return (fStar, gap, float.IsNaN(fStar));
        }

        static string Holgura((float fStar, bool gap, bool never) fr, float minF)
        {
            if (fr.never) return "< 1";
            string h = fr.fStar <= minF + 1e-4f ? $"> {1f / minF:0}" : (1f / fr.fStar).ToString("0.00", CultureInfo.InvariantCulture);
            return fr.gap ? h + " *" : h;
        }

        public static int Run(string[] args)
        {
            string outDir = ".";
            var onlyLevels = new HashSet<string>();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--salida") outDir = args[i + 1];
                if (args[i] == "--nivel") onlyLevels.Add(args[i + 1]);
            }
            Directory.CreateDirectory(outDir);
            var csv = new StringBuilder();
            csv.AppendLine("timestamp,player_id,session_id,event,version,environment,flow,currency,amount,reason,wave,level_id,strategy,f,result,stars,hp,towers,unspent,seconds");
            var factors = Factors();
            float minF = factors.Min();
            int bad = 0;
            var durations = new Dictionary<string, float>();

            var bal0 = World1.Balance();
            Version = bal0.version;
            Console.WriteLine($"Economía del Mundo 1 — balance {Version}. Bots de World1, sin mejoras permanentes. Instrumento de MET-004.1.");
            Console.WriteLine($"Holgura = 1/f*, f* = menor multiplicador de las fuentes (baja y Torre de oro) con el que se mantiene el resultado; oro inicial fijo " +
                $"({(bal0.economy.tutorialStartGold > 0 ? $"{bal0.economy.tutorialStartGold} con tutorial, " : "")}{bal0.economy.startGold}).");
            Console.WriteLine("  * = frontera no monótona (algún f menor que f* vuelve a cumplir)\n");

            var summary = new List<string>();
            var kpis = new List<Kpi>();
            for (int li = 0; li < World1.Levels.Length; li++)
            {
                string id = World1.Levels[li];
                if (onlyLevels.Count > 0 && !onlyLevels.Contains(id)) continue;
                var lvl = World1.Level(id);
                var bal = World1.Balance();
                var errs = DataValidator.Validate(bal, lvl);
                if (errs.Count > 0) { Console.WriteLine($"{id}: DATOS INVÁLIDOS\n  " + string.Join("\n  ", errs)); bad++; continue; }

                int possible = 0;
                foreach (var s in World1.AllSpawns(bal, lvl)) possible += bal.enemies.First(e => e.code == s.Code).gold;
                Console.WriteLine($"{id} «{lvl.displayName}» · {lvl.waves.Length} oleadas · oro por bajas {possible} ({possible / (float)C:0.0} C) · torres {string.Join(",", World1.TowersAt(li))}");

                // 1) todas las estrategias con f = 1, con flujos de oro
                Corrida comp = null;
                foreach (var plan in World1.Plans(li).Keys)
                {
                    var run = Play(li, plan, 1f, true);
                    if (plan == "competente") comp = run;
                    WriteRun(csv, run, true);
                    int built = run.R.Report.towersAtEnd.Sum(t => t.level1 + t.level2), up = run.R.Report.towersAtEnd.Sum(t => t.level2);
                    Console.WriteLine($"  f=1.00 {plan,-14} {run.R.Outcome,-9} ★{run.R.Stars} vida {run.R.Hp,3}  {MatchRecorder.Clock(run.R.Duration)}  torres {built,3} (mejoradas {up,3})  sin usar {run.R.Unspent,5}");
                    if (run.R.Report.checks.Any(c => !c.StartsWith("OK"))) { bad++; Console.WriteLine("   " + string.Join(" | ", run.R.Report.checks)); }
                }
                durations[id] = comp.R.Duration;
                kpis.Add(MeasureKpi(li, lvl, bal, comp, World1.Plans(li).Keys.Where(k => k != "competente").Select(k => k).ToList()));

                // 2) destino del gasto, ingreso por oleada y combinación del competente
                var g = comp.Log.Where(e => e.Type == SimEventType.GoldChanged).ToList();
                int build = -g.Where(e => e.Text == "construccion").Sum(e => e.Int1);
                int upg = -g.Where(e => e.Text == "mejora").Sum(e => e.Int1);
                int earned = g.Where(e => e.Text == "baja").Sum(e => e.Int1), collected = g.Where(e => e.Text == "recoleccion").Sum(e => e.Int1);
                var perWave = IncomePerWave(comp.Log);
                var dmg = comp.R.Report.damageByTowerType.Where(d => d.damage > 0).OrderByDescending(d => d.damage).ToList();
                float total = dmg.Sum(d => d.damage);
                int combined = dmg.Count(d => d.damage >= 0.15f * total);
                string top = dmg.Count > 0 ? $"{dmg[0].type} {100f * dmg[0].damage / total:0}%" : "-";
                Console.WriteLine($"  competente: ingreso {earned + collected} (bajas {earned}, Torre de oro {collected}) · gasto {build + upg}: construir {100f * build / Math.Max(1, build + upg):0}% · mejorar {100f * upg / Math.Max(1, build + upg):0}%");
                Console.WriteLine($"              ingreso por oleada en C: [{string.Join(" ", perWave.Select(x => (x / (float)C).ToString("0.0", CultureInfo.InvariantCulture)))}] · tipos con ≥15% del daño: {combined} (mayor: {top}) · vida perdida {100 - comp.R.Hp}");

                // 3) holgura por escalado
                var cells = new List<string>();
                foreach (var plan in SlackPlans)
                {
                    var three = new List<(float, bool)>(); var win = new List<(float, bool)>();
                    var row = new StringBuilder();
                    foreach (var f in factors)
                    {
                        var run = f >= 0.999f && plan == "competente" ? comp : Play(li, plan, f, false);
                        if (!(f >= 0.999f)) WriteRun(csv, run, false);
                        three.Add((f, run.R.Outcome == "victoria" && run.R.Stars == 3));
                        win.Add((f, run.R.Outcome == "victoria"));
                        row.Append(run.R.Outcome == "victoria" ? run.R.Stars.ToString() : "x");
                    }
                    string h3 = Holgura(Frontier(three), minF), hv = Holgura(Frontier(win), minF);
                    Console.WriteLine($"  holgura {plan,-14} 3★ {h3,-7} victoria {hv,-7} estrellas por f (1.00→{Fs(minF)}): {row}");
                    cells.Add($"{h3}|{hv}");
                }
                summary.Add($"{id,-6} {cells[0].Split('|')[0],-8} {cells[0].Split('|')[1],-9} {cells[1].Split('|')[0],-8} {cells[1].Split('|')[1],-9}");
                Console.WriteLine();
            }

            Console.WriteLine("RESUMEN — holgura (1/f*)");
            Console.WriteLine($"{"nivel",-6} {"comp 3★",-8} {"comp vic",-9} {"arq 3★",-8} {"arq vic",-9}");
            foreach (var s in summary) Console.WriteLine(s);

            PrintKpis(kpis);
            Meta(durations);

            string path = Path.Combine(outDir, "economia_w1.csv");
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"\nCSV: {Path.GetFullPath(path)}");
            return bad == 0 ? 0 : 1;
        }

        sealed class Kpi
        {
            public string Level; public float Duration, TargetMin, TargetMax, WindowMin, WindowMax;
            public string Outcome; public int Stars, Unspent, ActionsWave1; public string TopSpend; public float TopShare;
            public int DamageTypes; public List<(string plan, string outcome, int stars, float duration, int unspent)> Others = new List<(string, string, int, float, int)>();
        }

        /// <summary>Ventana estructural de duración: el mínimo si cada oleada termina con su última aparición y el máximo si la
        /// unidad que más tarda de cada oleada camina todo el recorrido (más, sería una filtración).</summary>
        static (float min, float max) Window(BalanceData bal, LevelData lvl)
        {
            var starts = LevelGeometry.RoutePoints(lvl).Select(p => p[0]).ToList();
            float pre = 3f * bal.timing.countdownStep + bal.timing.defenseDuration;
            float min = pre + (lvl.waves.Length - 1) * bal.timing.waveGap, max = min;
            foreach (var w in lvl.waves)
            {
                var sched = WaveSchedule.Build(w, bal.waveRules, starts);
                min += (float)sched.Max(x => x.Time);
                max += (float)sched.Max(x => x.Time + bal.enemies.First(e => e.code == x.Code).travelTime);
            }
            return (min, max);
        }

        static (float a, float b) Target(string s)
        {
            if (string.IsNullOrEmpty(s)) return (float.NaN, float.NaN);
            var parts = new string(s.Where(c => char.IsDigit(c) || c == '–' || c == '-').ToArray()).Split('–', '-');
            return parts.Length >= 2 && float.TryParse(parts[0], out var a) && float.TryParse(parts[1], out var b) ? (a * 60f, b * 60f) : (float.NaN, float.NaN);
        }

        static Kpi MeasureKpi(int li, LevelData lvl, BalanceData bal, Corrida comp, List<string> others)
        {
            var camp = World1.Campaign();
            var meta = camp.worlds.SelectMany(w => w.continents).SelectMany(c => c.levels).FirstOrDefault(l => l.id == lvl.id);
            var (tMin, tMax) = Target(meta?.durationTarget);
            var (wMin, wMax) = Window(bal, lvl);
            var k = new Kpi { Level = lvl.id, Duration = comp.R.Duration, TargetMin = tMin, TargetMax = tMax, WindowMin = wMin, WindowMax = wMax,
                              Outcome = comp.R.Outcome, Stars = comp.R.Stars, Unspent = comp.R.Unspent };
            // acciones de compra o mejora hasta que termina la oleada 1 (Doc 05 §14: segunda torre en la oleada 1)
            foreach (var e in comp.Log)
            {
                if (e.Type == SimEventType.WaveCleared && e.Int1 == 1) break;
                if (e.Type == SimEventType.TowerBuilt || e.Type == SimEventType.TowerUpgraded) k.ActionsWave1++;
            }
            // gasto por tipo de torre (construir + mejorar)
            var spend = new Dictionary<string, int>();
            foreach (var e in comp.Log)
                if (e.Type == SimEventType.TowerBuilt || e.Type == SimEventType.TowerUpgraded)
                    spend[e.Text] = (spend.TryGetValue(e.Text, out var v) ? v : 0) + e.Int1;
            int total = spend.Values.Sum();
            var top = spend.OrderByDescending(x => x.Value).FirstOrDefault();
            k.TopSpend = top.Key ?? "-"; k.TopShare = total > 0 ? top.Value / (float)total : 0f;
            var dmg = comp.R.Report.damageByTowerType.Where(d => d.damage > 0).ToList();
            float dt = dmg.Sum(d => d.damage);
            k.DamageTypes = dmg.Count(d => d.damage >= 0.15f * dt);
            return k;
        }

        static void PrintKpis(List<Kpi> kpis)
        {
            Console.WriteLine("\nINDICADORES DEL DOC 05 §11 Y §14 QUE UN BOT PUEDE MEDIR (plan competente)");
            Console.WriteLine($"{"nivel",-6} {"duración",8} {"objetivo",-11} {"ventana posible",-15} {"en obj.",-7} {"★",1} {"sin usar",8} {"<2 torres",-9} {"acc. ol.1",9} {"mayor gasto",-18} {"tipos≥15%",9}");
            int inTarget = 0, lowUnspent = 0, twoActions = 0, wins = 0;
            foreach (var k in kpis)
            {
                bool inT = k.Duration >= k.TargetMin && k.Duration <= k.TargetMax;
                bool reachable = k.WindowMax >= k.TargetMin;
                if (inT) inTarget++;
                if (k.Outcome == "victoria") { wins++; if (k.Unspent < 200) lowUnspent++; }
                if (k.ActionsWave1 >= 2) twoActions++;
                string target = float.IsNaN(k.TargetMin) ? "-" : $"{k.TargetMin / 60:0}–{k.TargetMax / 60:0} min";
                string window = $"{MatchRecorder.Clock(k.WindowMin)}–{MatchRecorder.Clock(k.WindowMax)}" + (reachable ? "" : " !");
                Console.WriteLine($"{k.Level,-6} {MatchRecorder.Clock(k.Duration),8} {target,-11} {window,-15} {(inT ? "sí" : "no"),-7} {k.Stars,1} {k.Unspent,8} {(k.Unspent < 200 ? "sí" : "no"),-9} {k.ActionsWave1,9} {k.TopSpend + " " + (100 * k.TopShare).ToString("0") + "%",-18} {k.DamageTypes,9}");
            }
            Console.WriteLine($"  partidas dentro de su duración objetivo: {inTarget}/{kpis.Count} (objetivo ≥ 80 %)");
            Console.WriteLine($"  oro sin usar menor al costo de dos torres base (200): {lowUnspent}/{wins} victorias (objetivo ≥ 75 %)");
            Console.WriteLine($"  compra o mejora de una segunda torre durante la oleada 1: {twoActions}/{kpis.Count} (objetivo ≥ 70 % de jugadores)");
            Console.WriteLine($"  gasto del plan competente en un solo tipo: máximo {kpis.Max(k => k.TopShare) * 100:0}% (objetivo global < 50 %)");
            Console.WriteLine("  ventana posible: de la última aparición de cada oleada (mínimo) a que la unidad más lenta de cada oleada camine todo el recorrido (máximo); «!» = el objetivo no entra en la ventana");
            Console.WriteLine("  no medibles con bots: finalización por nivel, venta en victorias, derrotas sin causa identificable (playtest, MET-004.1)");
        }

        static int[] IncomePerWave(List<SimEvent> log)
        {
            var income = new SortedDictionary<int, int>();
            int wave = 0;
            foreach (var e in log)
            {
                if (e.Type == SimEventType.WaveStarted) wave = e.Int1;
                else if (e.Type == SimEventType.GoldChanged && (e.Text == "baja" || e.Text == "recoleccion") && wave > 0)
                    income[wave] = (income.TryGetValue(wave, out var v) ? v : 0) + e.Int1;
            }
            return income.Values.ToArray();
        }

        static void WriteRun(StringBuilder csv, Corrida run, bool flows)
        {
            string player = "bot_" + run.Plan, session = $"{run.Level}_{run.Plan}_f{Fs(run.F)}";
            string common = $"{Version},simulacion";
            if (flows && run.Log != null)
            {
                // Un evento por paso y por sentido: lo que entra (o sale) en el mismo paso de 1/60 s se suma en una fila,
                // porque metricas.py identifica un evento por jugador, sesión, nombre e instante. Si en ese paso hubo
                // más de un motivo, reason los junta con "+".
                int wave = 0;
                var rows = new List<(float time, string reason, int delta, int wave)>();
                foreach (var e in run.Log)
                {
                    if (e.Type == SimEventType.WaveStarted) wave = e.Int1;
                    if (e.Type != SimEventType.GoldChanged || e.Int1 == 0) continue;
                    int j = rows.FindLastIndex(x => x.time == e.Time && Math.Sign(x.delta) == Math.Sign(e.Int1));
                    if (j >= 0)
                        rows[j] = (e.Time, rows[j].reason.Split('+').Contains(e.Text) ? rows[j].reason : rows[j].reason + "+" + e.Text, rows[j].delta + e.Int1, wave);
                    else rows.Add((e.Time, e.Text, e.Int1, wave));
                }
                foreach (var r in rows)
                {
                    string flow = r.delta > 0 ? "source" : "sink";
                    csv.AppendLine(string.Join(",", r.time.ToString("0.000", CultureInfo.InvariantCulture), player, session, flow == "source" ? "oro_entra" : "oro_sale", common, flow, "oro",
                        Math.Abs(r.delta).ToString(CultureInfo.InvariantCulture), Csv(r.reason), r.wave, run.Level, run.Plan, Fs(run.F), "", "", "", "", "", ""));
                }
            }
            int towers = run.R.Report.towersAtEnd.Sum(t => t.level1 + t.level2);
            csv.AppendLine(string.Join(",", run.R.Duration.ToString("0.000", CultureInfo.InvariantCulture), player, session, "partida_terminada", common, "", "", "", "",
                run.R.Wave, run.Level, run.Plan, Fs(run.F), run.R.Outcome, run.R.Stars, run.R.Hp, towers, run.R.Unspent, run.R.Duration.ToString("0.0", CultureInfo.InvariantCulture)));
        }

        /// <summary>Cristales del Mundo 1 con 3★ al primer intento, contra la tienda disponible al terminarlo (Progression del núcleo).</summary>
        static void Meta(Dictionary<string, float> durations)
        {
            var camp = World1.Campaign();
            var p = new Progression(camp, new SaveData());
            Console.WriteLine($"\nECONOMÍA META — {p.CurrencyName} del Mundo 1 (3★ al primer intento, sin compras){(camp.bestResultOnly ? " · regla de mejor resultado" : "")}");
            int earned = 0;
            foreach (var id in World1.Levels)
            {
                int r = p.RewardFor(id, 3);
                p.ApplyVictory(id, 3, 100);
                earned += r;
                Console.WriteLine($"  {id}: +{r,4}  acumulado {p.Currency,4}");
            }
            var avail = camp.shop.Where(s => p.CannotBuyReason(s.id) != "torre bloqueada" && p.CannotBuyReason(s.id) != "no existe").ToList();
            int cost = avail.Sum(s => s.cost);
            Console.WriteLine($"  tienda disponible al terminar el Mundo 1: {avail.Count} mejoras, {cost} cristales · cobertura {100f * earned / Math.Max(1, cost):0}%");
            int missing = Math.Max(0, cost - earned);
            var best = World1.Levels.Select(id => (id, reward: p.RewardFor(id, 3), minutes: durations.TryGetValue(id, out var d) ? d / 60f : float.NaN))
                                    .Where(x => x.reward > 0 && !float.IsNaN(x.minutes))
                                    .OrderByDescending(x => x.reward / x.minutes).FirstOrDefault();
            if (camp.bestResultOnly)
            {
                var p1 = new Progression(camp, new SaveData());
                foreach (var id in World1.Levels) p1.ApplyVictory(id, 1, 1);
                Console.WriteLine($"  máximo posible del Mundo 1: {earned} (todo a 3★) · con 1★ en todo: {p1.Currency} ({100f * p1.Currency / Math.Max(1, cost):0}% de la tienda)");
                Console.WriteLine(missing == 0 ? $"  todo a 3★ paga la tienda del Mundo 1 y sobran {earned - cost}" : $"  todo a 3★ deja {missing} sin cubrir: con la regla de mejor resultado no se puede repetir para juntar más");
                return;
            }
            if (missing == 0) Console.WriteLine("  la tienda del Mundo 1 se paga con el primer recorrido");
            else if (best.reward > 0)
            {
                int n = (int)Math.Ceiling(missing / (float)best.reward);
                Console.WriteLine($"  faltan {missing}: repetir {best.id} a 3★ paga {best.reward} ({best.reward / best.minutes:0.0} por minuto del bot) → {n} repeticiones, {n * best.minutes:0} minutos de juego activo");
            }
        }
    }
}
