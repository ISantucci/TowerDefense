using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClashDefense.Core;
using ClashDefense.Game;
using UnityEngine;

namespace ClashDefense.QA
{
    /// <summary>
    /// Piloto de QA para QA-001 (solo en el editor). Juega la escena Prototipo0 en Play Mode recorriendo inicio → tutorial →
    /// cinco oleadas con un plan combinado → panel de torre → pausa → resultado, y una segunda partida que pierde.
    /// Saca capturas y escribe un informe con las verificaciones en Logs/ClashDefenseQA/. No reemplaza al owner jugando.
    /// </summary>
    public sealed class QaAutopilot : MonoBehaviour
    {
        public float speed = 3f;
        string dir;
        readonly StringBuilder log = new StringBuilder();
        readonly List<string> checks = new List<string>();
        int errors, exceptions;
        readonly HashSet<string> seen = new HashSet<string>();   // cada mensaje se anota una vez; el total va en el conteo
        GameBootstrap boot;

        public static QaAutopilot Launch(float speed = 3f)
        {
            var go = new GameObject("QaAutopilot");
            var qa = go.AddComponent<QaAutopilot>();
            qa.speed = speed;
            return qa;
        }

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception) { exceptions++; if (seen.Add(msg)) Line("EXCEPCION " + msg + " | " + string.Join(" <- ", stack.Split('\n').Take(4))); }
            else if (type == LogType.Error) { errors++; if (seen.Add(msg)) Line("ERROR " + msg); }
        }

        void Start()
        {
            dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ClashDefenseQA");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            StartCoroutine(Run());
        }

        void Line(string s) { log.Append($"[{Time.realtimeSinceStartup:0.0}] ").Append(s).Append('\n'); }
        void Check(bool ok, string what) { checks.Add((ok ? "OK    " : "FALLA ") + what); Line((ok ? "OK " : "FALLA ") + what); }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Line("captura " + name);
            yield return null;
        }

        IEnumerator WaitUntilOr(Func<bool> cond, float realSeconds)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        static Vector3 W(float x, float z) => new Vector3(x, 0f, z);

        IEnumerator Run()
        {
            yield return null;
            boot = FindAnyObjectByType<GameBootstrap>();
            Check(boot != null, "la escena tiene GameBootstrap");
            if (boot == null) { Finish(); yield break; }
            boot.QaTakeControl();
            // el registro del piloto va a su carpeta: no se mezcla con las partidas del owner (TL-002)
            if (boot.QaMetrics != null) boot.QaMetrics.SetFolder(Path.Combine(dir, "metricas"));
            yield return new WaitForSecondsRealtime(0.5f);
            Check(boot.QaHud.StartVisible, "arranca en la pantalla de inicio");
            yield return Shot("01_inicio");

            // ---------------- partida 1: tutorial + plan combinado
            boot.QaStart(true);
            var m = boot.CurrentMatch;
            yield return WaitUntilOr(() => m.State == MatchState.Countdown, 2f);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Shot("02_cuenta_regresiva");
            boot.QaSelectType("arqueras");
            Check(m.State == MatchState.Countdown && boot.QaSelectedType == null && m.Towers.Count == 0, "con tutorial, en la cuenta no se elige torre (GDS-001.6)");
            yield return WaitUntilOr(() => m.State == MatchState.Tutorial, 6f);
            Check(m.State == MatchState.Tutorial && m.Tutorial == TutorialStep.Base, "tras DEFENSE entra el tutorial en el paso 1");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("03_tutorial_base");
            float frozen = m.ActiveTime;
            yield return new WaitForSecondsRealtime(1f);
            Check(Mathf.Approximately(frozen, m.ActiveTime) && m.Enemies.Count == 0, "el tutorial congela el tiempo y no hay enemigos");
            boot.QaTutorialNext(); boot.QaTutorialNext(); boot.QaTutorialNext();
            Check(m.Tutorial == TutorialStep.SelectTower, "paso 4: elegir la torre");
            boot.QaSelectType("canon");
            Check(boot.QaSelectedType == null, "en el tutorial no se puede elegir el Cañón");
            boot.QaSelectType("arqueras");
            Check(m.Tutorial == TutorialStep.PlaceTower, "paso 5: colocar");
            boot.QaPointerAt(W(13f, 3.5f));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("04_fantasma_invalido");
            boot.QaPointerAt(W(4.5f, 3.5f));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("05_fantasma_valido_pista");
            Check(boot.QaClick(W(4.5f, 3.5f)), "construye la Arqueras en la pista");
            boot.QaPointerAt(null);
            Check(m.Gold == 0 && m.Tutorial == TutorialStep.Income, "queda en 0 de oro y pasa al paso 6");
            boot.QaTutorialNext();
            yield return Shot("06_tutorial_objetivo");
            boot.QaTutorialNext();
            Check(m.State == MatchState.Wave && m.WaveNumber == 1, "arranca la oleada 1 al cerrar el tutorial");

            Time.timeScale = speed;
            var plan = new (string kind, string type, Vector3 at, int index)[]
            {
                ("B", "arqueras", W(-6f, -3.5f), 0), ("B", "canon", W(8.5f, 3f), 0), ("B", "mago", W(11f, 3.5f), 0),
                ("U", "arqueras", default, 0), ("B", "canon", W(-8.5f, -4f), 0), ("U", "arqueras", default, 1),
                ("B", "mago", W(-11f, -3.5f), 0), ("U", "canon", default, 0), ("B", "arqueras", W(0f, -3.5f), 0),
                ("U", "mago", default, 0), ("U", "canon", default, 1), ("B", "canon", W(4f, -3.5f), 0), ("U", "mago", default, 1),
            };
            int next = 0;
            bool shotW3 = false, shotImm = false, shotBroken = false, shotPanel = false, paused = false;
            var built = new Dictionary<string, List<int>>();
            int lastTowerCount = 1;
            built["arqueras"] = new List<int> { m.Towers[0].Id };
            float guard = Time.realtimeSinceStartup + 600f;
            while (!m.Ended && Time.realtimeSinceStartup < guard)
            {
                if (next < plan.Length && (m.State == MatchState.Wave || m.State == MatchState.Interval))
                {
                    var o = plan[next];
                    if (o.kind == "B" && m.Gold >= m.GetTowerType(o.type).cost)
                    {
                        boot.QaSelectType(o.type);
                        boot.QaClick(o.at);
                        if (m.Towers.Count > lastTowerCount)
                        {
                            lastTowerCount = m.Towers.Count;
                            if (!built.ContainsKey(o.type)) built[o.type] = new List<int>();
                            built[o.type].Add(m.Towers[m.Towers.Count - 1].Id);
                            next++;
                        }
                        else { Line($"no pudo construir {o.type} en {o.at}"); next++; }
                    }
                    else if (o.kind == "U" && built.TryGetValue(o.type, out var ids) && o.index < ids.Count)
                    {
                        var t = m.GetTower(ids[o.index]);
                        if (t == null || !t.CanUpgrade) next++;
                        else if (m.Gold >= m.UpgradeCost(t))
                        {
                            boot.QaSelectTower(t.Id);
                            if (!shotPanel) { boot.QaUpgradeHover(true); Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.2f); yield return Shot("09_panel_torre_preview_N2"); boot.QaUpgradeHover(false); Time.timeScale = speed; shotPanel = true; }
                            boot.QaUpgrade();
                            Check(t.Level == 2, $"mejora {o.type} a N2");
                            boot.QaClick(Vector3.zero);
                            next++;
                        }
                    }
                    else if (o.kind == "U") next++;
                }
                if (!shotW3 && m.WaveNumber == 3 && m.Enemies.Any(e => e.Layer == Layer.Air))
                {
                    shotW3 = true; Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.2f); yield return Shot("07_oleada3_aereos"); Time.timeScale = speed;
                }
                if (!shotImm && m.WaveNumber >= 4 && m.Enemies.Any(e => e.Armored && e.Distance > 20f))
                {
                    shotImm = true; Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.2f); yield return Shot("08_oleada4_blindados"); Time.timeScale = speed;
                }
                if (!shotBroken && m.Enemies.Any(e => e.Type.armor > 0f && !e.Armored && e.Hp > 0f))
                {
                    shotBroken = true; Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.2f); yield return Shot("08b_blindado_sin_armadura"); Time.timeScale = speed;
                }
                if (!paused && m.WaveNumber == 4 && m.State == MatchState.Wave)
                {
                    paused = true;
                    boot.QaPause();
                    Check(m.State == MatchState.Paused && Time.timeScale == 0f, "la pausa congela (estado y timeScale)");
                    float t0 = m.ActiveTime;
                    yield return new WaitForSecondsRealtime(0.6f);
                    Check(Mathf.Approximately(t0, m.ActiveTime), "en pausa no avanza el tiempo de partida");
                    boot.QaSelectType("arqueras");
                    Check(boot.QaSelectedType == null, "en pausa no se puede elegir torre");
                    yield return Shot("10_pausa");
                    boot.QaResume();
                    Time.timeScale = speed;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            Check(m.Ended, "la partida 1 termina");
            Line($"partida 1: {m.Result}, vida {m.BaseHp}, estrellas {m.Stars}, duración {m.ActiveTime:0.0} s, oro {m.Gold}");
            yield return WaitUntilOr(() => boot.QaHud.ResultVisible, 3f);
            Check(boot.QaHud.ResultVisible, "aparece la pantalla de resultado");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("11_resultado_partida1");
            var rep1 = boot.QaMetrics.LastReport;
            Check(rep1 != null && rep1.checks.All(c => c.StartsWith("OK")), "el registro de la partida 1 cierra sus cuentas");
            Check(rep1 != null && File.Exists(boot.QaMetrics.LastReportPath), "existe el partida_*.json de la partida 1");
            Line("registro: " + boot.QaMetrics.LastReportPath);

            // ---------------- partida 2: reintento sin tutorial, una sola Arqueras -> derrota
            boot.QaRestart();
            m = boot.CurrentMatch;
            Check(m != null && !m.TutorialEnabled && m.Gold == 100 && m.BaseHp == 100 && m.Towers.Count == 0, "reintentar: vida 100, oro 100, sin torres, sin tutorial");
            boot.QaSelectType("arqueras");
            boot.QaClick(W(-8.5f, 9.5f));
            Check(m.Towers.Count == 1 && m.Gold == 0, "construye una Arqueras lejos del camino");
            // venta: el primer pedido solo arma la confirmación; el segundo vende (GDS-001.3, UXS-001.3)
            var lone = m.Towers[0];
            int refund = m.SellRefund(lone);
            boot.QaSelectTower(lone.Id);
            boot.QaSell();
            Check(m.Towers.Count == 1 && m.Gold == 0, "vender pide confirmación: el primer pedido no vende");
            Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.2f); yield return Shot("12a_venta_confirmacion"); Time.timeScale = 1f;
            boot.QaSell();
            Check(m.Towers.Count == 0 && m.Gold == refund, $"el segundo pedido vende y reembolsa {refund}");
            boot.QaSelectType("arqueras");
            boot.QaClick(W(-8.5f, 9.5f));
            Check(m.Towers.Count == 0, "sin oro suficiente no se construye (negativo)");
            boot.QaCancel();
            Time.timeScale = speed * 2f;
            yield return WaitUntilOr(() => m.Ended, 300f);
            Time.timeScale = 1f;
            Check(m.State == MatchState.Defeat, "sin torres, derrota");
            yield return WaitUntilOr(() => boot.QaHud.ResultVisible, 3f);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("12_resultado_derrota");

            // ---------------- abandono desde la pausa
            boot.QaRestart();
            m = boot.CurrentMatch;
            yield return new WaitForSecondsRealtime(1f);
            boot.QaPause();
            boot.QaExitToStart();
            Check(boot.QaHud.StartVisible && boot.QaMetrics.LastReport.result == "abandono", "salir desde la pausa registra abandono y vuelve al inicio");
            yield return Shot("13_vuelta_al_inicio");
            Finish();
        }

        void Finish()
        {
            Check(exceptions == 0, $"sin excepciones en consola ({exceptions})");
            Check(errors == 0, $"sin errores en consola ({errors})");
            var csv = Path.Combine(boot != null && boot.QaMetrics != null ? boot.QaMetrics.Folder : Path.Combine(Application.persistentDataPath, "metricas"), "eventos.csv");
            if (File.Exists(csv)) Line($"eventos.csv: {File.ReadAllLines(csv).Length} líneas en {csv}");
            var sb = new StringBuilder();
            sb.Append("QA-001 piloto automático · ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
            sb.Append($"verificaciones: {checks.Count(c => c.StartsWith("OK"))} OK · {checks.Count(c => c.StartsWith("FALLA"))} FALLA\n\n");
            foreach (var c in checks) sb.Append(c).Append('\n');
            sb.Append("\n--- bitácora ---\n").Append(log);
            File.WriteAllText(Path.Combine(dir, "qa_informe.txt"), sb.ToString());
            File.WriteAllText(Path.Combine(dir, "qa_listo.txt"), "listo");
            Time.timeScale = 1f;
        }
    }
}
