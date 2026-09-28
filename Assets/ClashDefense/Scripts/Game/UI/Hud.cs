using System;
using System.Collections.Generic;
using System.Text;
using ClashDefense.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Lo que la pantalla de resultado de la campaña tiene que contar (UXS-002.3). null = resultado del P0.</summary>
    public sealed class CampaignResultInfo
    {
        public string LevelName;
        public bool Victory;
        public VictoryResult Reward;
        public string CurrencyName;
        public int CurrencyTotal;
        public string TowerName, TowerDescription;
        public string WorldName, FollowText;
        public bool HasNext;
        public string NextName;
        public int TandaPending;
    }

    /// <summary>
    /// Pantallas y HUD de la partida (UXS-001.6, UXS-002.4, UXS-002.5). Muestra y comunica; no decide reglas:
    /// todo lo que el jugador hace sale por los eventos C# de abajo y lo resuelve el flujo del juego contra el Match.
    /// TL-003: vive en el prefab Hud (UI/Prefabs); acá solo se enlazan datos y botones. Los textos que cambian seguido
    /// (valores, cuenta, cursor, barra del miniboss) están en canvases propios para no reconstruir toda la interfaz.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        // ---- lo que el jugador pide (lo atiende el flujo del juego)
        public event Action<bool> PlayClicked;
        public event Action QuitClicked, ResumeClicked, RestartClicked, ExitClicked, RetryClicked, TutorialNextClicked, UpgradeClicked, SellClicked, PauseClicked, CollectClicked, NextClicked, TandaClicked;
        public event Action<string> CardClicked;
        public event Action<bool> UpgradeHover;
        public event Action<float> VolumeChanged;
        public event Action<bool> MuteChanged;

        [Header("Raíz")]
        [SerializeField] internal Canvas canvas;
        [Header("Pantallas")]
        [SerializeField] internal GameObject startScreen, errorScreen, hudRoot, pauseScreen, resultScreen;
        [SerializeField] internal TextMeshProUGUI errorText, startFooter, pauseExitLabel, startQuitLabel, startNote;
        [SerializeField] internal Toggle tutorialToggle, pauseMute;
        [SerializeField] internal Slider pauseVolume;
        [SerializeField] internal Button startPlay, startQuit, errorQuit, pauseResume, pauseRestart, pauseExit, pauseButton;
        [Header("HUD")]
        [SerializeField] internal TextMeshProUGUI vidaText, oroText, oleadaText, levelText, hintText;
        [SerializeField] internal RectTransform bottomBand;
        [SerializeField] internal TowerCardView cardPrefab;
        [SerializeField] internal GameObject tooltip;
        [SerializeField] internal TextMeshProUGUI tooltipTitle, tooltipBody;
        [Header("Miniboss")]
        [SerializeField] internal GameObject bossBar, bossArmorRoot;
        [SerializeField] internal TextMeshProUGUI bossName;
        [SerializeField] internal RectTransform bossHpFill, bossArmorFill;
        [Header("Panel de torre")]
        [SerializeField] internal GameObject towerPanel;
        [SerializeField] internal TextMeshProUGUI tpTitle, tpStats, tpTargets, tpPreview, tpN3, tpUpgradeLabel, tpSellLabel, tpCollectLabel;
        [SerializeField] internal Button tpUpgrade, tpSell, tpCollect;
        [SerializeField] internal HoverRelay tpUpgradeHover;
        [Header("Avisos")]
        [SerializeField] internal GameObject toast;
        [SerializeField] internal TextMeshProUGUI toastText, countdownText;
        [SerializeField] internal GameObject tutorialPanel;
        [SerializeField] internal TextMeshProUGUI tutorialText, tutorialButtonText;
        [SerializeField] internal Button tutorialButton;
        [SerializeField] internal RectTransform highlightRt;
        [SerializeField] internal GameObject cursorLabel;
        [SerializeField] internal TextMeshProUGUI cursorText;
        [Header("Resultado")]
        [SerializeField] internal TextMeshProUGUI resTitle, resLine, resRule, resCause, resTime, resFile, resReward;
        [SerializeField] internal Button resPrimary, resSecondary, resTertiary, resTanda;
        [SerializeField] internal TextMeshProUGUI resPrimaryLabel, resSecondaryLabel, resTertiaryLabel;
        [SerializeField] internal Image[] resStars = new Image[3];

        RectTransform root, vidaRt, oroRt, oleadaRt, tooltipRt, cursorRt;
        readonly List<TowerCardView> cards = new List<TowerCardView>();
        readonly List<TowerCardView> spareCards = new List<TowerCardView>();
        float toastUntil;
        readonly Queue<(string text, float seconds)> toastQueue = new Queue<(string, float)>();
        string countdownLabel; float countdownStart;
        RectTransform highlightTarget;
        int bossId; float bossMaxHp, bossMaxArmor;
        Tower panelTower;
        int lastCollectAmount = -1; bool lastCollectFull;
        Match match;
        int lastGold = int.MinValue;
        readonly HashSet<string> seenEnemies = new HashSet<string>();
        TimingData timing = new TimingData { countdownStep = 0.8f, defenseDuration = 1f };
        readonly Dictionary<RectTransform, float> pulses = new Dictionary<RectTransform, float>();
        readonly List<RectTransform> pulseDone = new List<RectTransform>();
        readonly Vector3[] corners = new Vector3[4];

        public bool TutorialStepNeedsButton { get; private set; }
        public bool ResultHasNext { get; private set; }
        public bool CampaignResult { get; private set; }
        /// <summary>Etiqueta de salida del resultado del P0 (en la campaña, el P0 vuelve al Laboratorio).</summary>
        public string ResultExitLabel;
        public Canvas Canvas => canvas;

        // ------------------------------------------------------------------ enlace
        void Awake()
        {
            root = (RectTransform)canvas.transform;
            vidaRt = vidaText.rectTransform; oroRt = oroText.rectTransform; oleadaRt = oleadaText.rectTransform;
            tooltipRt = (RectTransform)tooltip.transform;
            cursorRt = (RectTransform)cursorLabel.transform;

            pauseButton.onClick.AddListener(() => PauseClicked?.Invoke());
            tpCollect.onClick.AddListener(() => CollectClicked?.Invoke());
            tpUpgrade.onClick.AddListener(() => UpgradeClicked?.Invoke());
            tpSell.onClick.AddListener(() => SellClicked?.Invoke());
            tpUpgradeHover.Enter = () => { tpPreview.gameObject.SetActive(true); UpgradeHover?.Invoke(true); };
            tpUpgradeHover.Exit = () => { if (tpPreview != null) tpPreview.gameObject.SetActive(false); UpgradeHover?.Invoke(false); };
            tutorialButton.onClick.AddListener(() => TutorialNextClicked?.Invoke());
            pauseResume.onClick.AddListener(() => ResumeClicked?.Invoke());
            pauseRestart.onClick.AddListener(() => RestartClicked?.Invoke());
            pauseExit.onClick.AddListener(() => ExitClicked?.Invoke());
            pauseVolume.SetValueWithoutNotify(PlayerPrefs.GetFloat("cd_volumen", 0.7f));
            pauseVolume.onValueChanged.AddListener(v => VolumeChanged?.Invoke(v));
            pauseMute.SetIsOnWithoutNotify(PlayerPrefs.GetInt("cd_silencio", 0) == 1);
            pauseMute.onValueChanged.AddListener(v => MuteChanged?.Invoke(v));
            resTanda.onClick.AddListener(() => TandaClicked?.Invoke());
            resPrimary.onClick.AddListener(() => { if (CampaignResult && ResultHasNext) NextClicked?.Invoke(); else RetryClicked?.Invoke(); });
            resSecondary.onClick.AddListener(() => RetryClicked?.Invoke());
            resTertiary.onClick.AddListener(() => ExitClicked?.Invoke());
            startPlay.onClick.AddListener(() => PlayClicked?.Invoke(tutorialToggle == null || tutorialToggle.isOn));
            startQuit.onClick.AddListener(() => QuitClicked?.Invoke());
            errorQuit.onClick.AddListener(() => QuitClicked?.Invoke());

            ShowOnly(null);
            hudRoot.SetActive(false);
            towerPanel.SetActive(false);
            tooltip.SetActive(false);
            bossBar.SetActive(false);
            toast.SetActive(false);
            countdownText.gameObject.SetActive(false);
            tutorialPanel.SetActive(false);
            highlightRt.gameObject.SetActive(false);
            cursorLabel.SetActive(false);
            tpPreview.gameObject.SetActive(false);
        }

        /// <summary>Tiempos de la cuenta regresiva: salen del balance, no del código.</summary>
        public void SetTiming(TimingData t) { if (t != null) timing = t; }

        // ------------------------------------------------------------------ tarjetas
        /// <summary>Las tarjetas son las torres que el jugador tiene en esta partida: hasta doce en una franja (Doc 03 §12, UXS-002.4).</summary>
        void BuildCards(IReadOnlyList<TowerTypeData> types, Func<string, Color> family)
        {
            foreach (var c in cards) { c.gameObject.SetActive(false); spareCards.Add(c); }
            cards.Clear();
            int n = Mathf.Max(1, types.Count);
            float gap = n > 6 ? 12f : 22f;
            float w = Mathf.Min(340f, (1920f - 80f - (n - 1) * gap) / n);
            float total = n * w + (n - 1) * gap;
            for (int i = 0; i < types.Count; i++)
            {
                var t = types[i];
                TowerCardView c;
                if (spareCards.Count > 0) { c = spareCards[spareCards.Count - 1]; spareCards.RemoveAt(spareCards.Count - 1); }
                else
                {
                    c = Instantiate(cardPrefab, bottomBand);
                    var card = c;
                    c.button.onClick.AddListener(() => CardClicked?.Invoke(card.Id));
                    c.hover.Enter = () => ShowTooltip(card);
                    c.hover.Exit = () => { if (tooltip != null) tooltip.SetActive(false); };
                }
                c.gameObject.SetActive(true);
                float x = -total * 0.5f + w * 0.5f + i * (w + gap);
                c.Bind(t, i, family != null ? family(t.id) : Palette.Bloqueo, w, x);
                cards.Add(c);
            }
        }

        /// <summary>Tecla de la tarjeta i: 1…9, 0, -, = (UXS-002.4: una tecla por tarjeta hasta doce).</summary>
        public static string KeyLabel(int i) => i < 9 ? (i + 1).ToString() : i == 9 ? "0" : i == 10 ? "-" : "=";

        void ShowTooltip(TowerCardView c)
        {
            if (c == null || tooltip == null) return;
            tooltip.SetActive(true);
            tooltipTitle.text = $"{c.Type.displayName} · {c.Type.cost} oro";
            tooltipBody.text = $"{c.Type.description}\n{TargetsLine(c.Type)}\nNivel 1: {StatsLine(c.Type, c.Type.levels[0])}";
            c.Rect.GetWorldCorners(corners);
            float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            float x = (corners[0].x + corners[2].x) * 0.5f / scale;
            float half = tooltipRt.sizeDelta.x * 0.5f;
            x = Mathf.Clamp(x, half + 8f, root.rect.width - half - 8f);
            tooltipRt.anchoredPosition = new Vector2(x, 160f);
        }

        public static string TargetsLine(TowerTypeData t)
        {
            if (!t.Attacks) return "No ataca · junta oro";
            string layers = t.targetsAir && t.targetsGround ? "Tierra y aire" : t.targetsAir ? "Solo aire" : "Solo tierra";
            string metal = t.metalEfficiency <= 0f ? "Metal: no lo daña"
                         : t.attack == AttackKind.Inferno ? "Metal: al máximo"
                         : t.metalEfficiency >= 0.999f ? "Metal: lo rompe" : $"Metal: lento ({Mathf.RoundToInt(t.metalEfficiency * 100)} %)";
            string area = t.attack == AttackKind.Area || t.attack == AttackKind.Mortar ? " · Área" : t.attack == AttackKind.Chain ? " · Rebota" : t.attack == AttackKind.Flame ? " · Quema" : "";
            return $"{layers}{area} · {metal}";
        }

        public static string TargetsShort(TowerTypeData t)
        {
            if (!t.Attacks) return "Junta oro";
            string layers = t.targetsAir && t.targetsGround ? "Tierra+aire" : t.targetsAir ? "Aire" : "Tierra";
            string metal = t.metalEfficiency <= 0f ? "metal no" : t.attack == AttackKind.Inferno ? "metal al máx." : t.metalEfficiency >= 0.999f ? "metal sí" : "metal lento";
            return $"{layers} · {metal}";
        }

        // ------------------------------------------------------------------ pantallas
        void ShowOnly(GameObject screen)
        {
            startScreen.SetActive(startScreen == screen);
            errorScreen.SetActive(errorScreen == screen);
            pauseScreen.SetActive(pauseScreen == screen);
            resultScreen.SetActive(resultScreen == screen);
        }

        /// <summary>En la campaña, salir de la pausa vuelve al mapa (UXS-002.1).</summary>
        public void SetExitLabel(string label) { if (pauseExitLabel != null) pauseExitLabel.text = label; }

        public void SyncAudio(float volume, bool muted)
        {
            if (pauseVolume != null) pauseVolume.SetValueWithoutNotify(volume);
            if (pauseMute != null) pauseMute.SetIsOnWithoutNotify(muted);
        }

        public bool StartVisible => startScreen.activeSelf;
        public bool ResultVisible => resultScreen.activeSelf;
        public bool TutorialVisible => tutorialPanel != null && tutorialPanel.activeSelf;
        public bool ErrorVisible => errorScreen.activeSelf;
        public bool PauseVisible => pauseScreen.activeSelf;
        public bool TandaButtonVisible => resTanda != null && resTanda.gameObject.activeSelf && resultScreen.activeSelf;

        public void ShowStart(bool tutorialDefault, string footer, string quitLabel = "Salir  [S]", string note = null, bool quitIsBack = false)
        {
            ShowOnly(startScreen);
            if (startQuitLabel != null) startQuitLabel.text = quitLabel;
            if (startNote != null) startNote.text = note ?? "";
            hudRoot.SetActive(false);
            HideTowerPanel(); HideCursorLabel(); SetTutorialStep(0);
            countdownText.gameObject.SetActive(false); toast.SetActive(false); toastQueue.Clear();
            tutorialToggle.isOn = tutorialDefault;
            startFooter.text = footer;
            // en WebGL no hay "salir de la aplicación", pero "volver al inicio" del laboratorio sí (revisión técnica, hallazgo 5)
            startQuit.gameObject.SetActive(quitIsBack || Application.platform != RuntimePlatform.WebGLPlayer);
        }

        /// <summary>Oculta todo lo de la partida: lo usan los menús de la campaña (UXS-002.1).</summary>
        public void HideAll()
        {
            ShowOnly(null);
            hudRoot.SetActive(false);
            HideTowerPanel(); HideCursorLabel(); SetTutorialStep(0);
            countdownText.gameObject.SetActive(false); toast.SetActive(false); toastQueue.Clear();
            bossBar.SetActive(false); bossId = 0;
            match = null;
        }

        public bool StartTutorialChecked => tutorialToggle != null && tutorialToggle.isOn;

        public void ShowError(IEnumerable<string> errors)
        {
            ShowOnly(errorScreen);
            hudRoot.SetActive(false);
            var sb = new StringBuilder();
            foreach (var e in errors) sb.Append("• ").Append(e).Append('\n');
            errorText.text = sb.ToString();
        }

        public void ShowGame(Match m, string levelName, Func<string, Color> family)
        {
            match = m;
            seenEnemies.Clear();
            ShowOnly(null);
            hudRoot.SetActive(true);
            HideTowerPanel(); HideCursorLabel(); SetTutorialStep(0);   // un reintento no hereda el cartel del tutorial (revisión técnica, hallazgo 2)
            BuildCards(m.TowerTypes, family);
            lastGold = int.MinValue;
            RefreshStats();
            oleadaText.text = $"OLEADA 0/{m.WaveCount}"; oleadaText.color = Palette.Texto;
            levelText.text = levelName ?? m.Level.displayName;
            bossBar.SetActive(false); bossId = 0;
            toast.SetActive(false); toastQueue.Clear();
            tooltip.SetActive(false);
            SetSelectedCard(null);
        }

        public void ShowPause(bool on)
        {
            if (on) { ShowOnly(pauseScreen); HideCursorLabel(); }
            else if (pauseScreen.activeSelf) ShowOnly(null);
        }

        public void ShowResult(MatchReport r, Match m, string file, CampaignResultInfo info = null)
        {
            ShowOnly(resultScreen);
            HideTowerPanel(); HideCursorLabel(); SetTutorialStep(0);
            toast.SetActive(false); toastQueue.Clear(); countdownText.gameObject.SetActive(false); tooltip.SetActive(false);
            bossBar.SetActive(false);
            CampaignResult = info != null;
            bool win = r.result == "victoria";
            resTitle.text = win ? "¡VICTORIA!" : "DERROTA";
            resTitle.color = win ? Palette.Acento : Palette.Vida;
            for (int i = 0; i < 3; i++) resStars[i].color = i < r.stars ? Palette.Oro : Palette.WithAlpha(Palette.Bloqueo, 0.35f);
            int maxHp = m.Balance.economy.baseHp;
            resLine.text = win ? $"Vida {r.baseHpLeft}/{maxHp}  →  {r.stars} {(r.stars == 1 ? "estrella" : "estrellas")}"
                               : $"Caíste en la oleada {r.waveReached} de {r.waveCount}";
            resRule.text = $"{m.Balance.stars.threeStarsMinHp} de vida = 3 · de {m.Balance.stars.twoStarsMinHp} a {m.Balance.stars.threeStarsMinHp - 1} = 2 · de 1 a {m.Balance.stars.twoStarsMinHp - 1} = 1";
            var sb = new StringBuilder();
            foreach (var e in r.enemies)
                if (e.leaked > 0)
                {
                    if (sb.Length > 0) sb.Append("   ·   ");
                    sb.Append($"{DisplayName(m, e.type)} ×{e.leaked} (−{e.baseDamage})");
                }
            resCause.text = sb.Length > 0 ? "Entraron: " + sb : "No entró ningún enemigo.";
            resTime.text = $"Duración {MatchRecorder.Clock(r.durationSeconds)} (sin tutorial ni pausa)";
            resFile.text = string.IsNullOrEmpty(file) ? "" : "Registro: " + file;

            if (info == null)
            {
                ResultHasNext = false;
                resReward.text = "";
                resTanda.gameObject.SetActive(false);
                SetButtons("Reintentar  [Enter]", null, ResultExitLabel ?? "Salir al inicio  [Esc]");
                return;
            }
            ResultHasNext = win && info.HasNext;
            var rw = new StringBuilder();
            if (win && info.Reward != null)
            {
                var v = info.Reward;
                string why = v.FirstClear ? "primera victoria" : v.NewRecord ? $"repetición · récord de {v.PreviousStars} a {r.stars} estrellas" : "repetición";
                rw.Append($"<color=#8FD3FF>+{v.Currency} {info.CurrencyName}</color>  ({why}) · total {info.CurrencyTotal}\n");
                if (!string.IsNullOrEmpty(v.TowerUnlocked))
                    rw.Append($"<color=#F2C14E>Nueva torre: {info.TowerName}</color> — {info.TowerDescription} Sus mejoras permanentes ya están en la Tienda.\n");
                if (v.TandaEarned) rw.Append("<color=#45D0C4>¡Continente completado!</color> Ganaste una mejora permanente para una torre inicial.\n");
                if (!string.IsNullOrEmpty(v.WorldUnlocked)) rw.Append($"<color=#45D0C4>¡Se abrió el {info.WorldName}!</color> {info.FollowText}\n");
            }
            else if (!win) rw.Append("La derrota no entrega estrellas, cristales ni desbloqueos. El nivel se puede repetir las veces que quieras.");
            resReward.text = rw.ToString();
            resTanda.gameObject.SetActive(win && info.TandaPending > 0);
            if (win && info.HasNext) SetButtons("Siguiente nivel  [Enter]", "Reintentar  [R]", "Mapa  [Esc]");
            else SetButtons("Reintentar  [Enter / R]", null, "Mapa  [Esc]");
        }

        void SetButtons(string primary, string secondary, string exit)
        {
            resPrimaryLabel.text = primary;
            resSecondary.gameObject.SetActive(secondary != null);
            if (secondary != null) resSecondaryLabel.text = secondary;
            resTertiaryLabel.text = exit;
            var p = (RectTransform)resPrimary.transform; var t = (RectTransform)resTertiary.transform;
            p.anchoredPosition = new Vector2(secondary != null ? -310 : -170, 100);
            t.anchoredPosition = new Vector2(secondary != null ? 310 : 170, 100);
        }

        public void HideTandaButton() { if (resTanda != null) resTanda.gameObject.SetActive(false); }

        static string DisplayName(Match m, string enemyId)
        {
            foreach (var t in m.Balance.enemies) if (t.id == enemyId) return t.displayName;
            return enemyId;
        }

        // ------------------------------------------------------------------ HUD
        public void RefreshStats()
        {
            if (match == null) return;
            vidaText.text = $"VIDA {match.BaseHp}/{match.Balance.economy.baseHp}";
            if (match.Gold != lastGold)
            {
                lastGold = match.Gold;
                oroText.text = $"ORO {match.Gold}";
                foreach (var c in cards) c.SetGold(match.Gold, Palette.Oro, Palette.Invalido);
            }
        }

        public void SetCardsInteractable(Func<string, bool> allowed)
        {
            foreach (var c in cards) c.SetInteractable(allowed == null || allowed(c.Id));
        }

        public void SetSelectedCard(string id)
        {
            foreach (var c in cards) c.SetSelected(c.Id == id);
        }

        public void OnEvent(SimEvent e)
        {
            if (match == null) return;
            switch (e.Type)
            {
                case SimEventType.GoldChanged:
                    RefreshStats();
                    Pulse(oroRt);
                    if (panelTower != null && towerPanel.activeSelf) RefreshCollect();
                    break;
                case SimEventType.BaseDamaged:
                    RefreshStats();
                    Pulse(vidaRt);
                    break;
                case SimEventType.WaveStarted:
                    oleadaText.text = $"OLEADA {e.Int1}/{e.Int2}"; oleadaText.color = Palette.Texto;
                    Pulse(oleadaRt);
                    break;
                case SimEventType.WaveCleared:
                    oleadaText.text = $"OLEADA {e.Int1}/{e.Int2} — completa"; oleadaText.color = Palette.Acento;
                    break;
                case SimEventType.Countdown:
                    countdownLabel = e.Text; countdownStart = Time.time;
                    countdownText.text = countdownLabel;
                    countdownText.gameObject.SetActive(true);
                    break;
                case SimEventType.EnemySpawned:
                    {
                        var en = match.GetEnemy(e.EnemyId);
                        if (en != null && en.Type.miniboss) ShowBoss(en);
                        if (seenEnemies.Add(e.Text))
                            foreach (var t in match.Balance.enemies)
                                if (t.id == e.Text) { ShowToast(t.miniboss ? $"¡Miniboss: {t.displayName}! {t.counterHint}" : $"Nuevo: {t.displayName} — {t.counterHint}", 4.5f, true); break; }
                        break;
                    }
                case SimEventType.EnemyKilled:
                case SimEventType.EnemyReachedBase:
                    if (e.EnemyId == bossId) { bossBar.SetActive(false); bossId = 0; }
                    break;
                case SimEventType.GoldStoredFull:
                case SimEventType.GoldCollected:
                    if (panelTower != null && panelTower.Id == e.TowerId) RefreshCollect();
                    break;
            }
        }

        void ShowBoss(Enemy e)
        {
            bossId = e.Id; bossMaxHp = e.Type.hp; bossMaxArmor = e.Type.armor;
            bossName.text = $"MINIBOSS · {e.Type.displayName}";
            bossArmorRoot.SetActive(bossMaxArmor > 0f);
            bossBar.SetActive(true);
        }

        void Pulse(RectTransform rt) { if (rt != null) pulses[rt] = Time.unscaledTime; }

        /// <summary>Aviso arriba. Si hay uno visible, el nuevo espera su turno (UXS-002.4: un aviso a la vez).</summary>
        public void ShowToast(string text, float seconds, bool urgent = false)
        {
            if (toast.activeSelf && !urgent) { toastQueue.Enqueue((text, seconds)); return; }
            toastText.text = text;
            toast.SetActive(true);
            toastUntil = Time.time + seconds;
        }

        // ------------------------------------------------------------------ panel de torre (UXS-001.3, UXS-002.4)
        public bool TowerPanelVisible => towerPanel.activeSelf;

        public void ShowTowerPanel(Tower t, Match m, bool sellArmed)
        {
            panelTower = t;
            towerPanel.SetActive(true);
            var s = t.Stats;
            tpTitle.text = $"{t.Type.displayName} · Nivel {t.Level}";
            tpStats.text = StatsLine(t.Type, s);
            tpTargets.text = TargetsLine(t.Type);
            bool gold = t.Type.attack == AttackKind.Gold;
            tpCollect.gameObject.SetActive(gold);
            lastCollectAmount = -1;
            if (gold) RefreshCollect();
            if (t.CanUpgrade)
            {
                int cost = m.UpgradeCost(t);
                bool afford = m.Gold >= cost;
                tpUpgrade.gameObject.SetActive(true);
                tpUpgrade.interactable = afford;
                tpUpgradeLabel.text = afford ? $"Mejorar a nivel {t.Level + 1} · {cost} oro  [U]" : $"Mejorar a nivel {t.Level + 1} · {cost} oro (faltan {cost - m.Gold})  [U]";
                tpUpgradeLabel.color = afford ? Palette.Texto : Palette.Invalido;
                var n = t.Type.levels[t.Level];
                tpPreview.text = DeltaLine(t.Type, s, n);
            }
            else
            {
                tpUpgrade.gameObject.SetActive(false);
                tpPreview.gameObject.SetActive(false);
            }
            int refund = m.SellRefund(t);
            string stored = gold && t.Stored >= 1f ? $" (+{(int)t.Stored} guardado)" : "";
            tpSellLabel.text = sellArmed ? $"¿Seguro? Vender +{refund}{stored}  [V]" : $"Vender +{refund}{stored}  [V]";
            tpSellLabel.color = sellArmed ? Palette.Invalido : Palette.Texto;
            tpN3.text = t.CanUpgrade ? "Nivel 3 · bloqueado · próximamente" : "Nivel 2 es el máximo de la beta · Nivel 3 próximamente";
        }

        /// <summary>Botón de recoger de la torre de oro: el texto cambia solo si cambió lo guardado (no en cada cuadro).</summary>
        void RefreshCollect()
        {
            if (panelTower == null || panelTower.Type.attack != AttackKind.Gold) return;
            int amount = (int)panelTower.Stored;
            bool full = panelTower.Full;
            if (amount == lastCollectAmount && full == lastCollectFull) return;
            lastCollectAmount = amount; lastCollectFull = full;
            tpCollect.interactable = amount > 0;
            tpCollectLabel.text = full ? $"¡Llena! Recoger {amount}  [C]" : amount > 0 ? $"Recoger {amount} de {panelTower.Stats.goldCapacity:0}  [C]" : $"Juntando… 0 de {panelTower.Stats.goldCapacity:0}";
            tpCollectLabel.color = full ? Palette.Oro : Palette.Texto;
        }

        /// <summary>UXS-001.3: qué cambia al mejorar, en una línea.</summary>
        static string DeltaLine(TowerTypeData type, TowerLevelData a, TowerLevelData b)
        {
            switch (type.attack)
            {
                case AttackKind.Gold: return $"Oro {UiKit.Num(a.goldPerSecond)}→{UiKit.Num(b.goldPerSecond)}/s · capacidad {a.goldCapacity:0}→{b.goldCapacity:0}";
                case AttackKind.Inferno: return $"Máximo {a.rampDps[a.rampDps.Length - 1]:0}→{b.rampDps[b.rampDps.Length - 1]:0}/s · alcance {UiKit.Num(a.range)}→{UiKit.Num(b.range)}";
                case AttackKind.Chain: return $"Daño {UiKit.Num(a.damage)}→{UiKit.Num(b.damage)} · saltos {a.chainJumps}→{b.chainJumps} · alcance {UiKit.Num(a.range)}→{UiKit.Num(b.range)}";
                case AttackKind.Flame: return $"Daño {UiKit.Num(a.damage)}→{UiKit.Num(b.damage)} · quema {UiKit.Num(a.burnDps)}→{UiKit.Num(b.burnDps)}/s · alcance {UiKit.Num(a.range)}→{UiKit.Num(b.range)}";
            }
            string area = a.areaRadius > 0f ? $" · área {UiKit.Num(a.areaRadius)}→{UiKit.Num(b.areaRadius)}" : "";
            return $"Daño {UiKit.Num(a.damage)}→{UiKit.Num(b.damage)} · {UiKit.Num(a.interval)}→{UiKit.Num(b.interval)} s · alcance {UiKit.Num(a.range)}→{UiKit.Num(b.range)}{area}";
        }

        public static string StatsLine(TowerTypeData type, TowerLevelData s)
        {
            switch (type.attack)
            {
                case AttackKind.Gold: return $"Junta {UiKit.Num(s.goldPerSecond)} de oro por segundo · capacidad {s.goldCapacity:0} · clic para recoger";
                case AttackKind.Inferno: return $"Daño por segundo {s.rampDps[0]:0} → {s.rampDps[s.rampDps.Length - 1]:0} en {s.rampDps.Length} etapas de {UiKit.Num(s.rampStep)} s · alcance {UiKit.Num(s.range)}";
                case AttackKind.Mortar: return $"Daño {UiKit.Num(s.damage)} en área {UiKit.Num(s.areaRadius)} · cada {UiKit.Num(s.interval)} s · alcance {UiKit.Num(s.minRange)} a {UiKit.Num(s.range)}";
                case AttackKind.Chain: return $"Daño {UiKit.Num(s.damage)} · cada {UiKit.Num(s.interval)} s · alcance {UiKit.Num(s.range)} · salta a {s.chainJumps} más";
                case AttackKind.Flame: return $"Daño {UiKit.Num(s.damage)} + quema {UiKit.Num(s.burnDps)}/s por {UiKit.Num(s.burnDuration)} s · cada {UiKit.Num(s.interval)} s · alcance {UiKit.Num(s.range)}";
            }
            string area = s.areaRadius > 0f ? $" · área {UiKit.Num(s.areaRadius)}" : "";
            return $"Daño {UiKit.Num(s.damage)} · cada {UiKit.Num(s.interval)} s · alcance {UiKit.Num(s.range)}{area}";
        }

        /// <summary>Línea de ayuda sobre la franja inferior (UXS-001.6: Partida y Colocando).</summary>
        public void SetHint(string text)
        {
            if (hintText != null && hintText.text != text) hintText.text = text;
        }

        public void ShowUpgradePreview(bool on)
        {
            if (tpPreview == null) return;
            tpPreview.gameObject.SetActive(on && towerPanel.activeSelf && tpUpgrade.gameObject.activeSelf);
        }

        public void HideTowerPanel()
        {
            panelTower = null;
            if (towerPanel != null && towerPanel.activeSelf) { tpPreview.gameObject.SetActive(false); towerPanel.SetActive(false); }
        }

        // ------------------------------------------------------------------ etiqueta del cursor (UXS-001.2)
        string lastCursorText; Color lastCursorColor;
        public void SetCursorLabel(string text, Color color, Vector2 screenPos)
        {
            if (!cursorLabel.activeSelf) cursorLabel.SetActive(true);
            if (text != lastCursorText) { cursorText.text = text; lastCursorText = text; }
            if (color != lastCursorColor) { cursorText.color = color; lastCursorColor = color; }
            float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            var pos = screenPos / scale + new Vector2(26, -22);
            var area = root.rect.size; var size = cursorRt.sizeDelta;
            if (pos.x + size.x > area.x - 8f) pos.x = screenPos.x / scale - 26f - size.x;
            pos.x = Mathf.Clamp(pos.x, 8f, Mathf.Max(8f, area.x - size.x - 8f));
            pos.y = Mathf.Clamp(pos.y, size.y + 8f, area.y - 8f);
            cursorRt.anchoredPosition = pos;
        }

        public void HideCursorLabel() { if (cursorLabel != null && cursorLabel.activeSelf) cursorLabel.SetActive(false); }

        // ------------------------------------------------------------------ tutorial (GDS-001.6)
        public void SetTutorialStep(int step, string towerId = null, string towerName = "Torre de arqueras", int waves = 5, int gold = 100)
        {
            TutorialStepNeedsButton = false;
            highlightTarget = null;
            if (step <= 0) { tutorialPanel.SetActive(false); highlightRt.gameObject.SetActive(false); SetCardsInteractable(null); return; }
            tutorialPanel.SetActive(true);
            string text; bool button = true; string buttonText = "Siguiente [Enter]";
            switch (step)
            {
                case 1: text = "Esta es tu base. Si su vida llega a 0, perdés."; highlightTarget = vidaRt; break;
                case 2: text = "Los enemigos llegan por este camino, desde la entrada."; break;
                case 3: text = $"Tenés {gold} de oro. Las torres cuestan oro."; highlightTarget = oroRt; break;
                case 4: { var c = cards.Find(k => k.Id == towerId); int idx = cards.IndexOf(c); text = $"Elegí la {towerName}: clic en su tarjeta o tecla [{KeyLabel(Mathf.Max(0, idx))}]."; button = false; highlightTarget = c != null ? c.Rect : null; break; }
                case 5: text = "Colocala cerca del camino: donde pasa dos veces por su alcance. Blanco: se puede. Rojo: no."; button = false; break;
                case 6: text = "Cada enemigo derrotado te da oro para más torres."; highlightTarget = oroRt; break;
                default: text = $"Aguantá las {waves} oleadas y ganás."; buttonText = "¡A defender! [Enter]"; highlightTarget = oleadaRt; break;
            }
            tutorialText.text = text;
            tutorialButton.gameObject.SetActive(button);
            tutorialButtonText.text = buttonText;
            TutorialStepNeedsButton = button;
            highlightRt.gameObject.SetActive(highlightTarget != null);
        }

        // ------------------------------------------------------------------ animación de la interfaz (tiempo real)
        void Update()
        {
            float now = Time.unscaledTime;
            if (toast.activeSelf && Time.time > toastUntil)
            {
                if (toastQueue.Count > 0) { var n = toastQueue.Dequeue(); toastText.text = n.text; toastUntil = Time.time + n.seconds; }
                else toast.SetActive(false);
            }

            if (countdownText.gameObject.activeSelf)
            {
                float t = Time.time - countdownStart;   // tiempo escalado: la pausa congela la cuenta (SOL-001 D11)
                bool defense = countdownLabel == "DEFENSE";
                float dur = Mathf.Max(0.05f, defense ? timing.defenseDuration : timing.countdownStep);   // del balance, no del código
                float fade = dur * 0.25f;                                                                // GDS-001.6: 0,2 entra · 0,4 queda · 0,2 sale sobre 0,8
                if (t >= dur) countdownText.gameObject.SetActive(false);
                else
                {
                    float a = defense ? 1f - t / dur : (t < fade ? t / fade : t > dur - fade ? 1f - (t - (dur - fade)) / fade : 1f);
                    var c = defense ? Palette.Oro : Palette.Texto; c.a = Mathf.Clamp01(a);
                    countdownText.color = c;
                    countdownText.rectTransform.localScale = Vector3.one * (defense ? Mathf.Lerp(1f, 1.6f, t / dur) : 1f);
                }
            }

            // barra del miniboss: cambia por cuadro (UXS-002.5, frecuencia declarada) — en su propio canvas
            if (bossId != 0 && match != null && bossBar.activeSelf)
            {
                var b = match.GetEnemy(bossId);
                if (b == null || !b.Alive) { bossBar.SetActive(false); bossId = 0; }
                else
                {
                    bossHpFill.anchorMax = new Vector2(Mathf.Clamp01(b.Hp / Mathf.Max(1f, bossMaxHp)), 1f);
                    if (bossMaxArmor > 0f) bossArmorFill.anchorMax = new Vector2(Mathf.Clamp01(b.Armor / bossMaxArmor), 1f);
                }
            }
            if (panelTower != null && panelTower.Type.attack == AttackKind.Gold && towerPanel.activeSelf) RefreshCollect();

            if (pulses.Count > 0)
            {
                pulseDone.Clear();
                foreach (var kv in pulses)
                {
                    float t = (now - kv.Value) / 0.15f;
                    if (kv.Key == null) { pulseDone.Add(kv.Key); continue; }
                    if (t >= 1f) { kv.Key.localScale = Vector3.one; pulseDone.Add(kv.Key); }
                    else kv.Key.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
                }
                foreach (var d in pulseDone) pulses.Remove(d);
            }

            if (highlightTarget != null && highlightRt.gameObject.activeSelf)
            {
                highlightTarget.GetWorldCorners(corners);
                float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
                var center = (corners[0] + corners[2]) * 0.5f;
                var size = (corners[2] - corners[0]) / scale;
                highlightRt.anchorMin = highlightRt.anchorMax = Vector2.zero;
                highlightRt.anchoredPosition = new Vector2(center.x, center.y) / scale;
                float pulse = 1f + 0.06f * Mathf.Sin(now * 6f);
                highlightRt.sizeDelta = new Vector2(size.x + 24, size.y + 16) * pulse;
            }
        }
    }
}
