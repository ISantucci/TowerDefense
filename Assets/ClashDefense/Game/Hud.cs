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
    /// todo lo que el jugador hace sale por los eventos C# de abajo y lo resuelve GameBootstrap contra el Match.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        // ---- lo que el jugador pide (lo atiende GameBootstrap)
        public event Action<bool> PlayClicked;
        public event Action QuitClicked, ResumeClicked, RestartClicked, ExitClicked, RetryClicked, TutorialNextClicked, UpgradeClicked, SellClicked, PauseClicked, CollectClicked, NextClicked, TandaClicked;
        public event Action<string> CardClicked;
        public event Action<bool> UpgradeHover;
        public event Action<float> VolumeChanged;
        public event Action<bool> MuteChanged;

        Canvas canvas;
        RectTransform root;
        // pantallas
        GameObject startScreen, errorScreen, hudRoot, pauseScreen, resultScreen;
        TextMeshProUGUI errorText, startFooter, pauseExitLabel, startQuitLabel, startNote;
        Toggle tutorialToggle, pauseMute;
        Slider pauseVolume;
        GameObject quitButton;
        // HUD
        TextMeshProUGUI vidaText, oroText, oleadaText, levelText;
        RectTransform vidaRt, oroRt, oleadaRt, bottomBand;
        readonly List<Card> cards = new List<Card>();
        GameObject tooltip; TextMeshProUGUI tooltipTitle, tooltipBody; RectTransform tooltipRt;
        // miniboss
        GameObject bossBar; TextMeshProUGUI bossName; RectTransform bossHpFill, bossArmorFill; GameObject bossArmorRoot; int bossId; float bossMaxHp, bossMaxArmor;
        // panel de torre
        GameObject towerPanel;
        TextMeshProUGUI tpTitle, tpStats, tpTargets, tpPreview, tpN3, tpUpgradeLabel, tpSellLabel, tpCollectLabel;
        Button tpUpgrade, tpSell, tpCollect;
        Tower panelTower;
        // avisos
        GameObject toast; TextMeshProUGUI toastText; float toastUntil;
        readonly Queue<(string text, float seconds)> toastQueue = new Queue<(string, float)>();
        TextMeshProUGUI hintText;
        TextMeshProUGUI countdownText; string countdownLabel; float countdownStart;
        GameObject tutorialPanel; TextMeshProUGUI tutorialText; Button tutorialButton; TextMeshProUGUI tutorialButtonText;
        RectTransform highlightRt; RectTransform highlightTarget;
        GameObject cursorLabel; TextMeshProUGUI cursorText; RectTransform cursorRt;
        // resultado
        TextMeshProUGUI resTitle, resLine, resRule, resCause, resTime, resFile, resReward;
        Button resPrimary, resSecondary, resTertiary, resTanda;
        TextMeshProUGUI resPrimaryLabel, resSecondaryLabel, resTertiaryLabel;
        readonly Image[] resStars = new Image[3];
        // estado
        Match match;
        int lastGold = int.MinValue;
        readonly HashSet<string> seenEnemies = new HashSet<string>();
        public bool TutorialStepNeedsButton { get; private set; }
        public bool ResultHasNext { get; private set; }
        public bool CampaignResult { get; private set; }
        /// <summary>Etiqueta de salida del resultado del P0 (en la campaña, el P0 vuelve al Laboratorio).</summary>
        public string ResultExitLabel;

        sealed class Card
        {
            public string Id; public int Cost; public Button Button; public Image Bg, Stripe; public Outline Outline;
            public TextMeshProUGUI Name, CostText, Targets; public RectTransform Rt; public CanvasGroup Group; public TowerTypeData Type;
        }

        // ------------------------------------------------------------------ construcción
        TimingData timing;

        public void Build(BalanceData balance)
        {
            timing = balance.timing ?? new TimingData { countdownStep = 0.8f, defenseDuration = 1f };
            canvas = UiKit.Canvas(transform, 10);
            root = (RectTransform)canvas.transform;
            BuildHud();
            BuildTowerPanel();
            BuildOverlays();
            BuildPause();
            BuildResult();
            BuildStart();
            BuildError();
            ShowOnly(null);
            hudRoot.SetActive(false);
        }

        public Canvas Canvas => canvas;

        void BuildHud()
        {
            hudRoot = UiKit.Stretch("HUD", root).gameObject;
            var top = UiKit.Rect("FranjaSuperior", hudRoot.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 76));
            UiKit.Panel(top, Palette.WithAlpha(Palette.Panel, 0.94f));
            vidaText = UiKit.Text(top, "Vida", "VIDA 100/100", 36, Palette.Vida, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(320, 0));
            vidaRt = vidaText.rectTransform;
            oroText = UiKit.Text(top, "Oro", "ORO 100", 36, Palette.Oro, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(390, 0), new Vector2(300, 0));
            oroRt = oroText.rectTransform;
            oleadaText = UiKit.Text(top, "Oleada", "OLEADA 0/5", 34, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 0));
            oleadaRt = oleadaText.rectTransform;
            levelText = UiKit.Text(top, "Nivel", "", 22, Palette.Texto2, TextAlignmentOptions.MidlineRight, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-330, 0), new Vector2(400, 0));
            UiKit.OneLine(levelText, 15f);
            UiKit.Button(top, "Pausa", "Pausa  [P / Esc]", 26, new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(280, 56), () => PauseClicked?.Invoke(), out _);

            bottomBand = UiKit.Rect("FranjaInferior", hudRoot.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 151));
            UiKit.Panel(bottomBand, Palette.WithAlpha(Palette.Panel, 0.94f));
            hintText = UiKit.Text(hudRoot.transform, "Ayuda", "", 21, Palette.Texto, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 160), new Vector2(900, 30));
            hintText.outlineWidth = 0.2f; hintText.outlineColor = new Color32(20, 22, 28, 255);

            // ayuda de la tarjeta (UXS-002.4, ley 5: lo que hace una torre está escrito antes de pagarla)
            tooltipRt = UiKit.Rect("AyudaTorre", hudRoot.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0), Vector2.zero, new Vector2(560, 150));
            UiKit.Panel(tooltipRt, Palette.WithAlpha(Palette.Panel, 0.97f), false);
            UiKit.Frame(tooltipRt, Palette.Acento, 2f);
            tooltip = tooltipRt.gameObject;
            tooltipTitle = UiKit.Text(tooltipRt, "Titulo", "", 26, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -10), new Vector2(-32, 34));
            tooltipBody = UiKit.Text(tooltipRt, "Texto", "", 20, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -46), new Vector2(-32, -56));
            tooltipBody.textWrappingMode = TextWrappingModes.Normal;
            tooltip.SetActive(false);

            // barra del miniboss (UXS-002.5)
            var bb = UiKit.Rect("Miniboss", hudRoot.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(760, 64));
            UiKit.Panel(bb, Palette.WithAlpha(Palette.Panel, 0.92f), false);
            UiKit.Frame(bb, Palette.Vida, 3f);
            bossBar = bb.gameObject;
            bossName = UiKit.Text(bb, "Nombre", "", 22, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -4), new Vector2(-28, 28));
            var hpBg = UiKit.Rect("VidaFondo", bb, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(14, 10), new Vector2(-28, 16));
            UiKit.Panel(hpBg, Palette.Zocalo, false);
            bossHpFill = UiKit.Rect("Vida", hpBg, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Panel(bossHpFill, Palette.Vida, false);
            var arBg = UiKit.Rect("MetalFondo", bb, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(14, 28), new Vector2(-28, 6));
            UiKit.Panel(arBg, Palette.Zocalo, false);
            bossArmorFill = UiKit.Rect("Metal", arBg, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Panel(bossArmorFill, Palette.Metal, false);
            bossArmorRoot = arBg.gameObject;
            bossBar.SetActive(false);
        }

        /// <summary>Las tarjetas son las torres que el jugador tiene en esta partida: hasta doce en una franja (Doc 03 §12, UXS-002.4).</summary>
        void BuildCards(IReadOnlyList<TowerTypeData> types)
        {
            foreach (var c in cards) if (c.Button != null) Destroy(c.Button.gameObject);
            cards.Clear();
            int n = Mathf.Max(1, types.Count);
            float gap = n > 6 ? 12f : 22f;
            float w = Mathf.Min(340f, (1920f - 80f - (n - 1) * gap) / n);
            float total = n * w + (n - 1) * gap;
            bool compact = w < 230f;
            for (int i = 0; i < types.Count; i++)
            {
                var t = types[i];
                var c = new Card { Id = t.id, Cost = t.cost, Type = t };
                float x = -total * 0.5f + w * 0.5f + i * (w + gap);
                c.Button = UiKit.Button(bottomBand, "Tarjeta_" + t.id, "", 20, new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(w, 128), null, out var dummy);
                dummy.gameObject.SetActive(false);
                c.Rt = (RectTransform)c.Button.transform;
                c.Bg = c.Button.GetComponent<Image>();
                c.Group = c.Button.gameObject.AddComponent<CanvasGroup>();
                c.Outline = UiKit.Outline(c.Button.gameObject, Palette.Acento, 4f); c.Outline.enabled = false;
                var stripe = UiKit.Rect("Familia", c.Rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 10));
                c.Stripe = UiKit.Panel(stripe, Palette.ForTower(t.id), false);
                UiKit.Text(c.Rt, "Tecla", $"[{KeyLabel(i)}]", compact ? 19 : 24, Palette.Texto2, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -16), new Vector2(50, 30));
                c.Name = UiKit.Text(c.Rt, "Nombre", t.Short, compact ? 22 : 28, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(12, -16), new Vector2(-60, 34));
                UiKit.OneLine(c.Name, 15f);
                c.CostText = UiKit.Text(c.Rt, "Costo", $"{t.cost} oro", compact ? 21 : 26, Palette.Oro, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(12, -6), new Vector2(-20, 32));
                UiKit.OneLine(c.CostText, 13f);
                c.Targets = UiKit.Text(c.Rt, "Objetivos", compact ? TargetsShort(t) : TargetsLine(t), compact ? 16 : 20, Palette.Texto2, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(12, 10), new Vector2(-20, 30));
                UiKit.OneLine(c.Targets, 11f);
                string id = t.id;
                c.Button.onClick.AddListener(() => CardClicked?.Invoke(id));
                var hover = c.Button.gameObject.AddComponent<HoverRelay>();
                var card = c;
                hover.Enter = () => ShowTooltip(card);
                hover.Exit = () => { if (tooltip != null) tooltip.SetActive(false); };
                cards.Add(c);
            }
        }

        /// <summary>Tecla de la tarjeta i: 1…9, 0, -, = (UXS-002.4: una tecla por tarjeta hasta doce).</summary>
        public static string KeyLabel(int i) => i < 9 ? (i + 1).ToString() : i == 9 ? "0" : i == 10 ? "-" : "=";

        void ShowTooltip(Card c)
        {
            if (c == null || tooltip == null) return;
            tooltip.SetActive(true);
            tooltipTitle.text = $"{c.Type.displayName} · {c.Type.cost} oro";
            tooltipBody.text = $"{c.Type.description}\n{TargetsLine(c.Type)}\nNivel 1: {StatsLine(c.Type, c.Type.levels[0])}";
            var corners = new Vector3[4];
            c.Rt.GetWorldCorners(corners);
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

        void BuildTowerPanel()
        {
            var rt = UiKit.Rect("PanelTorre", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -172), new Vector2(520, 430));
            UiKit.Panel(rt, Palette.WithAlpha(Palette.Panel, 0.96f));
            towerPanel = rt.gameObject;
            tpTitle = UiKit.Text(rt, "Titulo", "", 30, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(-40, 40));
            tpStats = UiKit.Text(rt, "Estadisticas", "", 22, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -60), new Vector2(-40, 56));
            tpStats.textWrappingMode = TextWrappingModes.Normal;
            tpTargets = UiKit.Text(rt, "Objetivos", "", 20, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -120), new Vector2(-40, 28));
            tpCollect = UiKit.Button(rt, "Recoger", "", 23, new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(480, 52), () => CollectClicked?.Invoke(), out tpCollectLabel);
            tpUpgrade = UiKit.Button(rt, "Mejorar", "", 23, new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(480, 56), () => UpgradeClicked?.Invoke(), out tpUpgradeLabel);
            var hover = tpUpgrade.gameObject.AddComponent<HoverRelay>();
            hover.Enter = () => { tpPreview.gameObject.SetActive(true); UpgradeHover?.Invoke(true); };
            hover.Exit = () => { if (tpPreview != null) tpPreview.gameObject.SetActive(false); UpgradeHover?.Invoke(false); };
            tpPreview = UiKit.Text(rt, "Preview", "", 20, Palette.Acento, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -272), new Vector2(-40, 28));
            UiKit.OneLine(tpPreview, 13f);
            tpPreview.gameObject.SetActive(false);
            tpSell = UiKit.Button(rt, "Vender", "", 23, new Vector2(0.5f, 1), new Vector2(0, -334), new Vector2(480, 56), () => SellClicked?.Invoke(), out tpSellLabel);
            tpN3 = UiKit.Text(rt, "N3", "Nivel 3 · bloqueado · próximamente", 20, Palette.Bloqueo, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(20, 34), new Vector2(-40, 26));
            UiKit.Text(rt, "Cerrar", "Cerrar [Esc]", 19, Palette.Texto2, TextAlignmentOptions.MidlineRight, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 8), new Vector2(200, 24));
            towerPanel.SetActive(false);
        }

        void BuildOverlays()
        {
            var t = UiKit.Rect("Aviso", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(1320, 64));
            UiKit.Panel(t, Palette.WithAlpha(Palette.Panel, 0.92f), false);
            UiKit.Frame(t, Palette.Oro, 3f);
            toast = t.gameObject;
            toastText = UiKit.Fill(t, "Texto", "", 26, Palette.Texto, TextAlignmentOptions.Center);
            UiKit.OneLine(toastText, 16f);
            toast.SetActive(false);

            countdownText = UiKit.Text(root, "Cuenta", "", 190, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1400, 300));
            countdownText.fontStyle = FontStyles.Bold;
            countdownText.outlineWidth = 0.2f; countdownText.outlineColor = new Color32(20, 22, 28, 255);
            countdownText.gameObject.SetActive(false);

            var tp = UiKit.Rect("Tutorial", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(1150, 118));
            UiKit.Panel(tp, Palette.WithAlpha(Palette.Panel, 0.96f));
            UiKit.Frame(tp, Palette.Oro, 3f);
            tutorialPanel = tp.gameObject;
            tutorialText = UiKit.Text(tp, "Texto", "", 30, Palette.Texto, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(-340, 0));
            tutorialText.textWrappingMode = TextWrappingModes.Normal;
            tutorialButton = UiKit.Button(tp, "Siguiente", "Siguiente [Enter]", 25, new Vector2(1, 0.5f), new Vector2(-160, 0), new Vector2(280, 60), () => TutorialNextClicked?.Invoke(), out tutorialButtonText);
            tutorialPanel.SetActive(false);

            highlightRt = UiKit.Rect("Resaltado", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));
            UiKit.Frame(highlightRt, Palette.Oro, 5f);
            highlightRt.gameObject.SetActive(false);

            cursorRt = UiKit.Rect("EtiquetaCursor", root, Vector2.zero, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(440, 44));
            UiKit.Panel(cursorRt, Palette.WithAlpha(Palette.Panel, 0.9f), false);
            cursorLabel = cursorRt.gameObject;
            cursorText = UiKit.Fill(cursorRt, "Texto", "", 24, Palette.Texto, TextAlignmentOptions.Center);
            UiKit.OneLine(cursorText, 14f);
            cursorLabel.SetActive(false);
        }

        GameObject Modal(string name, Vector2 size, out RectTransform panel, float dim = 0.6f)
        {
            var bg = UiKit.Stretch(name, root);
            UiKit.Panel(bg, new Color(0, 0, 0, dim));
            panel = UiKit.Rect("Panel", bg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            UiKit.Panel(panel, Palette.Panel);
            return bg.gameObject;
        }

        void BuildPause()
        {
            pauseScreen = Modal("Pausa", new Vector2(640, 600), out var p);
            UiKit.Text(p, "Titulo", "PAUSA", 56, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(600, 70));
            UiKit.Button(p, "Reanudar", "Reanudar  [P / Esc]", 28, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(460, 68), () => ResumeClicked?.Invoke(), out _);
            UiKit.Button(p, "Reiniciar", "Reiniciar  [R]", 28, new Vector2(0.5f, 1), new Vector2(0, -236), new Vector2(460, 68), () => RestartClicked?.Invoke(), out _);
            UiKit.Button(p, "Salir", "Salir al inicio  [S]", 28, new Vector2(0.5f, 1), new Vector2(0, -322), new Vector2(460, 68), () => ExitClicked?.Invoke(), out pauseExitLabel);
            UiKit.Text(p, "Volumen", "Volumen de efectos", 24, Palette.Texto2, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(460, 30));
            var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGo.transform.SetParent(p, false);
            var srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1); srt.pivot = new Vector2(0.5f, 1); srt.anchoredPosition = new Vector2(0, -442); srt.sizeDelta = new Vector2(460, 28);
            pauseVolume = sliderGo.GetComponent<Slider>();
            UiKit.StyleSlider(sliderGo);
            pauseVolume.value = PlayerPrefs.GetFloat("cd_volumen", 0.7f);
            pauseVolume.onValueChanged.AddListener(v => VolumeChanged?.Invoke(v));
            var toggleGo = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleGo.transform.SetParent(p, false);
            var trt = (RectTransform)toggleGo.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1); trt.pivot = new Vector2(0.5f, 1); trt.anchoredPosition = new Vector2(0, -494); trt.sizeDelta = new Vector2(460, 40);
            pauseMute = toggleGo.GetComponent<Toggle>();
            pauseMute.isOn = PlayerPrefs.GetInt("cd_silencio", 0) == 1;
            UiKit.StyleToggle(toggleGo);
            UiKit.ReplaceLegacyLabel(toggleGo, "Silenciar", 24);
            pauseMute.onValueChanged.AddListener(v => MuteChanged?.Invoke(v));
            pauseScreen.SetActive(false);
        }

        /// <summary>En la campaña, salir de la pausa vuelve al mapa (UXS-002.1).</summary>
        public void SetExitLabel(string label) { if (pauseExitLabel != null) pauseExitLabel.text = label; }

        public void SyncAudio(float volume, bool muted)
        {
            if (pauseVolume != null) pauseVolume.SetValueWithoutNotify(volume);
            if (pauseMute != null) pauseMute.SetIsOnWithoutNotify(muted);
        }

        void BuildResult()
        {
            resultScreen = Modal("Resultado", new Vector2(1000, 800), out var p);
            p.anchoredPosition = new Vector2(0, 40);
            resTitle = UiKit.Text(p, "Titulo", "", 64, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(960, 80));
            for (int i = 0; i < 3; i++)
            {
                var srt = UiKit.Rect("Estrella" + i, p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 1) * 110, -112), new Vector2(96, 96));
                resStars[i] = srt.gameObject.AddComponent<Image>();
                resStars[i].sprite = UiKit.Star();
                resStars[i].raycastTarget = false;
            }
            resLine = UiKit.Text(p, "Linea", "", 32, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -220), new Vector2(960, 44));
            resRule = UiKit.Text(p, "Regla", "", 22, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -266), new Vector2(960, 32));
            resCause = UiKit.Text(p, "Causa", "", 23, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -302), new Vector2(940, 64));
            resCause.textWrappingMode = TextWrappingModes.Normal;
            resTime = UiKit.Text(p, "Duracion", "", 23, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -370), new Vector2(960, 32));
            resReward = UiKit.Text(p, "Recompensa", "", 25, Palette.Texto, TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -414), new Vector2(940, 150));
            resReward.textWrappingMode = TextWrappingModes.Normal;
            resReward.enableAutoSizing = true; resReward.fontSizeMin = 18; resReward.fontSizeMax = 25;
            resTanda = UiKit.Button(p, "Tanda", "Elegir la mejora de torre base  [M]", 26, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(620, 60), () => TandaClicked?.Invoke(), out _);
            resPrimary = UiKit.Button(p, "Principal", "", 26, new Vector2(0.5f, 0), new Vector2(-310, 100), new Vector2(290, 70), () => { if (CampaignResult && ResultHasNext) NextClicked?.Invoke(); else RetryClicked?.Invoke(); }, out resPrimaryLabel);
            resSecondary = UiKit.Button(p, "Secundario", "", 26, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(290, 70), () => RetryClicked?.Invoke(), out resSecondaryLabel);
            resTertiary = UiKit.Button(p, "Salir", "", 26, new Vector2(0.5f, 0), new Vector2(310, 100), new Vector2(290, 70), () => ExitClicked?.Invoke(), out resTertiaryLabel);
            resFile = UiKit.Text(p, "Archivo", "", 17, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(960, 44));
            resFile.enableAutoSizing = true; resFile.fontSizeMin = 11; resFile.fontSizeMax = 17;
            resultScreen.SetActive(false);
        }

        void BuildStart()
        {
            var bg = UiKit.Stretch("Inicio", root);
            UiKit.Panel(bg, Palette.WithAlpha(Palette.Panel, 0.9f));
            startScreen = bg.gameObject;
            UiKit.Text(bg, "Titulo", "CLASH DEFENSE", 96, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1400, 120)).fontStyle = FontStyles.Bold;
            UiKit.Text(bg, "Sub", "Prototipo 0 · título provisional", 34, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1400, 50));
            UiKit.Text(bg, "Objetivo", "Protegé la base de cinco oleadas combinando torres.", 32, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1400, 50));
            UiKit.Button(bg, "Jugar", "Jugar  [Enter]", 36, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(420, 90), () => PlayClicked?.Invoke(tutorialToggle == null || tutorialToggle.isOn), out _);
            var tg = DefaultControls.CreateToggle(new DefaultControls.Resources());
            tg.transform.SetParent(bg, false);
            var trt = (RectTransform)tg.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0.5f); trt.anchoredPosition = new Vector2(20, -82); trt.sizeDelta = new Vector2(420, 40);
            tutorialToggle = tg.GetComponent<Toggle>();
            UiKit.StyleToggle(tg);
            UiKit.ReplaceLegacyLabel(tg, "Tutorial", 28);
            quitButton = UiKit.Button(bg, "Salir", "Salir  [S]", 30, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(420, 70), () => QuitClicked?.Invoke(), out startQuitLabel).gameObject;
            startNote = UiKit.Text(bg, "Nota", "", 24, Palette.Acento, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(1400, 34));
            startFooter = UiKit.Text(bg, "Pie", "", 22, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1400, 34));
            startScreen.SetActive(false);
        }

        void BuildError()
        {
            var bg = UiKit.Stretch("Error", root);
            UiKit.Panel(bg, Palette.Panel);
            errorScreen = bg.gameObject;
            UiKit.Text(bg, "Titulo", "Los datos del juego no validan", 48, Palette.Vida, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1600, 70));
            errorText = UiKit.Text(bg, "Lista", "", 24, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1500, 700));
            errorText.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Button(bg, "Salir", "Salir  [S]", 30, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(300, 70), () => QuitClicked?.Invoke(), out _);
            errorScreen.SetActive(false);
        }

        void ShowOnly(GameObject screen)
        {
            foreach (var s in new[] { startScreen, errorScreen, pauseScreen, resultScreen })
                if (s != null) s.SetActive(s == screen);
        }

        // ------------------------------------------------------------------ pantallas
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
            quitButton.SetActive(quitIsBack || Application.platform != RuntimePlatform.WebGLPlayer);
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

        public void ShowGame(Match m, string levelName = null)
        {
            match = m;
            seenEnemies.Clear();
            ShowOnly(null);
            hudRoot.SetActive(true);
            HideTowerPanel(); HideCursorLabel(); SetTutorialStep(0);   // un reintento no hereda el cartel del tutorial (revisión técnica, hallazgo 2)
            BuildCards(m.TowerTypes);
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
                foreach (var c in cards)
                {
                    bool afford = match.Gold >= c.Cost;
                    bool small = c.Rt.sizeDelta.x < 230f;
                    c.CostText.text = afford ? $"{c.Cost} oro" : small ? $"{c.Cost} · faltan {c.Cost - match.Gold}" : $"{c.Cost} oro · faltan {c.Cost - match.Gold}";
                    c.CostText.color = afford ? Palette.Oro : Palette.Invalido;
                }
            }
        }

        public void SetCardsInteractable(Func<string, bool> allowed)
        {
            foreach (var c in cards)
            {
                bool ok = allowed == null || allowed(c.Id);
                c.Button.interactable = ok;
                c.Group.alpha = ok ? 1f : 0.45f;
            }
        }

        public void SetSelectedCard(string id)
        {
            foreach (var c in cards) c.Outline.enabled = c.Id == id;
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

        readonly Dictionary<RectTransform, float> pulses = new Dictionary<RectTransform, float>();
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

        void RefreshCollect()
        {
            if (panelTower == null || panelTower.Type.attack != AttackKind.Gold) return;
            int amount = (int)panelTower.Stored;
            tpCollect.interactable = amount > 0;
            tpCollectLabel.text = panelTower.Full ? $"¡Llena! Recoger {amount}  [C]" : amount > 0 ? $"Recoger {amount} de {panelTower.Stats.goldCapacity:0}  [C]" : $"Juntando… 0 de {panelTower.Stats.goldCapacity:0}";
            tpCollectLabel.color = panelTower.Full ? Palette.Oro : Palette.Texto;
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
        public void SetCursorLabel(string text, Color color, Vector2 screenPos)
        {
            cursorLabel.SetActive(true);
            cursorText.text = text;
            cursorText.color = color;
            float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            var pos = screenPos / scale + new Vector2(26, -22);
            var area = root.rect.size; var size = cursorRt.sizeDelta;
            if (pos.x + size.x > area.x - 8f) pos.x = screenPos.x / scale - 26f - size.x;
            pos.x = Mathf.Clamp(pos.x, 8f, Mathf.Max(8f, area.x - size.x - 8f));
            pos.y = Mathf.Clamp(pos.y, size.y + 8f, area.y - 8f);
            cursorRt.anchoredPosition = pos;
        }

        public void HideCursorLabel() { if (cursorLabel != null) cursorLabel.SetActive(false); }

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
                case 4: { var c = cards.Find(k => k.Id == towerId); int idx = cards.IndexOf(c); text = $"Elegí la {towerName}: clic en su tarjeta o tecla [{KeyLabel(Mathf.Max(0, idx))}]."; button = false; highlightTarget = c != null ? c.Rt : null; break; }
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
            if (toast != null && toast.activeSelf && Time.time > toastUntil)
            {
                if (toastQueue.Count > 0) { var n = toastQueue.Dequeue(); toastText.text = n.text; toastUntil = Time.time + n.seconds; }
                else toast.SetActive(false);
            }

            if (countdownText != null && countdownText.gameObject.activeSelf)
            {
                float t = Time.time - countdownStart;   // tiempo escalado: la pausa congela la cuenta (SOL-001 D11)
                bool defense = countdownLabel == "DEFENSE";
                float dur = Mathf.Max(0.05f, defense ? timing.defenseDuration : timing.countdownStep);   // del balance, no del código
                float fade = dur * 0.25f;                                                                // GDS-001.6: 0,2 entra · 0,4 queda · 0,2 sale sobre 0,8
                if (t >= dur) countdownText.gameObject.SetActive(false);
                else
                {
                    countdownText.text = countdownLabel;
                    float a = defense ? 1f - t / dur : (t < fade ? t / fade : t > dur - fade ? 1f - (t - (dur - fade)) / fade : 1f);
                    var c = defense ? Palette.Oro : Palette.Texto; c.a = Mathf.Clamp01(a);
                    countdownText.color = c;
                    countdownText.rectTransform.localScale = Vector3.one * (defense ? Mathf.Lerp(1f, 1.6f, t / dur) : 1f);
                }
            }

            // barra del miniboss: cambia por cuadro (UXS-002.5, frecuencia declarada)
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
            if (panelTower != null && panelTower.Type.attack == AttackKind.Gold && towerPanel.activeSelf && Time.frameCount % 10 == 0) RefreshCollect();

            var done = new List<RectTransform>();
            foreach (var kv in pulses)
            {
                float t = (now - kv.Value) / 0.15f;
                if (kv.Key == null) { done.Add(kv.Key); continue; }
                if (t >= 1f) { kv.Key.localScale = Vector3.one; done.Add(kv.Key); }
                else kv.Key.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
            }
            foreach (var d in done) pulses.Remove(d);

            if (highlightTarget != null && highlightRt.gameObject.activeSelf)
            {
                var corners = new Vector3[4];
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
