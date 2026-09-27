using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Registro local de la prueba (RQ-001.7, MET-001.7): un partida_&lt;fecha&gt;.json + .txt por partida y un eventos.csv
    /// acumulado con los ocho eventos del tracking plan. Nada sale de la máquina.
    /// </summary>
    public sealed class MetricsWriter
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        readonly string dir;
        readonly string playerId;
        readonly string sessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
        readonly string environment;
        MatchRecorder recorder;
        Match match;
        string startedAt;
        float realPause;
        public string Folder => dir;
        public string LastReportPath { get; private set; }
        public MatchReport LastReport { get; private set; }

        public MetricsWriter()
        {
            dir = Path.Combine(Application.persistentDataPath, "metricas");
            playerId = PlayerPrefs.GetString("cd_player_id", "");
            if (string.IsNullOrEmpty(playerId)) { playerId = Guid.NewGuid().ToString("N").Substring(0, 12); PlayerPrefs.SetString("cd_player_id", playerId); PlayerPrefs.Save(); }
            environment = Application.isEditor ? "editor" : Application.platform == RuntimePlatform.WebGLPlayer ? "webgl" : "windows";
        }

        public void Begin(Match m)
        {
            match = m;
            recorder = new MatchRecorder();
            realPause = 0f;
            startedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", Ci);
            Row("partida_iniciada", 0f, ("balance_version", m.Balance.version), ("level_id", m.Level.id), ("tutorial", m.TutorialEnabled ? "1" : "0"));
        }

        public void AddPause(float seconds) => realPause += seconds;

        public void OnEvent(SimEvent e)
        {
            if (recorder == null) return;
            recorder.Consume(e);
            switch (e.Type)
            {
                case SimEventType.TowerBuilt: Row("torre_construida", e.Time, ("tower_type", e.Text), ("x", F(e.Pos.x)), ("z", F(e.Pos.z)), ("cost", e.Int1.ToString(Ci))); break;
                case SimEventType.TowerUpgraded: Row("torre_mejorada", e.Time, ("tower_type", e.Text), ("cost", e.Int1.ToString(Ci))); break;
                case SimEventType.TowerSold: Row("torre_vendida", e.Time, ("tower_type", e.Text), ("level", e.Int2.ToString(Ci)), ("refund", e.Int1.ToString(Ci))); break;
                case SimEventType.EnemyReachedBase: Row("enemigo_filtrado", e.Time, ("enemy_type", e.Text), ("wave", match.WaveNumber.ToString(Ci)), ("damage", e.Int1.ToString(Ci))); break;
                case SimEventType.AttackImmune: Row("ataque_inmune", e.Time, ("tower_type", e.Text), ("discovery", e.Int1.ToString(Ci))); break;
                case SimEventType.WaveCleared: Row("oleada_terminada", e.Time, ("wave", e.Int1.ToString(Ci)), ("leaked", LeakedIn(e.Int1).ToString(Ci))); break;
            }
        }

        int LeakedIn(int wave)
        {
            var rep = recorder.Build(match);
            foreach (var w in rep.waves) if (w.n == wave) return w.leaked;
            return 0;
        }

        /// <summary>Cierra el registro de la partida. Devuelve el resumen legible.</summary>
        public string End()
        {
            if (recorder == null || match == null) return null;
            var rep = recorder.Build(match);
            rep.buildVersion = Application.version;
            rep.platform = environment;
            rep.startedAtUtc = startedAt;
            rep.realPauseSeconds = realPause;
            float dA = 0, dC = 0, dM = 0;
            foreach (var d in rep.damageByTowerType)
            {
                float v = d.damage + d.armorDamage;
                if (d.type == "arqueras") dA = v; else if (d.type == "canon") dC = v; else if (d.type == "mago") dM = v;
            }
            Row("partida_terminada", rep.durationSeconds, ("result", rep.result), ("seconds", F(rep.durationSeconds)), ("wave", rep.waveReached.ToString(Ci)),
                ("hp", rep.baseHpLeft.ToString(Ci)), ("stars", rep.stars.ToString(Ci)), ("gold_unspent", rep.gold.unspent.ToString(Ci)),
                ("damage_arqueras", F(dA)), ("damage_canon", F(dC)), ("damage_mago", F(dM)));
            string summary = MatchRecorder.Summary(rep);
            try
            {
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Ci);
                LastReportPath = Path.Combine(dir, $"partida_{stamp}.json");
                File.WriteAllText(LastReportPath, JsonUtility.ToJson(rep, true), Encoding.UTF8);
                File.WriteAllText(Path.Combine(dir, $"partida_{stamp}.txt"), summary, Encoding.UTF8);
            }
            catch (Exception ex) { Debug.LogWarning("[ClashDefense] No se pudo escribir el registro: " + ex.Message); }
            Debug.Log("[ClashDefense] Partida registrada\n" + summary);
            LastReport = rep;
            recorder = null; match = null;
            return summary;
        }

        static string F(float v) => v.ToString("0.###", Ci);

        void Row(string ev, float seconds, params (string k, string v)[] prms)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "eventos.csv");
                bool header = !File.Exists(path);
                var sb = new StringBuilder();
                if (header) sb.Append("timestamp,player_id,session_id,version,environment,event,seconds,params\n");
                sb.Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", Ci)).Append(',')
                  .Append(playerId).Append(',').Append(sessionId).Append(',')
                  .Append(Application.version).Append(',').Append(environment).Append(',')
                  .Append(ev).Append(',').Append(F(seconds)).Append(',');
                for (int i = 0; i < prms.Length; i++) { if (i > 0) sb.Append(';'); sb.Append(prms[i].k).Append('=').Append(prms[i].v); }
                sb.Append('\n');
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) { Debug.LogWarning("[ClashDefense] eventos.csv: " + ex.Message); }
        }
    }
}
