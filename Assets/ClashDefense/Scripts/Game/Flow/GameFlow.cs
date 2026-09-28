using System;
using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// El flujo entre pantallas (UXS-002.1 … .3, GDS-002.2): aviso, inicio, mapa, tienda, opciones, tanda, partidas de la
    /// campaña y el Laboratorio; la progresión y su guardado; y el teclado (una tecla, un verbo). Las reglas de la progresión
    /// viven en Progression (núcleo); las de la partida, en Match. Se separó de GameBootstrap en TL-003.
    /// </summary>
    public sealed class GameFlow
    {
        readonly GameServices s;
        readonly MatchController mc;
        readonly GameConfig config;
        readonly Dictionary<string, LevelDefinition> levels = new Dictionary<string, LevelDefinition>();
        CampaignData campaignData;
        BalanceData campaignBalance, labBalance;
        LevelData labLevel;
        Progression prog;
        string currentLevelId;     // null = P0 / Laboratorio
        bool labMode;
        bool levelTutorial;        // con qué se empezó el nivel actual: reintentar respeta la elección (revisión técnica, hallazgo 3)
        string saveKey = SaveStore.DefaultKey;   // el guardado queda atado a su clave: el piloto nunca escribe en la del owner (hallazgo 1)
        bool testMode;             // Play desde la escena de un nivel (TL-003): no se guarda nada
        static bool tutorialShownThisSession;

        bool Campaign => campaignData != null;
        public Progression Progression => prog;
        public string CurrentLevelId => currentLevelId;
        public bool IsCampaign => Campaign;

        public GameFlow(GameServices services, MatchController match, GameConfig cfg)
        {
            s = services; mc = match; config = cfg;
            mc.Outcome = CampaignOutcome;
            mc.WaveStarted += OnWaveStarted;
        }

        // ------------------------------------------------------------------ datos
        /// <summary>Carga y valida los datos del juego. Devuelve los errores, en palabras.</summary>
        public List<string> Load()
        {
            var errors = new List<string>();
            if (config.labBalance == null) errors.Add("GameConfig: falta el balance del Laboratorio");
            if (config.labLevel == null) errors.Add("GameConfig: falta el nivel del Laboratorio");
            if (config.presentation == null) errors.Add("GameConfig: falta la biblioteca de presentación");
            if (errors.Count > 0) return errors;
            labBalance = config.labBalance.ToCore();
            labLevel = config.labLevel.ToCore();
            errors.AddRange(DataValidator.Validate(labBalance, labLevel));
            errors.AddRange(VisualErrors(config.labBalance));
            if (config.campaign == null) return errors;
            var c = config.campaign;
            if (c.balance == null) { errors.Add("campaña: falta el balance"); return errors; }
            campaignData = c.ToCore();
            campaignBalance = c.balance.ToCore();
            errors.AddRange(DataValidator.ValidateBalance(campaignBalance));
            errors.AddRange(Progression.Validate(campaignData, campaignBalance));
            errors.AddRange(VisualErrors(c.balance));
            foreach (var w in c.worlds)
                foreach (var ct in w.continents)
                    foreach (var l in ct.levels)
                    {
                        if (!l.available || !ct.available) continue;
                        if (l.level == null) { errors.Add($"campaña: el nivel {l.number} de {ct.displayName} no tiene datos"); continue; }
                        levels[l.level.id] = l.level;
                        if (l.level.Shape == null || l.level.Shape.IsEmpty) { errors.Add($"nivel {l.level.id}: la escena no está horneada (abrila y guardala)"); continue; }
                        errors.AddRange(DataValidator.ValidateLevel(campaignBalance, l.level.ToCore()));
                    }
            return errors;
        }

        static IEnumerable<string> VisualErrors(BalanceDefinition b)
        {
            foreach (var t in b.towers) if (t != null && t.prefab == null) yield return $"la torre '{t.Id}' no tiene prefab";
            foreach (var e in b.enemies) if (e != null && e.prefab == null) yield return $"el enemigo '{e.Id}' no tiene prefab";
        }

        public BalanceDefinition CampaignVisuals => config.campaign != null ? config.campaign.balance : null;

        // ------------------------------------------------------------------ arranque
        public void Start(LevelDefinition testLevel, UnityEngine.SceneManagement.Scene testScene)
        {
            WireHud();
            if (Campaign)
            {
                prog = new Progression(campaignData, SaveStore.Load(saveKey));
                s.Menus.Setup(prog, config.campaign, config.version, s.Metrics.Folder);
                s.Menus.SetLevelTutorial(!prog.Save.tutorialDone || prog.Save.tutorialRepeat);
                WireMenus();
            }
            if (testLevel != null) { StartTest(testLevel, testScene); return; }
            if (Campaign)
            {
                ShowBackdrop(FirstLevelId());
                if (!prog.Save.noticeAccepted) s.Menus.ShowNotice(); else s.Menus.ShowTitle();
            }
            else
            {
                ShowLevel(config.labLevel, labLevel);
                ShowStart();
            }
        }

        /// <summary>Play desde la escena de un nivel (TL-003): arranca directo en ese nivel con las torres que tendría a esa altura
        /// de la campaña, con un guardado de prueba que no se escribe. El Laboratorio arranca como siempre.</summary>
        void StartTest(LevelDefinition def, UnityEngine.SceneManagement.Scene scene)
        {
            s.Stage.AdoptOpen(def, scene);
            if (def == config.labLevel || !Campaign || !levels.ContainsKey(def.id))
            {
                s.Fit(def == config.labLevel ? labLevel : def.ToCore());
                if (Campaign) { labMode = true; s.Hud.SetExitLabel("Salir al laboratorio  [S]"); s.Hud.ResultExitLabel = "Volver al laboratorio  [Esc]"; }
                ShowStart();
                return;
            }
            testMode = true;
            saveKey = "cd_guardado_prueba_editor";
            // las partidas de prueba no se mezclan con el registro de las partidas de verdad (MET-002.7)
            s.Metrics.SetFolder(System.IO.Path.Combine(Application.persistentDataPath, "metricas_prueba_editor"));
            prog = new Progression(campaignData, new SaveData { noticeAccepted = true, tutorialDone = true });
            s.Menus.Rebind(prog);
            foreach (var l in prog.AllLevels()) { if (l.id == def.id) break; if (l.available) prog.ApplyVictory(l.id, 2, 60); }
            prog.Save.tandaPending = 0;
            StartLevel(def.id, false);
            s.Hud.ShowToast($"Prueba del nivel {def.id} desde su escena: el progreso no se guarda.", 5f, true);
        }

        void SaveNow() { if (prog != null && !testMode) SaveStore.Save(prog.Save, saveKey); }

        string FirstLevelId()
        {
            foreach (var l in prog.AllLevels()) if (l.available) return l.id;
            return null;
        }

        void ShowLevel(LevelDefinition def, LevelData data)
        {
            s.World.SetLevel(data);
            s.Stage.Show(def);
            s.Fit(data);
        }

        /// <summary>El mapa del nivel elegido queda de fondo detrás de los menús (UXS-002.1).</summary>
        void ShowBackdrop(string levelId)
        {
            if (levelId != null && levels.TryGetValue(levelId, out var def)) ShowLevel(def, def.ToCore());
            else ShowLevel(config.labLevel, labLevel);
        }

        // ------------------------------------------------------------------ cableado
        void WireHud()
        {
            var hud = s.Hud;
            hud.PlayClicked += t => { s.Sfx.Play("click"); StartP0(t); };
            hud.QuitClicked += () => { if (Campaign && labMode) { s.Sfx.Play("click"); ShowTitle(); } else Quit(); };
            hud.PauseClicked += () => { if (mc.Match != null && mc.Match.State != MatchState.Paused) mc.Pause(); };
            hud.ResumeClicked += mc.Resume;
            hud.RestartClicked += Restart;
            hud.ExitClicked += ExitFromMatch;
            hud.RetryClicked += Restart;
            hud.NextClicked += NextLevel;
            hud.TandaClicked += () => { s.Sfx.Play("click"); s.Menus?.ShowTanda(); };
            hud.TutorialNextClicked += () => { s.Sfx.Play("click"); mc.Match?.TutorialNext(); };
            hud.CardClicked += mc.SelectType;
            hud.UpgradeClicked += mc.Upgrade;
            hud.SellClicked += mc.Sell;
            hud.CollectClicked += mc.Collect;
            hud.UpgradeHover += mc.SetUpgradeHover;
            hud.VolumeChanged += SetVolume;
            hud.MuteChanged += SetMuted;
        }

        void WireMenus()
        {
            var menus = s.Menus;
            menus.PlayClicked += () => { s.Sfx.Play("click"); ShowMap(null); };
            menus.ShopClicked += () => { s.Sfx.Play("click"); menus.ShowShop(menus.Current == MenuScreen.Map ? MenuScreen.Map : MenuScreen.Title); };
            menus.OptionsClicked += () => { s.Sfx.Play("click"); menus.ShowOptions(MenuScreen.Title, s.Sfx.Volume, s.Sfx.Muted); };
            menus.LabClicked += () => { s.Sfx.Play("click"); OpenLab(); };
            menus.QuitClicked += Quit;
            menus.NoticeAccepted += () => { s.Sfx.Play("click"); AcceptNotice(); };
            menus.BackClicked += OnMenuBack;
            menus.LevelSelected += id => { s.Sfx.Play("click"); ShowBackdrop(id); };
            menus.LevelLocked += id => s.Sfx.Play("rechazo", 0.7f);
            menus.BuyRejected += id => s.Sfx.Play("rechazo", 0.7f);
            menus.LevelPlay += (id, tut) => { s.Sfx.Play("click"); StartLevel(id, tut); };
            menus.BuyClicked += Buy;
            menus.TandaChosen += ChooseTanda;
            menus.VolumeChanged += SetVolume;
            menus.MuteChanged += SetMuted;
            menus.TutorialRepeatChanged += v => { prog.Save.tutorialRepeat = v; SaveNow(); menus.SetLevelTutorial(!prog.Save.tutorialDone || v); };
            menus.ResetProgress += ResetProgress;
        }

        void SetVolume(float v) { s.Sfx.Volume = v; PlayerPrefs.SetFloat("cd_volumen", v); PlayerPrefs.Save(); s.Hud.SyncAudio(v, s.Sfx.Muted); }
        void SetMuted(bool m) { s.Sfx.Muted = m; PlayerPrefs.SetInt("cd_silencio", m ? 1 : 0); PlayerPrefs.Save(); s.Hud.SyncAudio(s.Sfx.Volume, m); }

        public void AcceptNotice() { prog.Save.noticeAccepted = true; SaveNow(); s.Menus.ShowTitle(); }

        // ------------------------------------------------------------------ navegación de la campaña (UXS-002.1, .2)
        public void ShowTitle()
        {
            mc.Close();
            mc.ClearSelection();
            labMode = false;
            Time.timeScale = 1f;
            s.Hud.HideAll();
            ShowBackdrop(s.Menus.SelectedLevel ?? FirstLevelId());
            // después de borrar el progreso el aviso de datos vuelve a aparecer, como en una instalación nueva (RQ-002.1 CA5)
            if (!prog.Save.noticeAccepted) s.Menus.ShowNotice(); else s.Menus.ShowTitle();
        }

        public void ShowMap(string select)
        {
            mc.Close();
            mc.ClearSelection();
            labMode = false;
            Time.timeScale = 1f;
            s.Hud.HideAll();
            s.Menus.ShowMap(select);
            ShowBackdrop(s.Menus.SelectedLevel);
            int tab = s.Menus.WorldTab;
            if (tab > 0 && prog.IsWorldUnlocked(prog.Campaign.worlds[tab]) && !prog.WorldSeen(prog.Campaign.worlds[tab].id))
            { prog.MarkWorldSeen(prog.Campaign.worlds[tab].id); SaveNow(); }
        }

        void OnMenuBack(MenuScreen from)
        {
            s.Sfx.Play("click");
            switch (from)
            {
                case MenuScreen.Map: ShowTitle(); break;
                case MenuScreen.Shop: if (s.Menus.ShopReturn == MenuScreen.Map) ShowMap(null); else ShowTitle(); break;
                case MenuScreen.Options: ShowTitle(); break;
                case MenuScreen.Tanda:
                    if (s.Hud.ResultVisible) s.Menus.HideAll();
                    else ShowMap(null);
                    break;
                default: ShowTitle(); break;
            }
        }

        public void OpenLab()
        {
            labMode = true;
            s.Menus.HideAll();
            mc.Close();
            ShowLevel(config.labLevel, labLevel);
            s.Hud.SetExitLabel("Salir al laboratorio  [S]");
            s.Hud.ResultExitLabel = "Volver al laboratorio  [Esc]";
            ShowStart();
        }

        public void Buy(string id)
        {
            if (!prog.Buy(id)) { s.Sfx.Play("rechazo", 0.7f); s.Menus.RebuildShop(); return; }
            var item = prog.FindShopItem(id);
            s.Sfx.Play("compra");
            SaveNow();
            s.Metrics.CampaignEvent("compra_tienda", ("item", id), ("tower_type", item.tower), ("cost", item.cost.ToString()), ("currency_left", prog.Currency.ToString()));
            s.Menus.RebuildShop();
        }

        public void ChooseTanda(string towerId)
        {
            if (!prog.ChooseTanda(towerId)) { s.Sfx.Play("rechazo", 0.7f); return; }
            s.Sfx.Play("mejorar");
            SaveNow();
            s.Metrics.CampaignEvent("mejora_tanda_elegida", ("tower_type", towerId), ("tier", prog.TandaTier(towerId).ToString()));
            if (prog.Save.tandaPending > 0) { s.Menus.ShowTanda(); return; }
            if (s.Hud.ResultVisible) { s.Menus.HideAll(); s.Hud.HideTandaButton(); }
            else ShowMap(null);
        }

        public void ResetProgress()
        {
            SaveStore.Reset(saveKey);
            prog = new Progression(campaignData, new SaveData());
            s.Menus.Rebind(prog);
            s.Menus.SetLevelTutorial(true);
            s.Sfx.Play("vender");
            s.Menus.ShowOptions(MenuScreen.Title, s.Sfx.Volume, s.Sfx.Muted);
            s.Menus.ShowResetDone();
        }

        /// <summary>El piloto juega con un guardado propio (y opcionalmente vacío): nunca toca el progreso del owner.</summary>
        public void UseSaveKey(string key, bool fresh)
        {
            SaveStore.Key = key;
            saveKey = key;
            if (fresh) SaveStore.Reset(key);
            prog = new Progression(campaignData, SaveStore.Load(key));
            s.Menus.Rebind(prog);
            s.Menus.SetLevelTutorial(!prog.Save.tutorialDone || prog.Save.tutorialRepeat);
            if (!prog.Save.noticeAccepted) { mc.Close(); s.Hud.HideAll(); s.Menus.ShowNotice(); } else ShowTitle();
        }

        // ------------------------------------------------------------------ partidas (GDS-001.6, GDS-002.2)
        public void StartLevel(string levelId, bool tutorial)
        {
            var lm = prog.FindLevel(levelId);
            if (lm == null || !prog.IsLevelUnlocked(levelId) || !levels.TryGetValue(levelId, out var def)) { s.Sfx.Play("rechazo", 0.7f); return; }
            mc.Close();
            s.Menus.HideAll();
            labMode = false;
            currentLevelId = levelId;
            var ld = def.ToCore();
            ShowLevel(def, ld);
            levelTutorial = tutorial;
            var options = prog.Loadout(tutorial && !string.IsNullOrEmpty(ld.tutorialTowerId));
            prog.ApplyStart(levelId);
            SaveNow();
            var world0 = prog.WorldOf(levelId);
            mc.Begin(new Match(campaignBalance, ld, options), $"{world0.displayName} · Nivel {lm.number} — {lm.displayName}", config.campaign.balance, levelId);
            s.Hud.SetExitLabel("Salir al mapa  [S]");
            AnnounceNewTowers();
        }

        /// <summary>Mensaje de desbloqueo (Doc 03 §26 UX/UI): la primera vez que una torre está disponible en una partida.</summary>
        void AnnounceNewTowers()
        {
            bool first = prog.Save.seenTowers.Length == 0;
            foreach (var t in mc.Match.TowerTypes)
            {
                if (prog.TowerSeen(t.id)) continue;
                prog.MarkTowerSeen(t.id);
                if (!first) { s.Hud.ShowToast($"Nueva torre: {t.displayName} — {t.description}", 6f); s.Sfx.Play("desbloqueo"); }
            }
            SaveNow();
        }

        public void StartP0(bool tutorial)
        {
            mc.Close();
            currentLevelId = null;
            if (tutorial) tutorialShownThisSession = true;
            ShowLevel(config.labLevel, labLevel);
            mc.Begin(new Match(DataCopy.Clone(labBalance), config.labLevel.ToCore(), tutorial), null, config.labBalance, null);
            if (Campaign) s.Hud.SetExitLabel("Salir al laboratorio  [S]");
        }

        public void Restart()
        {
            s.Sfx.Play("click");
            if (currentLevelId != null) StartLevel(currentLevelId, (levelTutorial && !prog.Save.tutorialDone) || prog.Save.tutorialRepeat);
            else StartP0(false);
        }

        public void NextLevel()
        {
            s.Sfx.Play("click");
            var next = currentLevelId != null ? prog.NextLevel(currentLevelId) : null;
            if (next != null && prog.IsLevelUnlocked(next.id)) StartLevel(next.id, false);
            else ExitFromMatch();
        }

        public void ExitFromMatch()
        {
            s.Sfx.Play("click");
            if (Campaign && !labMode && currentLevelId != null) { var id = currentLevelId; ShowMap(id); return; }
            mc.Close();
            mc.ClearSelection();
            if (Campaign && labMode) { ShowLevel(config.labLevel, labLevel); ShowStart(); return; }
            ShowStart();
        }

        void ShowStart()
        {
            Time.timeScale = 1f;
            s.Hud.ShowStart(!tutorialShownThisSession, $"balance {labBalance.version} · nivel {labLevel.id} · build {Application.version} · registro en {s.Metrics.Folder}",
                Campaign ? "Volver al inicio  [S]" : "Salir  [S]", Campaign ? "Laboratorio: el nivel de prueba del núcleo (Doc 05). No da estrellas ni cristales." : null, Campaign);
        }

        public void Quit()
        {
            mc.Close();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OnApplicationQuit()
        {
            mc.Quit();
            SaveNow();
        }

        void OnWaveStarted(int wave)
        {
            // el tutorial se completa al llegar a la oleada 1 con él: desde ahí el nivel 1 ya no lo ofrece solo (GDS-002.2, S20)
            if (Campaign && !labMode && wave == 1 && mc.Match.TutorialPlayed && prog != null && !prog.Save.tutorialDone)
            { prog.Save.tutorialDone = true; SaveNow(); s.Menus.SetLevelTutorial(prog.Save.tutorialRepeat); }
        }

        /// <summary>Aplica la victoria a la progresión y arma lo que la pantalla de resultado cuenta (Doc 03 §22).</summary>
        CampaignResultInfo CampaignOutcome()
        {
            if (!Campaign || labMode || currentLevelId == null) return null;
            var match = mc.Match;
            var lm = prog.FindLevel(currentLevelId);
            bool win = match.State == MatchState.Victory;
            var info = new CampaignResultInfo { LevelName = lm.displayName, Victory = win, CurrencyName = prog.CurrencyName };
            if (win)
            {
                var r = prog.ApplyVictory(currentLevelId, match.Stars, match.BaseHp);
                if (match.TutorialPlayed) prog.Save.tutorialDone = true;
                s.Menus.SetLevelTutorial(!prog.Save.tutorialDone || prog.Save.tutorialRepeat);
                SaveNow();
                info.Reward = r;
                if (!string.IsNullOrEmpty(r.TowerUnlocked))
                {
                    var t = Array.Find(campaignBalance.towers, x => x.id == r.TowerUnlocked);
                    info.TowerName = t != null ? t.displayName : r.TowerUnlocked;
                    info.TowerDescription = t != null ? t.description : "";
                    s.Sfx.Play("desbloqueo");
                }
                if (!string.IsNullOrEmpty(r.WorldUnlocked))
                {
                    var w = Array.Find(prog.Campaign.worlds, x => x.id == r.WorldUnlocked);
                    info.WorldName = w != null ? w.displayName : r.WorldUnlocked;
                    info.FollowText = prog.Campaign.followText;
                }
                s.Metrics.CampaignEvent("progreso_recompensa", ("level_id", currentLevelId), ("stars", match.Stars.ToString()), ("currency", r.Currency.ToString()),
                    ("first_clear", r.FirstClear ? "1" : "0"), ("tower_unlocked", r.TowerUnlocked ?? ""), ("tanda_earned", r.TandaEarned ? "1" : "0"), ("world_unlocked", r.WorldUnlocked ?? ""));
            }
            var next = prog.NextLevel(currentLevelId);
            info.HasNext = next != null && prog.IsLevelUnlocked(next.id);
            info.NextName = next?.displayName;
            info.CurrencyTotal = prog.Currency;
            info.TandaPending = prog.Save.tandaPending;
            return info;
        }

        // ------------------------------------------------------------------ teclado (UXS-001.6, UXS-002.1: una tecla, un verbo)
        static readonly KeyCode[] CardKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Equals };
        static readonly KeyCode[] PadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.Keypad0, KeyCode.KeypadMinus, KeyCode.KeypadPlus };

        static bool Enter() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        public void HandleKeys()
        {
            var hud = s.Hud;
            if (hud == null) return;
            if (hud.ErrorVisible) { if (Input.GetKeyDown(KeyCode.S)) Quit(); return; }
            if (s.Menus != null && s.Menus.Current != MenuScreen.None) { HandleMenuKeys(); return; }
            if (hud.StartVisible)
            {
                if (Enter()) { s.Sfx.Play("click"); StartP0(hud.StartTutorialChecked); }
                else if (Input.GetKeyDown(KeyCode.S) && (Campaign || Application.platform != RuntimePlatform.WebGLPlayer)) { if (Campaign) { s.Sfx.Play("click"); ShowTitle(); } else Quit(); }
                else if (Input.GetKeyDown(KeyCode.Escape) && Campaign) { s.Sfx.Play("click"); ShowTitle(); }
                return;
            }
            if (hud.ResultVisible)
            {
                if (Enter()) { if (hud.CampaignResult && hud.ResultHasNext) NextLevel(); else Restart(); }
                else if (Input.GetKeyDown(KeyCode.R) && hud.CampaignResult) Restart();
                else if (Input.GetKeyDown(KeyCode.M) && hud.TandaButtonVisible) { s.Sfx.Play("click"); s.Menus.ShowTanda(); }
                else if (Input.GetKeyDown(KeyCode.Escape)) ExitFromMatch();
                return;
            }
            var match = mc.Match;
            if (match == null) return;

            if (match.State == MatchState.Paused)
            {
                if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) mc.Resume();
                else if (Input.GetKeyDown(KeyCode.R)) Restart();
                else if (Input.GetKeyDown(KeyCode.S)) ExitFromMatch();
                return;
            }
            if (match.Ended) return;

            if (Input.GetKeyDown(KeyCode.P)) { mc.Pause(); return; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (mc.HasSelection) mc.CancelPlacement();
                else if (mc.HasTowerSelected) mc.CloseTowerPanel();
                else mc.Pause();
                return;
            }
            if (Enter() && match.State == MatchState.Tutorial && hud.TutorialStepNeedsButton)
            {
                s.Sfx.Play("click");
                mc.TutorialNext();
                return;
            }
            var types = match.TowerTypes;
            for (int i = 0; i < types.Count && i < CardKeys.Length; i++)
                if (Input.GetKeyDown(CardKeys[i]) || Input.GetKeyDown(PadKeys[i])) { mc.SelectType(types[i].id); return; }
            if (Input.GetKeyDown(KeyCode.U)) mc.Upgrade();
            if (Input.GetKeyDown(KeyCode.V)) mc.Sell();
            if (Input.GetKeyDown(KeyCode.C)) mc.Collect();
        }

        void HandleMenuKeys()
        {
            var menus = s.Menus;
            switch (menus.Current)
            {
                case MenuScreen.Notice:
                    if (Enter()) { s.Sfx.Play("click"); AcceptNotice(); }
                    break;
                case MenuScreen.Title:
                    if (Enter()) { s.Sfx.Play("click"); ShowMap(null); }
                    else if (Input.GetKeyDown(KeyCode.T)) { s.Sfx.Play("click"); menus.ShowShop(MenuScreen.Title); }
                    else if (Input.GetKeyDown(KeyCode.O)) { s.Sfx.Play("click"); menus.ShowOptions(MenuScreen.Title, s.Sfx.Volume, s.Sfx.Muted); }
                    else if (Input.GetKeyDown(KeyCode.L)) { s.Sfx.Play("click"); OpenLab(); }
                    else if (Input.GetKeyDown(KeyCode.S) && Application.platform != RuntimePlatform.WebGLPlayer) Quit();
                    break;
                case MenuScreen.Map:
                    if (Enter() && menus.SelectedLevel != null) { s.Sfx.Play("click"); StartLevel(menus.SelectedLevel, menus.LevelTutorialChecked); }
                    else if (Input.GetKeyDown(KeyCode.Escape)) OnMenuBack(MenuScreen.Map);
                    else if (Input.GetKeyDown(KeyCode.T)) { s.Sfx.Play("click"); menus.ShowShop(MenuScreen.Map); }
                    else if (Input.GetKeyDown(KeyCode.M) && prog.Save.tandaPending > 0) { s.Sfx.Play("click"); menus.ShowTanda(); }
                    else if (Input.GetKeyDown(KeyCode.Tab)) { s.Sfx.Play("click"); menus.SetWorldTab((menus.WorldTab + 1) % prog.Campaign.worlds.Length); }
                    else if (Input.GetKeyDown(KeyCode.Y)) { s.Sfx.Play("click"); menus.SetLevelTutorial(!menus.LevelTutorialChecked); }
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
    }
}
