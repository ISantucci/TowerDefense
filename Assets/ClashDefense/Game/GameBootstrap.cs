using System;
using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClashDefense.Game
{
    /// <summary>
    /// Dueño del ciclo de la aplicación (SOL-001): carga y valida los datos, arma cámara, mundo, HUD, sonido y registro,
    /// corre el reloj fijo del Match y traduce la entrada del jugador a comandos del Match.
    /// No contiene reglas de juego: todas viven en ClashDefense.Core.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Datos (RQ-001.8)")]
        [SerializeField] TextAsset balanceJson = null;
        [SerializeField] TextAsset levelJson = null;
        [Header("Materiales (los crea el builder de la escena)")]
        [SerializeField] Material solidMaterial = null;
        [SerializeField] Material fadeMaterial = null;
        [SerializeField] Material lineMaterial = null;

        const float SellConfirmWindow = 2f;   // GDS-001.3, supuesto S8 (tiempo real)
        const int MaxStepsPerFrame = 8;

        static bool tutorialShownThisSession;

        BalanceData balance;
        LevelData level;
        Visuals vis;
        CameraRig rig;
        WorldView world;
        Hud hud;
        Sfx sfx;
        MetricsWriter metrics;

        Match match;
        float acc;
        bool resultShown;
        readonly List<SimEvent> buffer = new List<SimEvent>();

        string selectedType;
        int selectedTower;
        bool sellArmed; float sellArmedAt;
        bool upgradeHover;

        public Match CurrentMatch => match;

        // ------------------------------------------------------------------ arranque
        void Awake()
        {
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var errors = new List<string>();
            try
            {
                balance = balanceJson != null ? JsonUtility.FromJson<BalanceData>(balanceJson.text) : null;
                level = levelJson != null ? JsonUtility.FromJson<LevelData>(levelJson.text) : null;
            }
            catch (Exception ex) { errors.Add("JSON ilegible: " + ex.Message); }
            if (balanceJson == null) errors.Add("falta balance_p0.json en GameBootstrap");
            if (levelJson == null) errors.Add("falta level_p0.json en GameBootstrap");
            if (errors.Count == 0) errors.AddRange(DataValidator.Validate(balance, level));

            var cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }

            vis = new Visuals(solidMaterial, fadeMaterial, lineMaterial);
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(errors.Count == 0 ? balance : new BalanceData { towers = new TowerTypeData[0] });
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
            rig = new CameraRig(cam, WorldView.ContentBounds(level));
            world.BuildMap(vis, level, cam);
            ShowStart();
        }

        void WireHud()
        {
            hud.PlayClicked += t => { sfx.Play("click"); StartMatch(t); };
            hud.QuitClicked += Quit;
            hud.PauseClicked += () => { if (match != null && match.State != MatchState.Paused) Pause(); };
            hud.ResumeClicked += Resume;
            hud.RestartClicked += Restart;
            hud.ExitClicked += ExitToStart;
            hud.RetryClicked += Restart;
            hud.TutorialNextClicked += () => { sfx.Play("click"); match?.TutorialNext(); };
            hud.CardClicked += SelectType;
            hud.UpgradeClicked += Upgrade;
            hud.SellClicked += Sell;
            hud.UpgradeHover += on => { upgradeHover = on; RefreshPreview(); };
            hud.VolumeChanged += v => { sfx.Volume = v; PlayerPrefs.SetFloat("cd_volumen", v); };
            hud.MuteChanged += m => { sfx.Muted = m; PlayerPrefs.SetInt("cd_silencio", m ? 1 : 0); };
        }

        void ShowStart()
        {
            Time.timeScale = 1f;
            hud.ShowStart(!tutorialShownThisSession, $"balance {balance.version} · nivel {level.id} · build {Application.version} · registro en {metrics.Folder}");
        }

        // ------------------------------------------------------------------ flujo de partida (GDS-001.6)
        void StartMatch(bool tutorial)
        {
            CloseCurrent();
            match = new Match(balance, level, tutorial);
            if (tutorial) tutorialShownThisSession = true;
            world.Bind(match);
            metrics.Begin(match);
            hud.ShowGame(match);
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

        void Restart() { sfx.Play("click"); StartMatch(false); }

        void ExitToStart()
        {
            sfx.Play("click");
            CloseCurrent();
            ClearSelection();
            ShowStart();
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

            if (sellArmed && Time.unscaledTime - sellArmedAt > SellConfirmWindow) { sellArmed = false; RefreshTowerPanel(); }

            if (match.Ended && !resultShown)
            {
                resultShown = true;
                ClearSelection();
                metrics.End();
                hud.ShowResult(metrics.LastReport, match, metrics.LastReportPath);
            }
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
                case SimEventType.GoldChanged:
                case SimEventType.TowerUpgraded: RefreshTowerPanel(); break;
                case SimEventType.TowerSold: if (e.TowerId == selectedTower) CloseTowerPanel(); break;
            }
        }

        void ApplyTutorialStep(int step)
        {
            string towerId = level.tutorialTowerId;
            var type = match.GetTowerType(towerId);
            hud.SetTutorialStep(step, towerId, type != null ? type.displayName : towerId, match.WaveCount, balance.economy.startGold);
            world.Highlight(step == 1 ? "base" : step == 2 ? "entrada" : step == 5 ? "pista" : null);
            if (step == 4 || step == 5) hud.SetCardsInteractable(id => id == towerId);
            else if (step > 0) hud.SetCardsInteractable(id => false);
            else hud.SetCardsInteractable(null);
        }

        // ------------------------------------------------------------------ teclado (UXS-001.6: una tecla, un verbo)
        void HandleKeys()
        {
            if (hud == null) return;
            if (hud.ErrorVisible) { if (Input.GetKeyDown(KeyCode.S)) Quit(); return; }
            if (hud.StartVisible)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { sfx.Play("click"); StartMatch(hud.StartTutorialChecked); }
                else if (Input.GetKeyDown(KeyCode.S) && Application.platform != RuntimePlatform.WebGLPlayer) Quit();
                return;
            }
            if (hud.ResultVisible)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Restart();
                else if (Input.GetKeyDown(KeyCode.Escape)) ExitToStart();
                return;
            }
            if (match == null) return;

            if (match.State == MatchState.Paused)
            {
                if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) Resume();
                else if (Input.GetKeyDown(KeyCode.R)) Restart();
                else if (Input.GetKeyDown(KeyCode.S)) ExitToStart();
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
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && match.State == MatchState.Tutorial && hud.TutorialStepNeedsButton)
            {
                sfx.Play("click");
                match.TutorialNext();
                return;
            }
            for (int i = 0; i < balance.towers.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { SelectType(balance.towers[i].id); return; }
            if (Input.GetKeyDown(KeyCode.U)) Upgrade();
            if (Input.GetKeyDown(KeyCode.V)) Sell();
        }

        // ------------------------------------------------------------------ mouse sobre el mapa
        void HandlePointer()
        {
            if (match == null || match.Ended || match.State == MatchState.Paused) return;
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
                if (id != 0) SelectTower(id); else CloseTowerPanel();
            }
            else if (Input.GetMouseButtonDown(1)) CloseTowerPanel();
        }

        // ------------------------------------------------------------------ API de QA
        // La usa QaAutopilot (solo en el editor). Recorre los mismos caminos que la entrada del jugador,
        // salvo el mouse físico: el puntero se fija en un punto del mundo.
        bool qaDriven;
        Vector3? qaPointer;

        public Hud QaHud => hud;
        public WorldView QaWorld => world;
        public MetricsWriter QaMetrics => metrics;
        public string QaSelectedType => selectedType;
        public void QaTakeControl() { qaDriven = true; }
        public void QaStart(bool tutorial) => StartMatch(tutorial);
        public void QaSelectType(string id) => SelectType(id);
        public void QaPointerAt(Vector3? world) => qaPointer = world;
        public void QaSelectTower(int id) => SelectTower(id);
        public void QaUpgrade() => Upgrade();
        public void QaSell() => Sell();
        public void QaPause() => Pause();
        public void QaResume() => Resume();
        public void QaRestart() => Restart();
        public void QaExitToStart() => ExitToStart();
        public void QaTutorialNext() { if (match != null) { match.TutorialNext(); Flush(); } }
        public void QaUpgradeHover(bool on) { upgradeHover = on; hud.ShowUpgradePreview(on); RefreshPreview(); }

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
            if (id != 0) SelectTower(id); else CloseTowerPanel();
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

        // ------------------------------------------------------------------ selección, mejora y venta
        void SelectType(string id)
        {
            if (match == null || match.Ended || match.State == MatchState.Paused) return;
            bool tutorial = match.State == MatchState.Tutorial;
            if (tutorial && !(match.Tutorial == TutorialStep.SelectTower || match.Tutorial == TutorialStep.PlaceTower)) return;
            if (tutorial && id != level.tutorialTowerId) { sfx.Play("rechazo", 0.7f); return; }
            if (!tutorial && match.State != MatchState.Countdown && match.State != MatchState.Wave && match.State != MatchState.Interval) return;
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
            hud.SetSelectedCard(null);
            world.HideGhost();
            hud.HideCursorLabel();
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
            hud.HideTowerPanel();
            world.ShowSelection(null, 0f);
            world.ShowPreview(null, 0f);
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
