using System;
using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClashDefense.Game
{
    /// <summary>
    /// Dueño del ciclo de la aplicación (SOL-001, SOL-002): carga y valida los datos, arma cámara, mundo, HUD, menús,
    /// sonido y registro, corre el reloj fijo del Match y traduce la entrada del jugador a comandos del Match o de la
    /// progresión. No contiene reglas de juego: viven en ClashDefense.Core (Match y Progression).
    /// Dos modos: con campaign_w1 asignado es el juego (pantalla inicial → mapa → niveles); sin él, el P0 tal como se entregó.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Prototipo 0 (RQ-001.8) — también el Laboratorio de la campaña")]
        [SerializeField] TextAsset balanceJson = null;
        [SerializeField] TextAsset levelJson = null;
        [Header("Mundo 1 (RQ-002.x). Vacío = modo P0")]
        [SerializeField] TextAsset campaignJson = null;
        [SerializeField] TextAsset worldBalanceJson = null;
        [SerializeField] TextAsset[] levelJsons = new TextAsset[0];
        [Header("Materiales (los crea el builder de la escena)")]
        [SerializeField] Material solidMaterial = null;
        [SerializeField] Material fadeMaterial = null;
        [SerializeField] Material lineMaterial = null;

        const float SellConfirmWindow = 2f;   // GDS-001.3, supuesto S8 (tiempo real)
        const string Version = "w1-0.1";

        static bool tutorialShownThisSession;

        // datos
        BalanceData p0Balance, w1Balance;
        LevelData p0Level;
        CampaignData campaignData;
        readonly Dictionary<string, LevelData> levels = new Dictionary<string, LevelData>();
        Progression prog;
        bool Campaign => campaignData != null;

        // presentación
        Visuals vis;
        CameraRig rig;
        WorldView world;
        Hud hud;
        Menus menus;
        Sfx sfx;
        MetricsWriter metrics;

        // partida en curso
        Match match;
        BalanceData balance;       // el de la partida en curso
        LevelData level;           // el de la partida en curso
        string currentLevelId;     // null = P0 / Laboratorio
        bool labMode;
        bool levelTutorial;   // con qué se empezó el nivel actual: reintentar respeta la elección (revisión técnica, hallazgo 3)
        string saveKey = SaveStore.DefaultKey;   // el guardado queda atado a su clave: el piloto nunca escribe en la del owner (hallazgo 1)
        float acc;
        bool resultShown;
        readonly List<SimEvent> buffer = new List<SimEvent>();

        string selectedType;
        int selectedTower;
        bool sellArmed; float sellArmedAt;
        bool upgradeHover;

        public Match CurrentMatch => match;
        public int MaxStepsPerFrame = 8;

        // ------------------------------------------------------------------ arranque
        void Awake()
        {
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            // el teclado lo maneja el juego (UXS-001.6/002.x): si el EventSystem también manda Submit al último botón clickeado,
            // Enter actúa dos veces (revisión técnica, hallazgo 4)
            var evs = EventSystem.current != null ? EventSystem.current : FindAnyObjectByType<EventSystem>();
            if (evs != null) evs.sendNavigationEvents = false;

            var errors = LoadData();

            var cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }

            vis = new Visuals(solidMaterial, fadeMaterial, lineMaterial);
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(errors.Count == 0 ? (Campaign ? w1Balance : p0Balance) : new BalanceData { towers = new TowerTypeData[0] });
            WireHud();
            sfx = gameObject.AddComponent<Sfx>();
            metrics = new MetricsWriter();

            if (errors.Count > 0)
            {
                Debug.LogError("[ClashDefense] Datos inválidos:\n - " + string.Join("\n - ", errors));
                hud.ShowError(errors);
                return;
            }

            world = new GameObject("Mundo").AddComponent<WorldView>();
            world.transform.SetParent(transform, false);

            if (Campaign)
            {
                prog = new Progression(campaignData, SaveStore.Load(saveKey));
                menus = new GameObject("Menus").AddComponent<Menus>();
                menus.transform.SetParent(transform, false);
                menus.Build(prog, w1Balance, Version, metrics.Folder);
                menus.SetLevelTutorial(!prog.Save.tutorialDone || prog.Save.tutorialRepeat);
                WireMenus();
                ShowBackdrop(FirstLevelId());
                if (!prog.Save.noticeAccepted) menus.ShowNotice(); else menus.ShowTitle();
            }
            else
            {
                rig = new CameraRig(cam, WorldView.ContentBounds(p0Level));
                world.BuildMap(vis, p0Level, cam);
                ShowStart();
            }
        }

        List<string> LoadData()
        {
            var errors = new List<string>();
            try
            {
                if (balanceJson != null) p0Balance = JsonUtility.FromJson<BalanceData>(balanceJson.text);
                if (levelJson != null) p0Level = JsonUtility.FromJson<LevelData>(levelJson.text);
                if (campaignJson != null)
                {
                    campaignData = JsonUtility.FromJson<CampaignData>(campaignJson.text);
                    w1Balance = worldBalanceJson != null ? JsonUtility.FromJson<BalanceData>(worldBalanceJson.text) : null;
                    foreach (var ta in levelJsons)
                        if (ta != null) levels[ta.name] = JsonUtility.FromJson<LevelData>(ta.text);
                }
            }
            catch (Exception ex) { errors.Add("JSON ilegible: " + ex.Message); return errors; }

            if (p0Balance == null) errors.Add("falta balance_p0.json en GameBootstrap");
            if (p0Level == null) errors.Add("falta level_p0.json en GameBootstrap");
            if (errors.Count == 0) errors.AddRange(DataValidator.Validate(p0Balance, p0Level));
            if (campaignJson != null)
            {
                if (w1Balance == null) { errors.Add("falta balance_w1.json en GameBootstrap"); return errors; }
                errors.AddRange(DataValidator.ValidateBalance(w1Balance));
                errors.AddRange(Progression.Validate(campaignData, w1Balance));
                foreach (var w in campaignData.worlds)
                    foreach (var c in w.continents)
                        foreach (var l in c.levels)
                        {
                            if (!l.available) continue;
                            if (!levels.TryGetValue(l.file, out var ld)) { errors.Add($"falta el archivo {l.file}.json del nivel {l.id}"); continue; }
                            errors.AddRange(DataValidator.ValidateLevel(w1Balance, ld));
                        }
            }
            return errors;
        }

        string FirstLevelId()
        {
            foreach (var l in prog.AllLevels()) if (l.available) return l.id;
            return null;
        }

        /// <summary>El mapa del nivel elegido queda de fondo detrás de los menús (UXS-002.1).</summary>
        void ShowBackdrop(string levelId)
        {
            var lm = levelId != null ? prog.FindLevel(levelId) : null;
            var ld = lm != null && levels.TryGetValue(lm.file, out var l) ? l : p0Level;
            world.Clear();
            world.BuildMap(vis, ld, Camera.main);
            FitCamera(ld);
        }

        void FitCamera(LevelData ld)
        {
            var b = WorldView.ContentBounds(ld);
            if (rig == null) rig = new CameraRig(Camera.main, b); else rig.SetContent(b);
        }

        void WireHud()
        {
            hud.PlayClicked += t => { sfx.Play("click"); StartP0(t); };
            hud.QuitClicked += () => { if (Campaign && labMode) { sfx.Play("click"); ShowTitle(); } else Quit(); };
            hud.PauseClicked += () => { if (match != null && match.State != MatchState.Paused) Pause(); };
            hud.ResumeClicked += Resume;
            hud.RestartClicked += Restart;
            hud.ExitClicked += ExitFromMatch;
            hud.RetryClicked += Restart;
            hud.NextClicked += NextLevel;
            hud.TandaClicked += () => { sfx.Play("click"); menus?.ShowTanda(); };
            hud.TutorialNextClicked += () => { sfx.Play("click"); match?.TutorialNext(); };
            hud.CardClicked += SelectType;
            hud.UpgradeClicked += Upgrade;
            hud.SellClicked += Sell;
            hud.CollectClicked += Collect;
            hud.UpgradeHover += on => { upgradeHover = on; RefreshPreview(); };
            hud.VolumeChanged += SetVolume;
            hud.MuteChanged += SetMuted;
        }

        void WireMenus()
        {
            menus.PlayClicked += () => { sfx.Play("click"); ShowMap(null); };
            menus.ShopClicked += () => { sfx.Play("click"); menus.ShowShop(menus.Current == MenuScreen.Map ? MenuScreen.Map : MenuScreen.Title); };
            menus.OptionsClicked += () => { sfx.Play("click"); menus.ShowOptions(MenuScreen.Title, sfx.Volume, sfx.Muted); };
            menus.LabClicked += () => { sfx.Play("click"); OpenLab(); };
            menus.QuitClicked += Quit;
            menus.NoticeAccepted += () => { sfx.Play("click"); prog.Save.noticeAccepted = true; SaveNow(); menus.ShowTitle(); };
            menus.BackClicked += OnMenuBack;
            menus.LevelSelected += id => { sfx.Play("click"); ShowBackdrop(id); };
            menus.LevelLocked += id => sfx.Play("rechazo", 0.7f);
            menus.BuyRejected += id => sfx.Play("rechazo", 0.7f);
            menus.LevelPlay += (id, tut) => { sfx.Play("click"); StartLevel(id, tut); };
            menus.BuyClicked += Buy;
            menus.TandaChosen += ChooseTanda;
            menus.VolumeChanged += SetVolume;
            menus.MuteChanged += SetMuted;
            menus.TutorialRepeatChanged += v => { prog.Save.tutorialRepeat = v; SaveNow(); menus.SetLevelTutorial(!prog.Save.tutorialDone || v); };
            menus.ResetProgress += ResetProgress;
        }

        void SetVolume(float v) { sfx.Volume = v; PlayerPrefs.SetFloat("cd_volumen", v); PlayerPrefs.Save(); hud.SyncAudio(v, sfx.Muted); }
        void SetMuted(bool m) { sfx.Muted = m; PlayerPrefs.SetInt("cd_silencio", m ? 1 : 0); PlayerPrefs.Save(); hud.SyncAudio(sfx.Volume, m); }

        void SaveNow() { if (prog != null) SaveStore.Save(prog.Save, saveKey); }

        // ------------------------------------------------------------------ navegación de la campaña (UXS-002.1, .2)
        void ShowTitle()
        {
            CloseCurrent();
            ClearSelection();
            labMode = false;
            Time.timeScale = 1f;
            hud.HideAll();
            ShowBackdrop(menus.SelectedLevel ?? FirstLevelId());
            // después de borrar el progreso el aviso de datos vuelve a aparecer, como en una instalación nueva (RQ-002.1 CA5)
            if (!prog.Save.noticeAccepted) menus.ShowNotice(); else menus.ShowTitle();
        }

        void ShowMap(string select)
        {
            CloseCurrent();
            ClearSelection();
            labMode = false;
            Time.timeScale = 1f;
            hud.HideAll();
            menus.ShowMap(select);
            ShowBackdrop(menus.SelectedLevel);
            if (menus.WorldTab > 0 && prog.IsWorldUnlocked(prog.Campaign.worlds[menus.WorldTab]) && !prog.WorldSeen(prog.Campaign.worlds[menus.WorldTab].id))
            { prog.MarkWorldSeen(prog.Campaign.worlds[menus.WorldTab].id); SaveNow(); }
        }

        void OnMenuBack(MenuScreen from)
        {
            sfx.Play("click");
            switch (from)
            {
                case MenuScreen.Map: ShowTitle(); break;
                case MenuScreen.Shop: if (menus.ShopReturn == MenuScreen.Map) ShowMap(null); else ShowTitle(); break;
                case MenuScreen.Options: ShowTitle(); break;
                case MenuScreen.Tanda:
                    if (hud.ResultVisible) menus.HideAll();
                    else ShowMap(null);
                    break;
                default: ShowTitle(); break;
            }
        }

        void OpenLab()
        {
            labMode = true;
            menus.HideAll();
            world.Clear();
            world.BuildMap(vis, p0Level, Camera.main);
            FitCamera(p0Level);
            hud.SetExitLabel("Salir al laboratorio  [S]");
            hud.ResultExitLabel = "Volver al laboratorio  [Esc]";
            ShowStart();
        }

        void Buy(string id)
        {
            if (!prog.Buy(id)) { sfx.Play("rechazo", 0.7f); menus.RebuildShop(); return; }
            var item = prog.FindShopItem(id);
            sfx.Play("compra");
            SaveNow();
            metrics.CampaignEvent("compra_tienda", ("item", id), ("tower_type", item.tower), ("cost", item.cost.ToString()), ("currency_left", prog.Currency.ToString()));
            menus.RebuildShop();
        }

        void ChooseTanda(string towerId)
        {
            if (!prog.ChooseTanda(towerId)) { sfx.Play("rechazo", 0.7f); return; }
            sfx.Play("mejorar");
            SaveNow();
            metrics.CampaignEvent("mejora_tanda_elegida", ("tower_type", towerId), ("tier", prog.TandaTier(towerId).ToString()));
            if (prog.Save.tandaPending > 0) { menus.ShowTanda(); return; }
            if (hud.ResultVisible) { menus.HideAll(); hud.HideTandaButton(); }
            else ShowMap(null);
        }

        void ResetProgress()
        {
            SaveStore.Reset(saveKey);
            prog = new Progression(campaignData, new SaveData());
            menus.Rebind(prog);
            menus.SetLevelTutorial(true);
            sfx.Play("vender");
            menus.ShowOptions(MenuScreen.Title, sfx.Volume, sfx.Muted);
            menus.ShowResetDone();
        }

        // ------------------------------------------------------------------ flujo de partida (GDS-001.6, GDS-002.2)
        void StartLevel(string levelId, bool tutorial)
        {
            var lm = prog.FindLevel(levelId);
            if (lm == null || !prog.IsLevelUnlocked(levelId) || !levels.TryGetValue(lm.file, out var ld)) { sfx.Play("rechazo", 0.7f); return; }
            CloseCurrent();
            menus.HideAll();
            labMode = false;
            currentLevelId = levelId;
            balance = w1Balance;
            level = ld;
            world.Clear();
            world.BuildMap(vis, ld, Camera.main);
            FitCamera(ld);
            levelTutorial = tutorial;
            var options = prog.Loadout(tutorial && !string.IsNullOrEmpty(ld.tutorialTowerId));
            prog.ApplyStart(levelId);
            SaveNow();
            var world0 = prog.WorldOf(levelId);
            BeginMatch(new Match(balance, level, options), $"{world0.displayName} · Nivel {lm.number} — {lm.displayName}");
            hud.SetExitLabel("Salir al mapa  [S]");
            AnnounceNewTowers();
        }

        /// <summary>Mensaje de desbloqueo (Doc 03 §26 UX/UI): la primera vez que una torre está disponible en una partida.</summary>
        void AnnounceNewTowers()
        {
            bool first = prog.Save.seenTowers.Length == 0;
            foreach (var t in match.TowerTypes)
            {
                if (prog.TowerSeen(t.id)) continue;
                prog.MarkTowerSeen(t.id);
                if (!first) { hud.ShowToast($"Nueva torre: {t.displayName} — {t.description}", 6f); sfx.Play("desbloqueo"); }
            }
            SaveNow();
        }

        void StartP0(bool tutorial)
        {
            CloseCurrent();
            currentLevelId = null;
            balance = p0Balance;
            level = p0Level;
            if (tutorial) tutorialShownThisSession = true;
            BeginMatch(new Match(balance, level, tutorial), null);
            if (Campaign) hud.SetExitLabel("Salir al laboratorio  [S]");
        }

        void BeginMatch(Match m, string label)
        {
            match = m;
            world.Bind(match);
            sfx.ResetMatch();
            var bosses = new System.Collections.Generic.HashSet<string>();
            foreach (var et in match.Balance.enemies) if (et != null && et.miniboss) bosses.Add(et.id);
            sfx.IsMiniboss = bosses.Contains;
            metrics.Begin(match, currentLevelId);
            hud.ShowGame(match, label);
            ClearSelection();
            resultShown = false;
            acc = 0f;
            Time.timeScale = 1f;
            match.Begin();
            Flush();
        }

        /// <summary>Si hay una partida sin terminar, se registra como abandono (MET-001.7: partida_terminada una vez, también en abandono).</summary>
        void CloseCurrent()
        {
            if (match != null && !match.Ended)
            {
                match.Abandon();
                Flush();
                metrics.End();
            }
            match = null;
            world?.Clear();
        }

        void Pause()
        {
            if (match == null || !match.Pause()) return;
            Time.timeScale = 0f;
            world.HideGhost();
            hud.HideCursorLabel();
            hud.ShowPause(true);
            sfx.Play("click");
            Flush();
        }

        void Resume()
        {
            if (match == null || !match.Resume()) return;
            Time.timeScale = 1f;
            hud.ShowPause(false);
            sfx.Play("click");
            Flush();
        }

        void Restart()
        {
            sfx.Play("click");
            if (currentLevelId != null) StartLevel(currentLevelId, (levelTutorial && !prog.Save.tutorialDone) || prog.Save.tutorialRepeat);
            else StartP0(false);
        }

        void NextLevel()
        {
            sfx.Play("click");
            var next = currentLevelId != null ? prog.NextLevel(currentLevelId) : null;
            if (next != null && prog.IsLevelUnlocked(next.id)) StartLevel(next.id, false);
            else ExitFromMatch();
        }

        void ExitFromMatch()
        {
            sfx.Play("click");
            if (Campaign && !labMode && currentLevelId != null) { var id = currentLevelId; ShowMap(id); return; }
            CloseCurrent();
            ClearSelection();
            if (Campaign && labMode) { world.BuildMap(vis, p0Level, Camera.main); ShowStart(); return; }
            ShowStart();
        }

        void ShowStart()
        {
            Time.timeScale = 1f;
            hud.ShowStart(!tutorialShownThisSession, $"balance {p0Balance.version} · nivel {p0Level.id} · build {Application.version} · registro en {metrics.Folder}",
                Campaign ? "Volver al inicio  [S]" : "Salir  [S]", Campaign ? "Laboratorio: el nivel de prueba del núcleo (Doc 05). No da estrellas ni cristales." : null, Campaign);
        }

        void Quit()
        {
            CloseCurrent();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnApplicationQuit()
        {
            if (match != null && !match.Ended) { match.Abandon(); Flush(); metrics.End(); }
            SaveNow();
        }

        // ------------------------------------------------------------------ cuadro a cuadro
        void Update()
        {
            rig?.Tick();
            HandleKeys();
            if (match == null) return;

            if (match.State == MatchState.Paused) metrics.AddPause(Time.unscaledDeltaTime);
            else if (!match.Ended)
            {
                acc = Mathf.Min(acc + Time.deltaTime, Match.Step * MaxStepsPerFrame);
                while (acc >= Match.Step) { match.Tick(); acc -= Match.Step; }
            }
            Flush();
            HandlePointer();
            UpdateHint();

            if (sellArmed && Time.unscaledTime - sellArmedAt > SellConfirmWindow) { sellArmed = false; RefreshTowerPanel(); }

            if (match.Ended && !resultShown)
            {
                resultShown = true;
                ClearSelection();
                metrics.End();
                hud.ShowResult(metrics.LastReport, match, metrics.LastReportPath, CampaignOutcome());
            }
        }

        /// <summary>Aplica la victoria a la progresión y arma lo que la pantalla de resultado cuenta (Doc 03 §22).</summary>
        CampaignResultInfo CampaignOutcome()
        {
            if (!Campaign || labMode || currentLevelId == null) return null;
            var lm = prog.FindLevel(currentLevelId);
            bool win = match.State == MatchState.Victory;
            var info = new CampaignResultInfo { LevelName = lm.displayName, Victory = win, CurrencyName = prog.CurrencyName };
            if (win)
            {
                var r = prog.ApplyVictory(currentLevelId, match.Stars, match.BaseHp);
                if (match.TutorialPlayed) prog.Save.tutorialDone = true;
                menus.SetLevelTutorial(!prog.Save.tutorialDone || prog.Save.tutorialRepeat);
                SaveNow();
                info.Reward = r;
                if (!string.IsNullOrEmpty(r.TowerUnlocked))
                {
                    var t = Array.Find(w1Balance.towers, x => x.id == r.TowerUnlocked);
                    info.TowerName = t != null ? t.displayName : r.TowerUnlocked;
                    info.TowerDescription = t != null ? t.description : "";
                    sfx.Play("desbloqueo");
                }
                if (!string.IsNullOrEmpty(r.WorldUnlocked))
                {
                    var w = Array.Find(prog.Campaign.worlds, x => x.id == r.WorldUnlocked);
                    info.WorldName = w != null ? w.displayName : r.WorldUnlocked;
                    info.FollowText = prog.Campaign.followText;
                }
                metrics.CampaignEvent("progreso_recompensa", ("level_id", currentLevelId), ("stars", match.Stars.ToString()), ("currency", r.Currency.ToString()),
                    ("first_clear", r.FirstClear ? "1" : "0"), ("tower_unlocked", r.TowerUnlocked ?? ""), ("tanda_earned", r.TandaEarned ? "1" : "0"), ("world_unlocked", r.WorldUnlocked ?? ""));
            }
            var next = prog.NextLevel(currentLevelId);
            info.HasNext = next != null && prog.IsLevelUnlocked(next.id);
            info.NextName = next?.displayName;
            info.CurrencyTotal = prog.Currency;
            info.TandaPending = prog.Save.tandaPending;
            return info;
        }

        void Flush()
        {
            if (match == null) return;
            buffer.Clear();
            match.DrainEvents(buffer);
            foreach (var e in buffer) Dispatch(e);
        }

        void Dispatch(SimEvent e)
        {
            world.OnEvent(e);
            hud.OnEvent(e);
            sfx.OnEvent(e);
            metrics.OnEvent(e);
            switch (e.Type)
            {
                case SimEventType.TutorialStep: ApplyTutorialStep(e.Int1); break;
                case SimEventType.WaveStarted:
                    // el tutorial se completa al llegar a la oleada 1 con él: desde ahí el nivel 1 ya no lo ofrece solo (GDS-002.2, S20)
                    if (Campaign && !labMode && e.Int1 == 1 && match.TutorialPlayed && prog != null && !prog.Save.tutorialDone)
                    { prog.Save.tutorialDone = true; SaveNow(); menus.SetLevelTutorial(prog.Save.tutorialRepeat); }
                    break;
                case SimEventType.StateChanged: if (match.State != MatchState.Tutorial) hud.SetCardsInteractable(id => match.CanSelectTower(id)); break;
                case SimEventType.GoldChanged:
                case SimEventType.TowerUpgraded: RefreshTowerPanel(); break;
                case SimEventType.TowerSold: if (e.TowerId == selectedTower) CloseTowerPanel(); break;
            }
        }

        void ApplyTutorialStep(int step)
        {
            string towerId = level.tutorialTowerId;
            var type = match.GetTowerType(towerId);
            hud.SetTutorialStep(step, towerId, type != null ? type.displayName : towerId, match.WaveCount, match.Gold);
            world.Highlight(step == 1 ? "base" : step == 2 ? "entrada" : step == 5 ? "pista" : null);
            if (step == 4 || step == 5) hud.SetCardsInteractable(id => id == towerId);
            else if (step > 0) hud.SetCardsInteractable(id => false);
            else hud.SetCardsInteractable(null);
        }

        // ------------------------------------------------------------------ teclado (UXS-001.6, UXS-002.1: una tecla, un verbo)
        static readonly KeyCode[] CardKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Equals };
        static readonly KeyCode[] PadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.Keypad0, KeyCode.KeypadMinus, KeyCode.KeypadPlus };

        static bool Enter() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        void HandleKeys()
        {
            if (hud == null) return;
            if (hud.ErrorVisible) { if (Input.GetKeyDown(KeyCode.S)) Quit(); return; }
            if (menus != null && menus.Current != MenuScreen.None) { HandleMenuKeys(); return; }
            if (hud.StartVisible)
            {
                if (Enter()) { sfx.Play("click"); StartP0(hud.StartTutorialChecked); }
                else if (Input.GetKeyDown(KeyCode.S) && (Campaign || Application.platform != RuntimePlatform.WebGLPlayer)) { if (Campaign) { sfx.Play("click"); ShowTitle(); } else Quit(); }
                else if (Input.GetKeyDown(KeyCode.Escape) && Campaign) { sfx.Play("click"); ShowTitle(); }
                return;
            }
            if (hud.ResultVisible)
            {
                if (Enter()) { if (hud.CampaignResult && hud.ResultHasNext) NextLevel(); else Restart(); }
                else if (Input.GetKeyDown(KeyCode.R) && hud.CampaignResult) Restart();
                else if (Input.GetKeyDown(KeyCode.M) && hud.TandaButtonVisible) { sfx.Play("click"); menus.ShowTanda(); }
                else if (Input.GetKeyDown(KeyCode.Escape)) ExitFromMatch();
                return;
            }
            if (match == null) return;

            if (match.State == MatchState.Paused)
            {
                if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) Resume();
                else if (Input.GetKeyDown(KeyCode.R)) Restart();
                else if (Input.GetKeyDown(KeyCode.S)) ExitFromMatch();
                return;
            }
            if (match.Ended) return;

            if (Input.GetKeyDown(KeyCode.P)) { Pause(); return; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (selectedType != null) CancelPlacement();
                else if (selectedTower != 0) CloseTowerPanel();
                else Pause();
                return;
            }
            if (Enter() && match.State == MatchState.Tutorial && hud.TutorialStepNeedsButton)
            {
                sfx.Play("click");
                match.TutorialNext();
                return;
            }
            var types = match.TowerTypes;
            for (int i = 0; i < types.Count && i < CardKeys.Length; i++)
                if (Input.GetKeyDown(CardKeys[i]) || Input.GetKeyDown(PadKeys[i])) { SelectType(types[i].id); return; }
            if (Input.GetKeyDown(KeyCode.U)) Upgrade();
            if (Input.GetKeyDown(KeyCode.V)) Sell();
            if (Input.GetKeyDown(KeyCode.C)) Collect();
        }

        void HandleMenuKeys()
        {
            switch (menus.Current)
            {
                case MenuScreen.Notice:
                    if (Enter()) { sfx.Play("click"); prog.Save.noticeAccepted = true; SaveNow(); menus.ShowTitle(); }
                    break;
                case MenuScreen.Title:
                    if (Enter()) { sfx.Play("click"); ShowMap(null); }
                    else if (Input.GetKeyDown(KeyCode.T)) { sfx.Play("click"); menus.ShowShop(MenuScreen.Title); }
                    else if (Input.GetKeyDown(KeyCode.O)) { sfx.Play("click"); menus.ShowOptions(MenuScreen.Title, sfx.Volume, sfx.Muted); }
                    else if (Input.GetKeyDown(KeyCode.L)) { sfx.Play("click"); OpenLab(); }
                    else if (Input.GetKeyDown(KeyCode.S) && Application.platform != RuntimePlatform.WebGLPlayer) Quit();
                    break;
                case MenuScreen.Map:
                    if (Enter() && menus.SelectedLevel != null) { sfx.Play("click"); StartLevel(menus.SelectedLevel, menus.LevelTutorialChecked); }
                    else if (Input.GetKeyDown(KeyCode.Escape)) OnMenuBack(MenuScreen.Map);
                    else if (Input.GetKeyDown(KeyCode.T)) { sfx.Play("click"); menus.ShowShop(MenuScreen.Map); }
                    else if (Input.GetKeyDown(KeyCode.M) && prog.Save.tandaPending > 0) { sfx.Play("click"); menus.ShowTanda(); }
                    else if (Input.GetKeyDown(KeyCode.Tab)) { sfx.Play("click"); menus.SetWorldTab((menus.WorldTab + 1) % prog.Campaign.worlds.Length); }
                    else if (Input.GetKeyDown(KeyCode.Y)) { sfx.Play("click"); menus.SetLevelTutorial(!menus.LevelTutorialChecked); }
                    else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)) menus.MoveSelection(1);    // LevelSelected ya suena y cambia el fondo
                    else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)) menus.MoveSelection(-1);
                    break;
                case MenuScreen.Shop:
                case MenuScreen.Options:
                    if (Input.GetKeyDown(KeyCode.Escape)) OnMenuBack(menus.Current);
                    break;
                case MenuScreen.Tanda:
                    if (Input.GetKeyDown(KeyCode.Escape)) OnMenuBack(MenuScreen.Tanda);
                    for (int i = 0; i < 3; i++)
                        if (Input.GetKeyDown(CardKeys[i]) || Input.GetKeyDown(PadKeys[i])) { var t = menus.TandaOptionAt(i); if (t != null) ChooseTanda(t); break; }
                    break;
            }
        }

        /// <summary>Línea de ayuda sobre la franja inferior: UXS-001.6, pantallas Partida y Colocando.</summary>
        void UpdateHint()
        {
            string h = "";
            if (match != null && !match.Ended && match.State != MatchState.Tutorial && match.State != MatchState.Paused)
            {
                if (selectedType != null) h = "Cancelar  [Esc / clic derecho]";
                else if (match.Towers.Count > 0) h = HasGoldTower() ? "Clic en una torre para mejorarla o venderla · en la de oro, recoge  [C]" : "Clic en una torre para mejorarla o venderla";
            }
            hud.SetHint(h);
        }

        bool HasGoldTower()
        {
            foreach (var t in match.Towers) if (t.Type.attack == AttackKind.Gold) return true;
            return false;
        }

        // ------------------------------------------------------------------ mouse sobre el mapa
        void HandlePointer()
        {
            if (match == null || match.Ended || match.State == MatchState.Paused) return;
            if (menus != null && menus.Current != MenuScreen.None) return;
            if (qaDriven) { HandleQaPointer(); return; }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool haveGround = rig.ScreenToGround(Input.mousePosition, out var ground);
            var pos = new Vec2(ground.x, ground.z);

            if (selectedType != null)
            {
                if (overUi || !haveGround) { world.HideGhost(); hud.HideCursorLabel(); return; }
                var type = match.GetTowerType(selectedType);
                var reason = match.CheckPlacement(selectedType, pos);
                bool ok = reason == RejectReason.None;
                world.ShowGhost(type, ground, ok);
                hud.SetCursorLabel(ok ? "Construir aquí (clic)" : "No se puede: " + ReasonText(reason, type), ok ? Palette.Valido : Palette.Invalido, Input.mousePosition);
                if (Input.GetMouseButtonDown(0))
                {
                    if (match.TryBuild(selectedType, pos, out _, out _)) CancelPlacement();
                    Flush();
                }
                else if (Input.GetMouseButtonDown(1)) CancelPlacement();
                return;
            }

            if (overUi) return;
            if (Input.GetMouseButtonDown(0) && haveGround)
            {
                int id = world.PickTower(ground);
                if (id != 0) ClickTower(id); else CloseTowerPanel();
            }
            else if (Input.GetMouseButtonDown(1)) CloseTowerPanel();
        }

        /// <summary>Clic en una torre: la de oro además recoge (Doc 04 §6.5: recolección manual mediante clic).</summary>
        void ClickTower(int id)
        {
            var t = match.GetTower(id);
            if (t != null && t.Type.attack == AttackKind.Gold && t.Stored >= 1f) { match.TryCollect(id, out _, out _); Flush(); }
            SelectTower(id);
        }

        // ------------------------------------------------------------------ API de QA
        // La usa QaAutopilot (solo en el editor). Recorre los mismos caminos que la entrada del jugador,
        // salvo el mouse físico: el puntero se fija en un punto del mundo.
        bool qaDriven;
        Vector3? qaPointer;

        public Hud QaHud => hud;
        public Menus QaMenus => menus;
        public WorldView QaWorld => world;
        public MetricsWriter QaMetrics => metrics;
        public Progression QaProgression => prog;
        public bool QaCampaign => Campaign;
        public string QaSelectedType => selectedType;
        public string QaLevelId => currentLevelId;
        public void QaTakeControl() { qaDriven = true; }
        public void QaStart(bool tutorial) { if (Campaign) OpenLab(); StartP0(tutorial); }
        public void QaSelectType(string id) => SelectType(id);
        public void QaPointerAt(Vector3? world) => qaPointer = world;
        public void QaSelectTower(int id) => SelectTower(id);
        public void QaUpgrade() => Upgrade();
        public void QaSell() => Sell();
        public void QaCollect() => Collect();
        public void QaCancel() => ClearSelection();
        public void QaPause() => Pause();
        public void QaResume() => Resume();
        public void QaRestart() => Restart();
        public void QaExitToStart() => ExitFromMatch();
        public void QaNext() => NextLevel();
        public void QaTutorialNext() { if (match != null) { match.TutorialNext(); Flush(); } }
        public void QaUpgradeHover(bool on) { upgradeHover = on; hud.ShowUpgradePreview(on); RefreshPreview(); }
        public void QaShowTitle() => ShowTitle();
        public void QaShowMap(string id) => ShowMap(id);
        public void QaShowShop() => menus.ShowShop(MenuScreen.Map);
        public void QaShowOptions() => menus.ShowOptions(MenuScreen.Title, sfx.Volume, sfx.Muted);
        public void QaStartLevel(string id, bool tutorial) => StartLevel(id, tutorial);
        public void QaBuy(string id) => Buy(id);
        public void QaChooseTanda(string tower) => ChooseTanda(tower);
        public void QaShowTanda() => menus.ShowTanda();
        public void QaAcceptNotice() { prog.Save.noticeAccepted = true; SaveNow(); menus.ShowTitle(); }
        public void QaResetProgress() => ResetProgress();
        /// <summary>El piloto juega con un guardado propio (y opcionalmente vacío): nunca toca el progreso del owner.</summary>
        public void QaUseSaveKey(string key, bool fresh)
        {
            SaveStore.Key = key;
            saveKey = key;
            if (fresh) SaveStore.Reset(key);
            prog = new Progression(campaignData, SaveStore.Load(key));
            menus.Rebind(prog);
            menus.SetLevelTutorial(!prog.Save.tutorialDone || prog.Save.tutorialRepeat);
            if (!prog.Save.noticeAccepted) { CloseCurrent(); hud.HideAll(); menus.ShowNotice(); } else ShowTitle();
        }

        /// <summary>Clic izquierdo en el punto del mundo, por el mismo camino que HandlePointer.</summary>
        public bool QaClick(Vector3 world)
        {
            if (match == null || match.Ended || match.State == MatchState.Paused) return false;
            var pos = new Vec2(world.x, world.z);
            if (selectedType != null)
            {
                bool ok = match.TryBuild(selectedType, pos, out _, out _);
                if (ok) CancelPlacement();
                Flush();
                return ok;
            }
            int id = world == Vector3.zero ? 0 : this.world.PickTower(world);
            if (id != 0) ClickTower(id); else CloseTowerPanel();
            return id != 0;
        }

        void HandleQaPointer()
        {
            if (selectedType == null || qaPointer == null) { world.HideGhost(); hud.HideCursorLabel(); return; }
            var ground = qaPointer.Value;
            var type = match.GetTowerType(selectedType);
            var reason = match.CheckPlacement(selectedType, new Vec2(ground.x, ground.z));
            bool ok = reason == RejectReason.None;
            world.ShowGhost(type, ground, ok);
            hud.SetCursorLabel(ok ? "Construir aquí (clic)" : "No se puede: " + ReasonText(reason, type), ok ? Palette.Valido : Palette.Invalido, rig.Camera.WorldToScreenPoint(ground));
        }

        string ReasonText(RejectReason r, TowerTypeData type)
        {
            switch (r)
            {
                case RejectReason.NotEnoughGold: return $"faltan {type.cost - match.Gold} de oro";
                case RejectReason.OutsideArea: return "fuera del área";
                case RejectReason.OnPath: return "sobre el camino";
                case RejectReason.Blocked: return "zona bloqueada";
                case RejectReason.Overlap: return "choca con otra torre";
                case RejectReason.TutorialRestricted: return "en el tutorial va la torre de arqueras";
                default: return "ahora no";
            }
        }

        // ------------------------------------------------------------------ selección, mejora, venta y recolección
        void SelectType(string id)
        {
            if (match == null || match.Ended || match.State == MatchState.Paused) return;
            if (!match.CanSelectTower(id))
            {
                if (match.State == MatchState.Tutorial || match.State == MatchState.Countdown) sfx.Play("rechazo", 0.7f);
                return;
            }
            CloseTowerPanel();
            selectedType = id;
            hud.SetSelectedCard(id);
            sfx.Play("click");
            match.NotifyTowerSelected(id);
            Flush();
        }

        void ClearSelection()
        {
            CancelPlacement();
            CloseTowerPanel();
        }

        void CancelPlacement()
        {
            selectedType = null;
            hud?.SetSelectedCard(null);
            world?.HideGhost();
            hud?.HideCursorLabel();
        }

        void SelectTower(int id)
        {
            selectedTower = id;
            sellArmed = false;
            upgradeHover = false;
            sfx.Play("click");
            RefreshTowerPanel();
        }

        void CloseTowerPanel()
        {
            selectedTower = 0;
            sellArmed = false;
            upgradeHover = false;
            hud?.HideTowerPanel();
            world?.ShowSelection(null, 0f);
            world?.ShowPreview(null, 0f);
        }

        void RefreshTowerPanel()
        {
            if (selectedTower == 0 || match == null) return;
            var t = match.GetTower(selectedTower);
            if (t == null) { CloseTowerPanel(); return; }
            hud.ShowTowerPanel(t, match, sellArmed);
            world.ShowSelection(t, t.Stats.range);
            RefreshPreview();
        }

        void RefreshPreview()
        {
            var t = selectedTower != 0 && match != null ? match.GetTower(selectedTower) : null;
            if (t != null && upgradeHover && t.CanUpgrade) world.ShowPreview(t, t.Type.levels[t.Level].range);
            else world.ShowPreview(null, 0f);
        }

        void Upgrade()
        {
            if (match == null || selectedTower == 0) return;
            match.TryUpgrade(selectedTower, out _);
            Flush();
            RefreshTowerPanel();
        }

        void Collect()
        {
            if (match == null || selectedTower == 0) return;
            var t = match.GetTower(selectedTower);
            if (t == null || t.Type.attack != AttackKind.Gold) return;
            match.TryCollect(selectedTower, out _, out _);
            Flush();
            RefreshTowerPanel();
        }

        void Sell()
        {
            if (match == null || selectedTower == 0) return;
            if (!sellArmed)
            {
                sellArmed = true; sellArmedAt = Time.unscaledTime;
                sfx.Play("click");
                RefreshTowerPanel();
                return;
            }
            sellArmed = false;
            match.TrySell(selectedTower, out _, out _);
            Flush();
        }
    }
}
