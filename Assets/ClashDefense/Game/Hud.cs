using System;
using System.Collections.Generic;
using System.Text;
using ClashDefense.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>
    /// Pantallas y HUD de UXS-001.6 (con los complementos .1 … .5). Muestra y comunica; no decide reglas:
    /// todo lo que el jugador hace sale por los eventos C# de abajo y lo resuelve GameBootstrap contra el Match.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        // ---- lo que el jugador pide (lo atiende GameBootstrap)
        public event Action<bool> PlayClicked;
        public event Action QuitClicked, ResumeClicked, RestartClicked, ExitClicked, RetryClicked, TutorialNextClicked, UpgradeClicked, SellClicked, PauseClicked;
        public event Action<string> CardClicked;
        public event Action<bool> UpgradeHover;
        public event Action<float> VolumeChanged;
        public event Action<bool> MuteChanged;

        Canvas canvas;
        RectTransform root;
        // pantallas
        GameObject startScreen, errorScreen, hudRoot, pauseScreen, resultScreen;
        TextMeshProUGUI errorText, startFooter;
        Toggle tutorialToggle;
        GameObject quitButton;
        // HUD
        TextMeshProUGUI vidaText, oroText, oleadaText;
        RectTransform vidaRt, oroRt, oleadaRt;
        readonly List<Card> cards = new List<Card>();
        // panel de torre
        GameObject towerPanel;
        TextMeshProUGUI tpTitle, tpStats, tpTargets, tpPreview, tpN3, tpUpgradeLabel, tpSellLabel;
        Button tpUpgrade, tpSell;
        // avisos
        GameObject toast; TextMeshProUGUI toastText; float toastUntil;
        TextMeshProUGUI hintText;
        TextMeshProUGUI countdownText; string countdownLabel; float countdownStart;
        GameObject tutorialPanel; TextMeshProUGUI tutorialText; Button tutorialButton; TextMeshProUGUI tutorialButtonText;
        RectTransform highlightRt; RectTransform highlightTarget;
        GameObject cursorLabel; TextMeshProUGUI cursorText; RectTransform cursorRt;
        // resultado
        TextMeshProUGUI resTitle, resLine, resRule, resCause, resTime, resFile;
        readonly Image[] resStars = new Image[3];
        // estado
        Match match;
        int lastGold = int.MinValue;
        readonly HashSet<string> seenEnemies = new HashSet<string>();
        public bool TutorialStepNeedsButton { get; private set; }

        sealed class Card
        {
            public string Id; public int Cost; public Button Button; public Image Bg, Stripe; public Outline Outline;
            public TextMeshProUGUI Name, CostText, Targets; public RectTransform Rt; public CanvasGroup Group;
        }

        // ------------------------------------------------------------------ construcción
        TimingData timing;

        public void Build(BalanceData balance)
        {
            timing = balance.timing;
            canvas = UiKit.Canvas(transform, 10);
            root = (RectTransform)canvas.transform;
            BuildHud(balance);
            BuildTowerPanel();
            BuildOverlays();
            BuildPause();
            BuildResult();
            BuildStart();
            BuildError();
            ShowOnly(null);
        }

        void BuildHud(BalanceData balance)
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
            UiKit.Button(top, "Pausa", "Pausa  [P / Esc]", 26, new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(280, 56), () => PauseClicked?.Invoke(), out _);

            var bottom = UiKit.Rect("FranjaInferior", hudRoot.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 151));
            UiKit.Panel(bottom, Palette.WithAlpha(Palette.Panel, 0.94f));
            hintText = UiKit.Text(bottom, "Ayuda", "", 21, Palette.Texto2, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(360, 0));
            hintText.textWrappingMode = TextWrappingModes.Normal;
            int n = balance.towers.Length;
            float w = 340f, gap = 22f, total = n * w + (n - 1) * gap;
            for (int i = 0; i < n; i++)
            {
                var t = balance.towers[i];
                var c = new Card { Id = t.id, Cost = t.cost };
                float x = -total * 0.5f + w * 0.5f + i * (w + gap);
                c.Button = UiKit.Button(bottom, "Tarjeta_" + t.id, "", 20, new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(w, 128), null, out var dummy);
                dummy.gameObject.SetActive(false);
                c.Rt = (RectTransform)c.Button.transform;
                c.Bg = c.Button.GetComponent<Image>();
                c.Group = c.Button.gameObject.AddComponent<CanvasGroup>();
                c.Outline = UiKit.Outline(c.Button.gameObject, Palette.Acento, 4f); c.Outline.enabled = false;
                var stripe = UiKit.Rect("Familia", c.Rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 10));
                c.Stripe = UiKit.Panel(stripe, Palette.ForTower(t.id), false);
                UiKit.Text(c.Rt, "Tecla", $"[{i + 1}]", 24, Palette.Texto2, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -16), new Vector2(60, 30));
                c.Name = UiKit.Text(c.Rt, "Nombre", t.displayName, 28, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(-80, 36));
                c.CostText = UiKit.Text(c.Rt, "Costo", $"{t.cost} oro", 26, Palette.Oro, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(16, -6), new Vector2(-32, 32));
                c.Targets = UiKit.Text(c.Rt, "Objetivos", TargetsLine(t), 20, Palette.Texto2, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(16, 12), new Vector2(-32, 30));
                UiKit.OneLine(c.Targets, 14f);
                string id = t.id;
                c.Button.onClick.AddListener(() => CardClicked?.Invoke(id));
                cards.Add(c);
            }
        }

        public static string TargetsLine(TowerTypeData t)
        {
            string layers = t.targetsAir && t.targetsGround ? "Tierra y aire" : t.targetsAir ? "Solo aire" : "Solo tierra";
            string metal = t.metalEfficiency <= 0f ? "Metal: no lo daña" : t.metalEfficiency >= 0.999f ? "Metal: lo rompe" : $"Metal: lento ({Mathf.RoundToInt(t.metalEfficiency * 100)} %)";
            string area = t.attack == "area" ? " · Área" : "";
            return $"{layers}{area} · {metal}";
        }

        void BuildTowerPanel()
        {
            var rt = UiKit.Rect("PanelTorre", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -172), new Vector2(500, 360));
            UiKit.Panel(rt, Palette.WithAlpha(Palette.Panel, 0.96f));
            towerPanel = rt.gameObject;
            tpTitle = UiKit.Text(rt, "Titulo", "", 30, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(-40, 40));
            tpStats = UiKit.Text(rt, "Estadisticas", "", 24, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -62), new Vector2(-40, 32));
            tpTargets = UiKit.Text(rt, "Objetivos", "", 21, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -96), new Vector2(-40, 30));
            tpUpgrade = UiKit.Button(rt, "Mejorar", "", 23, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(460, 56), () => UpgradeClicked?.Invoke(), out tpUpgradeLabel);
            var hover = tpUpgrade.gameObject.AddComponent<HoverRelay>();
            hover.Enter = () => { tpPreview.gameObject.SetActive(true); UpgradeHover?.Invoke(true); };
            hover.Exit = () => { if (tpPreview != null) tpPreview.gameObject.SetActive(false); UpgradeHover?.Invoke(false); };
            tpPreview = UiKit.Text(rt, "Preview", "", 21, Palette.Acento, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -194), new Vector2(-40, 30));
            tpPreview.gameObject.SetActive(false);
            tpSell = UiKit.Button(rt, "Vender", "", 23, new Vector2(0.5f, 1), new Vector2(0, -256), new Vector2(460, 56), () => SellClicked?.Invoke(), out tpSellLabel);
            tpN3 = UiKit.Text(rt, "N3", "Nivel 3 · bloqueado · próximamente", 21, Palette.Bloqueo, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(20, 36), new Vector2(-40, 28));
            UiKit.Text(rt, "Cerrar", "Cerrar [Esc]", 19, Palette.Texto2, TextAlignmentOptions.MidlineRight, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 10), new Vector2(200, 24));
            towerPanel.SetActive(false);
        }

        void BuildOverlays()
        {
            var t = UiKit.Rect("Aviso", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(1180, 64));
            UiKit.Panel(t, Palette.WithAlpha(Palette.Panel, 0.92f), false);
            UiKit.Frame(t, Palette.Oro, 3f);
            toast = t.gameObject;
            toastText = UiKit.Fill(t, "Texto", "", 27, Palette.Texto, TextAlignmentOptions.Center);
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
            tutorialButton = UiKit.Button(tp, "Siguiente", "Siguiente [Enter]", 25, new Vector2(1, 0.5f), new Vector2(-160, 0), new Vector2(280, 60), () => TutorialNextClicked?.Invoke(), out tutorialButtonText);
            tutorialPanel.SetActive(false);

            highlightRt = UiKit.Rect("Resaltado", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));
            UiKit.Frame(highlightRt, Palette.Oro, 5f);
            highlightRt.gameObject.SetActive(false);

            cursorRt = UiKit.Rect("EtiquetaCursor", root, Vector2.zero, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(420, 44));
            UiKit.Panel(cursorRt, Palette.WithAlpha(Palette.Panel, 0.9f), false);
            cursorLabel = cursorRt.gameObject;
            cursorText = UiKit.Fill(cursorRt, "Texto", "", 24, Palette.Texto, TextAlignmentOptions.Center);
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
            UiKit.Button(p, "Salir", "Salir al inicio  [S]", 28, new Vector2(0.5f, 1), new Vector2(0, -322), new Vector2(460, 68), () => ExitClicked?.Invoke(), out _);
            UiKit.Text(p, "Volumen", "Volumen de efectos", 24, Palette.Texto2, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(460, 30));
            var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGo.transform.SetParent(p, false);
            var srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1); srt.pivot = new Vector2(0.5f, 1); srt.anchoredPosition = new Vector2(0, -442); srt.sizeDelta = new Vector2(460, 28);
            var slider = sliderGo.GetComponent<Slider>();
            slider.value = PlayerPrefs.GetFloat("cd_volumen", 0.7f);
            slider.onValueChanged.AddListener(v => VolumeChanged?.Invoke(v));
            var toggleGo = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleGo.transform.SetParent(p, false);
            var trt = (RectTransform)toggleGo.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1); trt.pivot = new Vector2(0.5f, 1); trt.anchoredPosition = new Vector2(0, -494); trt.sizeDelta = new Vector2(460, 40);
            var toggle = toggleGo.GetComponent<Toggle>();
            toggle.isOn = PlayerPrefs.GetInt("cd_silencio", 0) == 1;
            UiKit.StyleToggle(toggleGo);
            ReplaceLegacyLabel(toggleGo, "Silenciar", 24);
            toggle.onValueChanged.AddListener(v => MuteChanged?.Invoke(v));
            pauseScreen.SetActive(false);
        }

        static void ReplaceLegacyLabel(GameObject control, string text, float size)
        {
            var legacy = control.GetComponentInChildren<Text>(true);
            if (legacy == null) return;
            var go = legacy.gameObject;
            var rt = (RectTransform)go.transform;
            UnityEngine.Object.DestroyImmediate(legacy);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = Palette.Texto; t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(44, 0); rt.offsetMax = Vector2.zero;
        }

        void BuildResult()
        {
            resultScreen = Modal("Resultado", new Vector2(900, 700), out var p);
            resTitle = UiKit.Text(p, "Titulo", "", 64, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(860, 80));
            for (int i = 0; i < 3; i++)
            {
                var srt = UiKit.Rect("Estrella" + i, p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 1) * 110, -120), new Vector2(96, 96));
                resStars[i] = srt.gameObject.AddComponent<Image>();
                resStars[i].sprite = UiKit.Star();
                resStars[i].raycastTarget = false;
            }
            resLine = UiKit.Text(p, "Linea", "", 32, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -236), new Vector2(860, 44));
            resRule = UiKit.Text(p, "Regla", "", 24, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -286), new Vector2(860, 36));
            resCause = UiKit.Text(p, "Causa", "", 25, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(860, 80));
            resTime = UiKit.Text(p, "Duracion", "", 26, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -420), new Vector2(860, 36));
            UiKit.Button(p, "Reintentar", "Reintentar  [Enter]", 28, new Vector2(0.5f, 0), new Vector2(-190, 110), new Vector2(340, 70), () => RetryClicked?.Invoke(), out _);
            UiKit.Button(p, "Salir", "Salir al inicio  [Esc]", 28, new Vector2(0.5f, 0), new Vector2(190, 110), new Vector2(340, 70), () => ExitClicked?.Invoke(), out _);
            resFile = UiKit.Text(p, "Archivo", "", 18, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(860, 50));
            resFile.enableAutoSizing = true; resFile.fontSizeMin = 12; resFile.fontSizeMax = 18;
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
            ReplaceLegacyLabel(tg, "Tutorial", 28);
            quitButton = UiKit.Button(bg, "Salir", "Salir  [S]", 30, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(300, 70), () => QuitClicked?.Invoke(), out _).gameObject;
            quitButton.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
            startFooter = UiKit.Text(bg, "Pie", "", 22, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1400, 34));
            startScreen.SetActive(false);
        }

        void BuildError()
        {
            var bg = UiKit.Stretch("Error", root);
            UiKit.Panel(bg, Palette.Panel);
            errorScreen = bg.gameObject;
            UiKit.Text(bg, "Titulo", "Los datos del balance no validan", 48, Palette.Vida, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1600, 70));
            errorText = UiKit.Text(bg, "Lista", "", 24, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1500, 700));
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
        public bool ErrorVisible => errorScreen.activeSelf;

        public void ShowStart(bool tutorialDefault, string footer)
        {
            ShowOnly(startScreen);
            hudRoot.SetActive(false);
            HideTowerPanel(); HideCursorLabel(); SetTutorialStep(0);
            countdownText.gameObject.SetActive(false); toast.SetActive(false);
            tutorialToggle.isOn = tutorialDefault;
            startFooter.text = footer;
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

        public void ShowGame(Match m)
        {
            match = m;
            seenEnemies.Clear();
            ShowOnly(null);
            hudRoot.SetActive(true);
            HideTowerPanel(); HideCursorLabel();
            lastGold = int.MinValue;
            RefreshStats();
            oleadaText.text = $"OLEADA 0/{m.WaveCount}"; oleadaText.color = Palette.Texto;
            SetSelectedCard(null);
        }

        public void ShowPause(bool on)
        {
            if (on) { ShowOnly(pauseScreen); HideCursorLabel(); }
            else if (pauseScreen.activeSelf) ShowOnly(null);
        }

        public void ShowResult(MatchReport r, Match m, string file)
        {
            ShowOnly(resultScreen);
            HideTowerPanel(); HideCursorLabel(); SetTutorialStep(0);
            toast.SetActive(false); countdownText.gameObject.SetActive(false);
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
        }

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
                    c.CostText.text = afford ? $"{c.Cost} oro" : $"{c.Cost} oro · faltan {c.Cost - match.Gold}";
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
                    if (seenEnemies.Add(e.Text))
                        foreach (var t in match.Balance.enemies)
                            if (t.id == e.Text) { ShowToast($"Nuevo: {t.displayName} — {t.counterHint}", 4f); break; }
                    break;
            }
        }

        readonly Dictionary<RectTransform, float> pulses = new Dictionary<RectTransform, float>();
        void Pulse(RectTransform rt) { if (rt != null) pulses[rt] = Time.unscaledTime; }

        public void ShowToast(string text, float seconds)
        {
            toastText.text = text;
            toast.SetActive(true);
            toastUntil = Time.time + seconds;
        }

        // ------------------------------------------------------------------ panel de torre (UXS-001.3)
        public bool TowerPanelVisible => towerPanel.activeSelf;

        public void ShowTowerPanel(Tower t, Match m, bool sellArmed)
        {
            towerPanel.SetActive(true);
            var s = t.Stats;
            tpTitle.text = $"{t.Type.displayName} · Nivel {t.Level}";
            tpStats.text = StatsLine(t.Type, s);
            tpTargets.text = TargetsLine(t.Type);
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
            tpSellLabel.text = sellArmed ? $"¿Seguro? Vender +{refund}  [V]" : $"Vender +{refund}  [V]";
            tpSellLabel.color = sellArmed ? Palette.Invalido : Palette.Texto;
            tpN3.text = t.CanUpgrade ? "Nivel 3 · bloqueado · próximamente" : "Nivel 2 es el máximo del prototipo · Nivel 3 próximamente";
        }

        /// <summary>UXS-001.3: "Daño 25→30 · 0,65→0,55 s · alcance 8→9" (y el área si la torre la tiene).</summary>
        static string DeltaLine(TowerTypeData type, TowerLevelData a, TowerLevelData b)
        {
            string area = type.attack == "area" ? $" · área {UiKit.Num(a.areaRadius)}→{UiKit.Num(b.areaRadius)}" : "";
            return $"Daño {a.damage}→{b.damage} · {UiKit.Num(a.interval)}→{UiKit.Num(b.interval)} s · alcance {UiKit.Num(a.range)}→{UiKit.Num(b.range)}{area}";
        }

        static string StatsLine(TowerTypeData type, TowerLevelData s)
        {
            string area = type.attack == "area" ? $" · área {UiKit.Num(s.areaRadius)}" : "";
            return $"Daño {s.damage} · cada {UiKit.Num(s.interval)} s · alcance {UiKit.Num(s.range)}{area}";
        }

        /// <summary>Línea de ayuda de la franja inferior (UXS-001.6: Partida y Colocando).</summary>
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
                case 4: { var c = cards.Find(k => k.Id == towerId); int idx = cards.IndexOf(c) + 1; text = $"Elegí la {towerName}: clic en su tarjeta o tecla [{Mathf.Max(1, idx)}]."; button = false; highlightTarget = c != null ? c.Rt : null; break; }
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
            if (toast != null && toast.activeSelf && Time.time > toastUntil) toast.SetActive(false);

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
