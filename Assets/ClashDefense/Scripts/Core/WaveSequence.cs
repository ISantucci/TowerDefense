using System;
using System.Collections.Generic;
using System.Text;

namespace ClashDefense.Core
{
    /// <summary>
    /// Expande la secuencia de una oleada tal cual la escribe el owner (Doc 05 §8, GDS-001.5):
    ///   "D D E / (D E V A E V)x4 / A D D E A"
    /// '/' solo agrupa (no agrega tiempo). "(…)xN" o "(…)×N" repite el grupo. "D x10" repite el código anterior.
    /// "." es un lugar vacío: consume un intervalo sin que aparezca nadie (LDS-002.6, respiro dentro de la oleada).
    /// </summary>
    public static class WaveSequence
    {
        public const string Gap = ".";

        public static List<string> Expand(string sequence)
        {
            if (sequence == null) throw new FormatException("secuencia vacía");
            var tokens = Tokenize(sequence);
            int i = 0;
            var result = ParseList(tokens, ref i, false);
            if (i != tokens.Count) throw new FormatException($"sobra '{tokens[i]}' en «{sequence}»");
            return result;
        }

        static List<string> Tokenize(string s)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            void Flush() { if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; } }
            for (int k = 0; k < s.Length; k++)
            {
                char c = s[k];
                if (char.IsWhiteSpace(c) || c == '/') { Flush(); continue; }
                if (c == '(' || c == ')' || c == '.') { Flush(); list.Add(c.ToString()); continue; }
                if ((c == 'x' || c == 'X' || c == '×') && k + 1 < s.Length && char.IsDigit(s[k + 1]) && sb.Length == 0)
                {
                    // multiplicador: x10, ×4
                    int j = k + 1; var num = new StringBuilder();
                    while (j < s.Length && char.IsDigit(s[j])) { num.Append(s[j]); j++; }
                    list.Add("*" + num);
                    k = j - 1;
                    continue;
                }
                sb.Append(c);
            }
            Flush();
            return list;
        }

        static List<string> ParseList(List<string> t, ref int i, bool inGroup)
        {
            var outList = new List<string>();
            List<string> last = null;
            while (i < t.Count)
            {
                string tok = t[i];
                if (tok == ")")
                {
                    if (!inGroup) throw new FormatException("')' sin '('");
                    return outList;
                }
                if (tok == "(")
                {
                    i++;
                    var group = ParseList(t, ref i, true);
                    if (i >= t.Count || t[i] != ")") throw new FormatException("falta ')'");
                    i++;
                    last = group;
                    outList.AddRange(group);
                    continue;
                }
                if (tok.StartsWith("*"))
                {
                    if (last == null) throw new FormatException("multiplicador sin nada que repetir");
                    int n = int.Parse(tok.Substring(1));
                    if (n < 1) throw new FormatException("multiplicador < 1");
                    for (int r = 1; r < n; r++) outList.AddRange(last);
                    last = null;
                    i++;
                    continue;
                }
                last = new List<string> { tok };
                outList.Add(tok);
                i++;
            }
            if (inGroup) throw new FormatException("falta ')'");
            return outList;
        }
    }

    /// <summary>Una aparición programada: segundo de la oleada, código del enemigo y recorrido.</summary>
    public struct SpawnEntry
    {
        public double Time;
        public string Code;
        public int Route;
        public bool Miniboss;
    }

    /// <summary>
    /// Convierte una oleada en su programa exacto de apariciones. Dos formas:
    ///  - secuencia (P0, LDS-002.6): el lugar i aparece en i × intervalo; "." no aparece; lanes reparte en ciclo.
    ///  - composición (Doc 05 v2.0 §9.2, GDS-004.2): pasadas por la prioridad de waveRules.order, una unidad de cada tipo con
    ///    cantidad pendiente; entradas alternadas (1.ª, 3.ª… a la A; 2.ª, 4.ª… a la B) y, dentro de cada entrada, sus ramales
    ///    en ciclo; el miniboss después de ceil(minibossAfter × normales), por la entrada A, con minibossGap antes y después.
    /// Sin azar: la misma oleada da siempre el mismo programa.
    /// </summary>
    public static class WaveSchedule
    {
        /// <summary>"D10 E6 V0" → [(D,10), (E,6)] en el orden escrito. Lanza FormatException si un término no es código+número.</summary>
        public static List<KeyValuePair<string, int>> ParseComposition(string composition)
        {
            var list = new List<KeyValuePair<string, int>>();
            if (string.IsNullOrEmpty(composition)) return list;
            foreach (var raw in composition.Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int k = 0;
                while (k < raw.Length && !char.IsDigit(raw[k])) k++;
                string code = raw.Substring(0, k).TrimEnd(':');
                if (code.Length == 0 || k == raw.Length || !int.TryParse(raw.Substring(k), out int n) || n < 0)
                    throw new FormatException($"'{raw}' no es código + cantidad (ej. D10)");
                list.Add(new KeyValuePair<string, int>(code, n));
            }
            return list;
        }

        /// <summary>Entradas del nivel: recorridos agrupados por su punto de partida, en el orden de los recorridos.</summary>
        public static List<List<int>> Entrances(IList<Vec2> routeStarts)
        {
            var groups = new List<List<int>>();
            var starts = new List<Vec2>();
            for (int r = 0; r < routeStarts.Count; r++)
            {
                int g = -1;
                for (int i = 0; i < starts.Count; i++) if (Vec2.Distance(starts[i], routeStarts[r]) < 0.01f) { g = i; break; }
                if (g < 0) { starts.Add(routeStarts[r]); groups.Add(new List<int>()); g = groups.Count - 1; }
                groups[g].Add(r);
            }
            return groups;
        }

        public static List<SpawnEntry> Build(WaveData wd, WaveRulesData rules, IList<Vec2> routeStarts)
        {
            var result = new List<SpawnEntry>();
            if (!wd.IsComposition)
            {
                var seq = WaveSequence.Expand(wd.sequence);
                var lanes = new List<int>();
                if (!string.IsNullOrEmpty(wd.lanes)) foreach (var l in WaveSequence.Expand(wd.lanes)) lanes.Add(int.Parse(l));
                int real = 0;
                for (int i = 0; i < seq.Count; i++)
                {
                    if (seq[i] == WaveSequence.Gap) continue;
                    result.Add(new SpawnEntry { Time = i * (double)wd.spawnInterval, Code = seq[i], Route = lanes.Count > 0 ? lanes[real % lanes.Count] : 0 });
                    real++;
                }
                return result;
            }

            if (rules == null) throw new FormatException("la oleada usa composition y el balance no trae waveRules");
            // 1. orden: pasadas por la prioridad, una de cada tipo pendiente
            var pending = new Dictionary<string, int>();
            foreach (var kv in ParseComposition(wd.composition)) pending[kv.Key] = (pending.TryGetValue(kv.Key, out var c) ? c : 0) + kv.Value;
            var order = new List<string>(WaveSequence.Expand(rules.order ?? ""));
            foreach (var code in pending.Keys) if (!order.Contains(code)) throw new FormatException($"'{code}' no está en waveRules.order");
            var normal = new List<string>();
            for (bool any = true; any;)
            {
                any = false;
                foreach (var code in order)
                    if (pending.TryGetValue(code, out int left) && left > 0) { normal.Add(code); pending[code] = left - 1; any = true; }
            }
            // 2. entradas y ramales
            var entrances = Entrances(routeStarts != null && routeStarts.Count > 0 ? routeStarts : new List<Vec2> { new Vec2(0f, 0f) });
            var branchNext = new int[entrances.Count];
            int RouteFor(int entrance)
            {
                var g = entrances[entrance];
                int r = g[branchNext[entrance] % g.Count];
                branchNext[entrance]++;
                return r;
            }
            // 3. tiempos, con el miniboss insertado
            bool boss = !string.IsNullOrEmpty(wd.miniboss);
            int k = boss ? (int)Math.Ceiling(rules.minibossAfter * normal.Count - 1e-6) : normal.Count;
            if (k > normal.Count) k = normal.Count;
            double t = 0;
            for (int i = 0; i < normal.Count; i++)
            {
                if (boss && i == k)
                {
                    t = (k > 0 ? result[result.Count - 1].Time + rules.minibossGap : 0);
                    result.Add(new SpawnEntry { Time = t, Code = wd.miniboss, Route = entrances[0][0], Miniboss = true });
                    t += rules.minibossGap;
                }
                else if (i > 0) t += wd.spawnInterval;
                result.Add(new SpawnEntry { Time = t, Code = normal[i], Route = RouteFor(i % entrances.Count) });
            }
            if (boss && k == normal.Count)
                result.Add(new SpawnEntry { Time = normal.Count > 0 ? t + rules.minibossGap : 0, Code = wd.miniboss, Route = entrances[0][0], Miniboss = true });
            return result;
        }
    }
}
