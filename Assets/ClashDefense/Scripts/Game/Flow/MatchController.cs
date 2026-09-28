using System;
using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClashDefense.Game
{
    /// <summary>
    /// La partida en curso (SOL-001, SOL-002): corre el reloj fijo del Match, reparte sus eventos a la vista, el HUD, el sonido
    /// y el registro, y traduce el mouse y los botones del HUD a comandos del Match (construir, mejorar, vender, recoger).
    /// No contiene reglas de juego: viven en ClashDefense.Core. Se separó de GameBootstrap en TL-003 (Vaultrum: riesgo de
    /// manager dios): el flujo entre pantallas lo lleva GameFlow.
    /// </summary>
    public sealed class MatchController
    {
        const float SellConfirmWindow = 2f;   // GDS-001.3, supuesto S8 (tiempo real)

        readonly GameServices s;
        readonly List<SimEvent> buffer = new List<SimEvent>();
        Match match;
        LevelData level;
        float acc;
        bool resultShown;
        string selectedType;
        int selectedTower;
        bool sellArmed; float sellArmedAt;
        bool upgradeHover;

        public int MaxStepsPerFrame = 8;
        public bool QaDriven;
        public Vector3? QaPointer;

        /// <summary>Lo arma el flujo: qué cuenta la pantalla de resultado de la campaña (null en el P0).</summary>
        public Func<CampaignResultInfo> Outcome;
        /// <summary>Empezó una oleada (el flujo marca el tutorial como completado).</summary>
        public event Action<int> WaveStarted;

        public Match Match => match;
        public string SelectedType => selectedType;
        public bool Running => match != null;

        public MatchController(GameServices services) { s = services; }

        // ------------------------------------------------------------------ ciclo
        public void Begin(Match m, string label, BalanceDefinition visuals, string campaignLevelId)
        {
            match = m;
            level = m.Level;
            s.World.SetLevel(level);
            s.World.Bind(match, visuals);
            s.Sfx.ResetMatch();
            var bosses = new HashSet<string>();
            foreach (var et in match.Balance.enemies) if (et != null && et.miniboss) bosses.Add(et.id);
            s.Sfx.IsMiniboss = bosses.Contains;
            s.Metrics.Begin(match, campaignLevelId);
            s.Hud.ShowGame(match, label, id => { var t = visuals != null ? visuals.Tower(id) : null; return t != null ? t.color : Palette.Bloqueo; });
            ClearSelection();
            resultShown = false;
            acc = 0f;
            Time.timeScale = 1f;
            match.Begin();
            Flush();
        }

        /// <summary>Si hay una partida sin terminar, se registra como abandono (MET-001.7: partida_terminada una vez, también en abandono).</summary>
        public void Close()
        {
            if (match != null && !match.Ended)
            {
                match.Abandon();
                Flush();
                s.Metrics.End();
            }
            match = null;
            s.World.Clear();
        }

        /// <summary>Al cerrar la aplicación.</summary>
        public void Quit()
        {
            if (match != null && !match.Ended) { match.Abandon(); Flush(); s.Metrics.End(); }
        }

        public void Pause()
        {
            if (match == null || !match.Pause()) return;
            Time.timeScale = 0f;
            s.World.HideGhost();
            s.Hud.HideCursorLabel();
            s.Hud.ShowPause(true);
            s.Sfx.Play("click");
            Flush();
        }

        public void Resume()
        {
            if (match == null || !match.Resume()) return;
            Time.timeScale = 1f;
            s.Hud.ShowPause(false);
            s.Sfx.Play("click");
            Flush();
        }

        public void TutorialNext() { if (match != null) { match.TutorialNext(); Flush(); } }

        // ------------------------------------------------------------------ cuadro a cuadro
        public void Tick()
        {
            if (match == null) return;
            if (match.State == MatchState.Paused) s.Metrics.AddPause(Time.unscaledDeltaTime);
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
                s.Metrics.End();
                s.Hud.ShowResult(s.Metrics.LastReport, match, s.Metrics.LastReportPath, Outcome?.Invoke());
            }
        }

        public void Flush()
        {
            if (match == null) return;
            buffer.Clear();
            match.DrainEvents(buffer);
            for (int i = 0; i < buffer.Count; i++) Dispatch(buffer[i]);
        }

        void Dispatch(SimEvent e)
        {
            s.World.OnEvent(e);
            s.Hud.OnEvent(e);
            s.Sfx.OnEvent(e);
            s.Metrics.OnEvent(e);
            switch (e.Type)
            {
                case SimEventType.TutorialStep: ApplyTutorialStep(e.Int1); break;
                case SimEventType.WaveStarted: WaveStarted?.Invoke(e.Int1); break;
                case SimEventType.StateChanged: if (match.State != MatchState.Tutorial) s.Hud.SetCardsInteractable(id => match.CanSelectTower(id)); break;
                case SimEventType.GoldChanged:
                case SimEventType.TowerUpgraded: RefreshTowerPanel(); break;
                case SimEventType.TowerSold: if (e.TowerId == selectedTower) CloseTowerPanel(); break;
            }
        }

        void ApplyTutorialStep(int step)
        {
            string towerId = level.tutorialTowerId;
            var type = match.GetTowerType(towerId);
            s.Hud.SetTutorialStep(step, towerId, type != null ? type.displayName : towerId, match.WaveCount, match.Gold);
            s.World.Highlight(step == 1 ? "base" : step == 2 ? "entrada" : step == 5 ? "pista" : null);
            if (step == 4 || step == 5) s.Hud.SetCardsInteractable(id => id == towerId);
            else if (step > 0) s.Hud.SetCardsInteractable(id => false);
            else s.Hud.SetCardsInteractable(null);
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
            s.Hud.SetHint(h);
        }

        bool HasGoldTower()
        {
            var towers = match.Towers;
            for (int i = 0; i < towers.Count; i++) if (towers[i].Type.attack == AttackKind.Gold) return true;
            return false;
        }

        // ------------------------------------------------------------------ mouse sobre el mapa
        void HandlePointer()
        {
            if (match == null || match.Ended || match.State == MatchState.Paused) return;
            if (s.Menus != null && s.Menus.Current != MenuScreen.None) return;
            if (QaDriven) { HandleQaPointer(); return; }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool haveGround = s.Rig.ScreenToGround(Input.mousePosition, out var ground);
            var pos = new Vec2(ground.x, ground.z);

            if (selectedType != null)
            {
                if (overUi || !haveGround) { s.World.HideGhost(); s.Hud.HideCursorLabel(); return; }
                var type = match.GetTowerType(selectedType);
                var reason = match.CheckPlacement(selectedType, pos);
                bool ok = reason == RejectReason.None;
                s.World.ShowGhost(type, ground, ok);
                s.Hud.SetCursorLabel(ok ? "Construir aquí (clic)" : "No se puede: " + ReasonText(reason, type), ok ? Palette.Valido : Palette.Invalido, Input.mousePosition);
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
                int id = s.World.PickTower(ground);
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

        /// <summary>Clic izquierdo en el punto del mundo, por el mismo camino que el mouse (piloto de QA).</summary>
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
            int id = world == Vector3.zero ? 0 : s.World.PickTower(world);
            if (id != 0) ClickTower(id); else CloseTowerPanel();
            return id != 0;
        }

        void HandleQaPointer()
        {
            if (selectedType == null || QaPointer == null) { s.World.HideGhost(); s.Hud.HideCursorLabel(); return; }
            var ground = QaPointer.Value;
            var type = match.GetTowerType(selectedType);
            var reason = match.CheckPlacement(selectedType, new Vec2(ground.x, ground.z));
            bool ok = reason == RejectReason.None;
            s.World.ShowGhost(type, ground, ok);
            s.Hud.SetCursorLabel(ok ? "Construir aquí (clic)" : "No se puede: " + ReasonText(reason, type), ok ? Palette.Valido : Palette.Invalido, s.Rig.Camera.WorldToScreenPoint(ground));
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
        public void SelectType(string id)
        {
            if (match == null || match.Ended || match.State == MatchState.Paused) return;
            if (!match.CanSelectTower(id))
            {
                if (match.State == MatchState.Tutorial || match.State == MatchState.Countdown) s.Sfx.Play("rechazo", 0.7f);
                return;
            }
            CloseTowerPanel();
            selectedType = id;
            s.Hud.SetSelectedCard(id);
            s.Sfx.Play("click");
            match.NotifyTowerSelected(id);
            Flush();
        }

        public void ClearSelection()
        {
            CancelPlacement();
            CloseTowerPanel();
        }

        public void CancelPlacement()
        {
            selectedType = null;
            s.Hud?.SetSelectedCard(null);
            s.World?.HideGhost();
            s.Hud?.HideCursorLabel();
        }

        public bool HasSelection => selectedType != null;
        public bool HasTowerSelected => selectedTower != 0;

        public void SelectTower(int id)
        {
            selectedTower = id;
            sellArmed = false;
            upgradeHover = false;
            s.Sfx.Play("click");
            RefreshTowerPanel();
        }

        public void CloseTowerPanel()
        {
            selectedTower = 0;
            sellArmed = false;
            upgradeHover = false;
            s.Hud?.HideTowerPanel();
            s.World?.ShowSelection(null, 0f);
            s.World?.ShowPreview(null, 0f);
        }

        void RefreshTowerPanel()
        {
            if (selectedTower == 0 || match == null) return;
            var t = match.GetTower(selectedTower);
            if (t == null) { CloseTowerPanel(); return; }
            s.Hud.ShowTowerPanel(t, match, sellArmed);
            s.World.ShowSelection(t, t.Stats.range);
            RefreshPreview();
        }

        public void SetUpgradeHover(bool on) { upgradeHover = on; RefreshPreview(); }

        void RefreshPreview()
        {
            var t = selectedTower != 0 && match != null ? match.GetTower(selectedTower) : null;
            if (t != null && upgradeHover && t.CanUpgrade) s.World.ShowPreview(t, t.Type.levels[t.Level].range);
            else s.World.ShowPreview(null, 0f);
        }

        public void Upgrade()
        {
            if (match == null || selectedTower == 0) return;
            match.TryUpgrade(selectedTower, out _);
            Flush();
            RefreshTowerPanel();
        }

        public void Collect()
        {
            if (match == null || selectedTower == 0) return;
            var t = match.GetTower(selectedTower);
            if (t == null || t.Type.attack != AttackKind.Gold) return;
            match.TryCollect(selectedTower, out _, out _);
            Flush();
            RefreshTowerPanel();
        }

        public void Sell()
        {
            if (match == null || selectedTower == 0) return;
            if (!sellArmed)
            {
                sellArmed = true; sellArmedAt = Time.unscaledTime;
                s.Sfx.Play("click");
                RefreshTowerPanel();
                return;
            }
            sellArmed = false;
            match.TrySell(selectedTower, out _, out _);
            Flush();
        }
    }
}
