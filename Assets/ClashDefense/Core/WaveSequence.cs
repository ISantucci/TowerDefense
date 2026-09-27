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
}
