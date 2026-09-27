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
    /// Piloto de QA de TL-002 (solo en el editor). Juega la escena del juego en Play Mode con un guardado propio:
    /// aviso → inicio → tienda → opciones → mapa → Mundo 1 niveles 1 a 6 con un plan combinado → mejora de tanda →
    /// Mundo 2 abierto → compra en la tienda → pausa y salida al mapa. Saca capturas y escribe un informe con las
    /// verificaciones en Logs/ClashDefenseQA_W1/. No toca el progreso ni el registro del owner. No reemplaza al owner jugando.
    /// </summary>
    public sealed class CampaignQa : MonoBehaviour
    {
        public float speed = 12f;
        public string only = "";          // "m1_n3" = solo ese nivel (con el guardado armado hasta ahí)
        string dir;
        readonly StringBuilder log = new StringBuilder();
        readonly List<string> checks = new List<string>();
        int errors, exceptions;
        readonly HashSet<string> seen = new HashSet<string>();
        GameBootstrap boot;

        public static CampaignQa Launch(float speed = 12f, string only = "")
        {
            var go = new GameObject("CampaignQa");
            var qa = go.AddComponent<CampaignQa>();
            qa.speed = speed;
            qa.only = only ?? "";
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
            dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ClashDefenseQA_W1");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            StartCoroutine(Run());
        }

        void Line(string s) { log.Append($"[{Time.realtimeSinceStartup:0.0}] ").Append(s).Append('\n'); }
        void Check(bool ok, string what) { checks.Add((ok ? "OK    " : "FALLA ") + what); Line((ok ? "OK " : "FALLA ") + what); }

        IEnumerator Shot(string name)
        {
            float ts = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Line("captura " + name);
            yield return null;
            Time.timeScale = ts;
        }

        IEnumerator WaitUntilOr(Func<bool> cond, float realSeconds)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        // ------------------------------------------------------------------ recorrido
        IEnumerator Run()
        {
            yield return null;
            boot = FindAnyObjectByType<GameBootstrap>();
            Check(boot != null && boot.QaCampaign, "la escena es la del juego (GameBootstrap con campaña)");
            if (boot == null || !boot.QaCampaign) { Finish(); yield break; }
            boot.QaTakeControl();
            boot.QaMetrics.SetFolder(Path.Combine(dir, "metricas"));
            boot.QaUseSaveKey("cd_guardado_qa", true);
            yield return new WaitForSecondsRealtime(0.4f);
            var menus = boot.QaMenus;
            var prog = boot.QaProgression;
            Check(menus.Current == MenuScreen.Notice, "primera vez: aviso de datos antes del inicio (Doc 02 §13)");
            yield return Shot("01_aviso_datos");
            boot.QaAcceptNotice();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(menus.Current == MenuScreen.Title, "aceptado el aviso, pantalla inicial");
            yield return Shot("02_pantalla_inicial");
            boot.QaShowShop();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(menus.Current == MenuScreen.Shop, "la tienda abre desde el menú");
            yield return Shot("03_tienda_inicial");
            boot.QaShowOptions();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(menus.Current == MenuScreen.Options, "opciones");
            yield return Shot("04_opciones");
            boot.QaShowMap(null);
            yield return new WaitForSecondsRealtime(0.4f);
            prog = boot.QaProgression;
            Check(menus.Current == MenuScreen.Map && menus.SelectedLevel == "m1_n1" && prog.IsLevelUnlocked("m1_n1") && !prog.IsLevelUnlocked("m1_n2"), "mapa: solo el nivel 1 disponible al empezar");
            yield return Shot("05_mapa_inicial");

            string[] ids = { "m1_n1", "m1_n2", "m1_n3", "m1_n4", "m1_n5", "m1_n6" };
            string[] rewards = { "mortero", "bombardera", "electrica", "infernal", "oro", "lanzallamas" };
            for (int i = 0; i < ids.Length; i++)
            {
                if (only.Length > 0 && only != ids[i]) { prog.ApplyVictory(ids[i], 2, 60); continue; }
                yield return PlayLevel(ids[i], i, rewards[i]);
                prog = boot.QaProgression;
                if (i == 2 || i == 5)
                {
                    Check(prog.Save.tandaPending > 0 && boot.QaHud.TandaButtonVisible, $"tras el nivel {i + 1}: mejora de tanda disponible en el resultado (Doc 02 §8)");
                    boot.QaShowTanda();
                    yield return new WaitForSecondsRealtime(0.3f);
                    Check(menus.Current == MenuScreen.Tanda, "elección de la mejora de tanda");
                    yield return Shot($"{20 + i}_tanda_nivel{i + 1}");
                    string pick = i == 2 ? "canon" : "arqueras";
                    int before = prog.TandaTier(pick);
                    boot.QaChooseTanda(pick);
                    Check(prog.TandaTier(pick) == before + 1 && prog.Save.tandaPending == 0, $"la mejora de tanda queda en {pick}");
                }
                if (only.Length > 0) break;
            }

            if (only.Length == 0)
            {
                Check(prog.IsWorldUnlocked(prog.Campaign.worlds[1]) && !prog.IsLevelUnlocked("m2_n1"), "completado el nivel 6: Mundo 2 abierto, sus niveles fuera de esta entrega");
                boot.QaShowMap("m1_n6");
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot("40_mapa_mundo1_completo");
                menus.SetWorldTab(1);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot("41_mapa_mundo2");
                menus.SetWorldTab(0);
                int cur = prog.Currency;
                var item = prog.Campaign.shop.First(s => prog.CannotBuyReason(s.id) == "");
                boot.QaShowShop();
                boot.QaBuy(item.id);
                Check(prog.IsPurchased(item.id) && prog.Currency == cur - item.cost, $"compra en la tienda: {item.id} por {item.cost}");
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot("42_tienda_con_compra");
                // la compra llega a la partida
                boot.QaStartLevel("m1_n1", false);
                var m = boot.CurrentMatch;
                var shopItem = prog.FindShopItem(item.id);
                var baseTower = Array.Find(JsonUtility.FromJson<BalanceData>(File.ReadAllText(Path.Combine(Application.dataPath, "ClashDefense/Data/balance_w1.json"))).towers, t => t.id == shopItem.tower);
                var inMatch = m.GetTowerType(shopItem.tower);
                Check(inMatch != null && !SameLevels(baseTower, inMatch), "la mejora comprada cambia la torre en la partida");
                // guardado: releer y comparar
                var reloaded = SaveStore.Load();
                Check(reloaded.currency == prog.Currency && reloaded.purchased.Contains(item.id) && reloaded.levels.Count(r => r.stars > 0) == 6, "el guardado local tiene los seis niveles, la moneda y la compra");
                // pausa y salida al mapa
                yield return WaitUntilOr(() => m.State == MatchState.Wave, 10f);
                boot.QaPause();
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Shot("43_pausa_campana");
                boot.QaExitToStart();
                yield return new WaitForSecondsRealtime(0.3f);
                Check(menus.Current == MenuScreen.Map && boot.QaMetrics.LastReport.result == "abandono", "salir desde la pausa: vuelve al mapa y registra abandono");

                // regresión de la revisión técnica (EJ-002): reintentar con tutorial no hereda el cartel; Enter no avanza dos pasos
                prog.Save.tutorialRepeat = true;
                boot.QaStartLevel("m1_n1", true);
                m = boot.CurrentMatch;
                yield return WaitUntilOr(() => m.State == MatchState.Tutorial, 10f);
                Check(boot.QaHud.TutorialVisible, "nivel 1 con tutorial: el cartel aparece");
                boot.QaPause();
                boot.QaRestart();
                yield return null;
                Check(!boot.QaHud.TutorialVisible && boot.CurrentMatch != m && boot.CurrentMatch.TutorialEnabled, "reintentar: el cartel del tutorial no queda colgado y la partida nueva lo vuelve a ofrecer");
                var es = UnityEngine.EventSystems.EventSystem.current;
                Check(es != null && !es.sendNavigationEvents, "Enter no llega dos veces: el EventSystem no manda Submit a los botones");
                boot.QaShowMap("m1_n1");
                prog.Save.tutorialRepeat = false;

                // borrar el progreso: el botón lo dice y el aviso de datos vuelve (RQ-002.1 CA5)
                boot.QaResetProgress();
                yield return null;
                boot.QaShowTitle();
                yield return new WaitForSecondsRealtime(0.2f);
                Check(menus.Current == MenuScreen.Notice && boot.QaProgression.Currency == 0 && boot.QaProgression.Save.levels.Length == 0, "borrar el progreso: queda como recién instalado y el aviso de datos vuelve");
                yield return Shot("45_aviso_despues_de_borrar");
                boot.QaAcceptNotice();
                boot.QaShowTitle();
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot("44_inicio_final");
            }
            Finish();
        }

        static bool SameLevels(TowerTypeData a, TowerTypeData b)
        {
            for (int i = 0; i < a.levels.Length; i++)
            {
                var x = a.levels[i]; var y = b.levels[i];
                if (Math.Abs(x.damage - y.damage) > 1e-3f || Math.Abs(x.interval - y.interval) > 1e-3f || Math.Abs(x.range - y.range) > 1e-3f || Math.Abs(x.areaRadius - y.areaRadius) > 1e-3f
                    || x.chainJumps != y.chainJumps || Math.Abs(x.goldPerSecond - y.goldPerSecond) > 1e-3f || Math.Abs(x.goldCapacity - y.goldCapacity) > 1e-3f || Math.Abs(x.burnDps - y.burnDps) > 1e-3f
                    || (x.rampDps != null && y.rampDps != null && x.rampDps.Length > 0 && Math.Abs(x.rampDps[0] - y.rampDps[0]) > 1e-3f)) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ un nivel
        IEnumerator PlayLevel(string id, int index, string reward)
        {
            var prog = boot.QaProgression;
            bool tutorial = index == 0;
            boot.QaShowMap(id);
            yield return new WaitForSecondsRealtime(0.2f);
            if (index > 0) yield return Shot($"{10 + index}_mapa_antes_nivel{index + 1}");
            boot.QaStartLevel(id, tutorial);
            var m = boot.CurrentMatch;
            Check(m != null && boot.QaLevelId == id && m.Level.id == id, $"{id}: arranca desde el mapa");
            if (m == null) yield break;
            var expected = new List<string> { "arqueras", "canon", "mago" };
            string[] unlocks = { "mortero", "bombardera", "electrica", "infernal", "oro", "lanzallamas" };
            for (int k = 0; k < index; k++) expected.Add(unlocks[k]);
            Check(m.TowerTypes.Select(t => t.id).SequenceEqual(expected), $"{id}: torres disponibles = iniciales + desbloqueadas ({m.TowerTypes.Count})");

            if (tutorial)
            {
                yield return WaitUntilOr(() => m.State == MatchState.Tutorial, 8f);
                Check(m.State == MatchState.Tutorial, $"{id}: tutorial integrado en el nivel 1 (Doc 02 §6)");
                yield return Shot("06_nivel1_tutorial");
                boot.QaTutorialNext(); boot.QaTutorialNext(); boot.QaTutorialNext();
                boot.QaSelectType("arqueras");
                boot.QaPointerAt(new Vector3(m.Level.tutorialHint.x, 0f, m.Level.tutorialHint.z));
                yield return new WaitForSecondsRealtime(0.25f);
                Check(boot.QaClick(new Vector3(m.Level.tutorialHint.x, 0f, m.Level.tutorialHint.z)), $"{id}: la Arqueras del tutorial se construye en la pista");
                boot.QaPointerAt(null);
                boot.QaTutorialNext(); boot.QaTutorialNext();
                Check(m.State == MatchState.Wave, $"{id}: cerrado el tutorial arranca la oleada 1");
            }

            boot.MaxStepsPerFrame = 64;
            Time.timeScale = speed;
            var plan = Plan(index);
            int next = 0;
            var spotCache = new Dictionary<string, List<Vector3>>();
            bool shotMid = false, shotBoss = false, shotPanel = false, shotLate = false;
            float guard = Time.realtimeSinceStartup + 900f;
            while (!m.Ended && Time.realtimeSinceStartup < guard)
            {
                if (m.State == MatchState.Wave || m.State == MatchState.Interval)
                {
                    foreach (var t in m.Towers.ToList())
                        if (t.Type.attack == AttackKind.Gold && t.Full) { boot.QaClick(new Vector3(t.Position.x, 0, t.Position.z)); boot.QaClick(Vector3.zero); }
                    for (int step = 0; step < 3 && next < plan.Length; step++)
                    {
                        string o = plan[next];
                        if (o == "^")
                        {
                            var up = m.Towers.FirstOrDefault(t => t.CanUpgrade && t.Type.attack != AttackKind.Gold) ?? m.Towers.FirstOrDefault(t => t.CanUpgrade);
                            if (up == null) { next++; continue; }
                            if (m.Gold < m.UpgradeCost(up)) break;
                            boot.QaSelectTower(up.Id);
                            if (!shotPanel && index >= 3) { shotPanel = true; boot.QaUpgradeHover(true); yield return Shot($"3{index}_panel_torre_{up.Type.id}"); boot.QaUpgradeHover(false); }
                            boot.QaUpgrade();
                            boot.QaClick(Vector3.zero);
                            next++;
                            continue;
                        }
                        var tt = m.GetTowerType(o);
                        if (tt == null) { next++; continue; }
                        if (m.Gold < tt.cost) break;
                        if (!spotCache.TryGetValue(o, out var spots)) { spots = RankSpots(m, tt); spotCache[o] = spots; }
                        boot.QaSelectType(o);
                        bool built = false;
                        foreach (var s in spots)
                            if (m.CheckPlacement(o, new Vec2(s.x, s.z)) == RejectReason.None) { built = boot.QaClick(s); break; }
                        if (!built) boot.QaCancel();
                        next++;
                    }
                }
                if (!shotMid && m.WaveNumber >= Math.Max(2, m.WaveCount / 2) && m.Enemies.Count >= 6) { shotMid = true; yield return Shot($"{30 + index}_nivel{index + 1}_mitad"); }
                if (!shotBoss && m.Enemies.Any(e => e.Type.miniboss && e.Distance > 8f)) { shotBoss = true; yield return Shot($"{30 + index}_nivel{index + 1}_miniboss"); Check(boot.QaHud != null, $"{id}: aparece el miniboss con su barra"); }
                if (!shotLate && m.WaveNumber == m.WaveCount && m.Enemies.Count >= 8) { shotLate = true; yield return Shot($"{30 + index}_nivel{index + 1}_ultima_oleada"); }
                yield return null;
            }
            Time.timeScale = 1f;
            boot.MaxStepsPerFrame = 8;
            Check(m.Ended, $"{id}: la partida termina");
            Line($"{id}: {m.Result}, vida {m.BaseHp}, estrellas {m.Stars}, duración {MatchRecorder.Clock(m.ActiveTime)}, oro {m.Gold}, torres {m.Towers.Count}");
            yield return WaitUntilOr(() => boot.QaHud.ResultVisible, 3f);
            Check(boot.QaHud.ResultVisible && boot.QaHud.CampaignResult, $"{id}: pantalla de resultado de campaña");
            Check(m.State == MatchState.Victory, $"{id}: el plan combinado gana ({m.Stars} estrellas, vida {m.BaseHp})");
            var p = boot.QaProgression;
            Check(p.StarsOf(id) == m.Stars && p.IsTowerUnlocked(reward), $"{id}: estrellas guardadas y {reward} desbloqueada");
            var rep = boot.QaMetrics.LastReport;
            Check(rep != null && rep.checks.All(c => c.StartsWith("OK")) && rep.levelId == id, $"{id}: el registro cierra sus cuentas");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot($"{50 + index}_resultado_nivel{index + 1}");
        }

        static string[] Plan(int li)
        {
            string[] comp;
            switch (li)
            {
                case 0: comp = new[] { "arqueras", "canon", "^", "mago", "^", "arqueras", "canon", "^", "mago", "^" }; break;
                case 1: comp = new[] { "arqueras", "canon", "mortero", "^", "mago", "^", "arqueras", "mortero", "^", "canon" }; break;
                case 2: comp = new[] { "arqueras", "canon", "bombardera", "^", "mago", "arqueras", "^", "mortero", "^", "arqueras" }; break;
                case 3: comp = new[] { "arqueras", "canon", "electrica", "^", "bombardera", "mago", "^", "mortero", "^", "arqueras", "electrica" }; break;
                case 4: comp = new[] { "arqueras", "canon", "infernal", "^", "electrica", "bombardera", "^", "arqueras", "mortero", "^", "infernal" }; break;
                default: comp = new[] { "arqueras", "oro", "canon", "infernal", "^", "electrica", "oro", "bombardera", "^", "arqueras", "mortero", "^", "infernal", "^", "mago" }; break;
            }
            return Enumerable.Range(0, 400).Select(i => comp[i % comp.Length]).ToArray();
        }

        /// <summary>Mismo criterio que el bot de ClashDefenseSim: el lugar libre que más recorrido cubre (la de oro, el que menos).</summary>
        static List<Vector3> RankSpots(Match m, TowerTypeData t)
        {
            var list = new List<(Vector3 p, float c)>();
            var a = m.Level.buildArea;
            var s = t.levels[0];
            for (float x = a.minX; x <= a.maxX; x += 0.5f)
                for (float z = a.minZ; z <= a.maxZ; z += 0.5f)
                {
                    var p = new Vec2(x, z);
                    if (m.CheckGeometry(t, p) != RejectReason.None) continue;
                    float c = 0f;
                    if (t.attack == AttackKind.Gold) foreach (var r in m.Routes) c -= r.CoverageWithin(p, 8f, 0.5f);
                    else foreach (var r in m.Routes) { c += r.CoverageWithin(p, s.range, 0.5f); if (s.minRange > 0f) c -= r.CoverageWithin(p, s.minRange, 0.5f); }
                    list.Add((new Vector3(x, 0f, z), c));
                }
            return list.OrderByDescending(v => v.c).Select(v => v.p).ToList();
        }

        void Finish()
        {
            Check(exceptions == 0, $"sin excepciones en consola ({exceptions})");
            Check(errors == 0, $"sin errores en consola ({errors})");
            var sb = new StringBuilder();
            sb.Append("TL-002 piloto de campaña · ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
            sb.Append($"verificaciones: {checks.Count(c => c.StartsWith("OK"))} OK · {checks.Count(c => c.StartsWith("FALLA"))} FALLA\n\n");
            foreach (var c in checks) sb.Append(c).Append('\n');
            sb.Append("\n--- bitácora ---\n").Append(log);
            File.WriteAllText(Path.Combine(dir, "qa_informe.txt"), sb.ToString());
            File.WriteAllText(Path.Combine(dir, "qa_listo.txt"), "listo");
            Time.timeScale = 1f;
            SaveStore.Key = SaveStore.DefaultKey;
        }
    }
}
