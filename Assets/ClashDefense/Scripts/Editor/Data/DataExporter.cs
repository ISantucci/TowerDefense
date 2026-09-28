using System;
using System.Collections.Generic;
using System.IO;
using ClashDefense.Core;
using ClashDefense.Game;
using UnityEditor;
using UnityEngine;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Exporta los datos del juego (los ScriptableObjects y la forma horneada de los niveles) a JSON para el simulador
    /// (Tools/ClashDefenseSim) y las pruebas sin Unity. Los JSON son un PRODUCTO, no una fuente: se regeneran solos al guardar
    /// una escena de nivel y desde el menú. Editarlos a mano no cambia el juego.
    /// </summary>
    public static class DataExporter
    {
        public const string OutDir = "Tools/ClashDefenseSim/Datos";
        const string Header = "GENERADO desde Unity (Clash Defense/Datos/Exportar). No editar a mano: la fuente son los assets de Assets/ClashDefense/Data y las escenas de nivel.";

        [MenuItem("Clash Defense/Datos/Exportar datos para el simulador", priority = 40)]
        static void ExportMenu() => Debug.Log("[ClashDefense] " + string.Join("\n", ExportAll(true)));

        /// <summary>Busca el GameConfig de la campaña y exporta balance, campaña y niveles con los nombres que usa el simulador.</summary>
        public static List<string> ExportAll(bool verbose)
        {
            var log = new List<string>();
            var cfg = FindConfig(true);
            if (cfg == null) { log.Add("no hay GameConfig con campaña: nada que exportar"); return log; }
            Directory.CreateDirectory(OutDir);
            foreach (var kv in Files(cfg)) { File.WriteAllText(Path.Combine(OutDir, kv.Key), kv.Value); log.Add($"{OutDir}/{kv.Key}"); }
            File.WriteAllText(Path.Combine(OutDir, "LEEME.txt"), Header + "\n");
            if (verbose) Debug.Log("[ClashDefense] Datos exportados para el simulador:\n" + string.Join("\n", log));
            return log;
        }

        /// <summary>Nombre de archivo → JSON, con las convenciones del simulador (balance_p0, balance_w1, campaign_w1, level_&lt;id&gt;, level_p0).</summary>
        public static Dictionary<string, string> Files(GameConfig cfg)
        {
            var d = new Dictionary<string, string>();
            if (cfg.labBalance != null) d["balance_p0.json"] = JsonUtility.ToJson(cfg.labBalance.ToCore(), true);
            if (cfg.labLevel != null) d["level_p0.json"] = JsonUtility.ToJson(cfg.labLevel.ToCore(), true);
            var c = cfg.campaign;
            if (c != null)
            {
                if (c.balance != null) d["balance_w1.json"] = JsonUtility.ToJson(c.balance.ToCore(), true);
                var camp = c.ToCore();
                foreach (var w in camp.worlds) foreach (var ct in w.continents) foreach (var l in ct.levels) if (!string.IsNullOrEmpty(l.file)) l.file = "level_" + l.file;
                d["campaign_w1.json"] = JsonUtility.ToJson(camp, true);
                foreach (var l in c.Levels()) d[$"level_{l.id}.json"] = JsonUtility.ToJson(l.ToCore(), true);
            }
            return d;
        }

        /// <summary>
        /// Compara archivo por archivo lo exportado contra otros JSON (los originales de la migración, o los del simulador),
        /// por contenido y no por texto: los dos se leen con las clases del núcleo y se vuelven a escribir igual, así un campo
        /// que falta y uno en su valor por defecto cuentan como iguales. El recorrido único del P0 (path) cuenta como un routes.
        /// </summary>
        public static List<string> Compare(Dictionary<string, string> reference, Dictionary<string, string> exported)
        {
            var r = new List<string>();
            foreach (var kv in reference)
            {
                if (!exported.TryGetValue(kv.Key, out var mine)) { r.Add($"{kv.Key}: FALTA en la exportación"); continue; }
                string a = Canonical(kv.Key, kv.Value), b = Canonical(kv.Key, mine);
                if (a == b) { r.Add($"{kv.Key}: igual"); continue; }
                int i = 0; while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
                int from = Math.Max(0, i - 60);
                r.Add($"{kv.Key}: DISTINTO desde el carácter {i}\n      antes: …{Cut(a, from, 140)}…\n      ahora: …{Cut(b, from, 140)}…");
            }
            foreach (var k in exported.Keys) if (!reference.ContainsKey(k)) r.Add($"{k}: nuevo (no estaba antes)");
            return r;
        }

        static string Cut(string s, int from, int len) => from >= s.Length ? "" : s.Substring(from, Math.Min(len, s.Length - from)).Replace("\n", " ");

        static string Canonical(string file, string json)
        {
            if (file.StartsWith("balance_")) return JsonUtility.ToJson(JsonUtility.FromJson<BalanceData>(json));
            if (file.StartsWith("campaign_")) return JsonUtility.ToJson(JsonUtility.FromJson<CampaignData>(json));
            if (file.StartsWith("level_"))
            {
                var l = JsonUtility.FromJson<LevelData>(json);
                if ((l.routes == null || l.routes.Length == 0) && l.path != null && l.path.Length >= 2) l.routes = new[] { new RouteData { points = l.path } };
                l.path = null;
                return JsonUtility.ToJson(l);
            }
            return json;
        }

        public static GameConfig FindConfig(bool campaign)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameConfig"))
            {
                var c = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && c.IsCampaign == campaign) return c;
            }
            return null;
        }
    }
}
