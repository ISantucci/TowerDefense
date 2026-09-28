using System.IO;
using ClashDefense.Game;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Genera el prefab del HUD (TL-003) con la misma disposición que antes se armaba en código al arrancar (Hud de TL-002,
    /// UXS-001.6, UXS-002.4/.5). De acá en adelante el HUD es un prefab: se abre, se mueve un texto o un botón con el mouse y
    /// el código no cambia (el código solo enlaza por referencias del Inspector).
    /// Los textos que cambian seguido (vida, oro, oleada, cuenta, cursor, barra del miniboss) llevan su propio Canvas: al
    /// cambiar no obligan a reconstruir el resto de la interfaz (Unity: dividir canvases por frecuencia de cambio).
    /// </summary>
    static class HudBuilder
    {
        public const string UiRoot = "Assets/ClashDefense/UI";
        public const string StarPath = UiRoot + "/Sprites/Estrella.png";

        static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        // ------------------------------------------------------------------ recursos
        /// <summary>La estrella del resultado y del mapa como sprite (antes se dibujaba en cada arranque).</summary>
        public static Sprite Star()
        {
            if (!File.Exists(StarPath))
            {
                AssetUtil.EnsureFolder(Path.GetDirectoryName(StarPath));
                var src = UiKit.Star().texture;
                File.WriteAllBytes(StarPath, src.EncodeToPNG());
                AssetDatabase.ImportAsset(StarPath, ImportAssetOptions.ForceUpdate);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(StarPath);
            if (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
        }

        /// <summary>Canvas propio para lo que cambia seguido (sin raycaster: solo muestra).</summary>
        static void OwnCanvas(Component c) { if (c.GetComponent<Canvas>() == null) c.gameObject.AddComponent<Canvas>(); }

        static void Outline(TextMeshProUGUI t, float width)
        {
            var m = MaterialLibrary.TmpOutline(width);
            if (m != null) t.fontSharedMaterial = m;
        }

        public static Toggle Toggle(Transform parent, string name, string label, float size, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box)
        {
            var tg = DefaultControls.CreateToggle(new DefaultControls.Resources());
            tg.name = name;
            tg.transform.SetParent(parent, false);
            var trt = (RectTransform)tg.transform;
            trt.anchorMin = trt.anchorMax = anchor; trt.pivot = pivot; trt.anchoredPosition = pos; trt.sizeDelta = box;
            UiKit.StyleToggle(tg);
            UiKit.ReplaceLegacyLabel(tg, label, size);
            return tg.GetComponent<Toggle>();
        }

        public static Slider Slider(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            var srt = (RectTransform)go.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1); srt.pivot = new Vector2(0.5f, 1); srt.anchoredPosition = pos; srt.sizeDelta = size;
            UiKit.StyleSlider(go);
            var s = go.GetComponent<Slider>();
            s.value = 0.7f;
            return s;
        }

        /// <summary>Botón sin texto propio (la tarjeta pone los suyos): se saca la etiqueta que trae UiKit.Button.</summary>
        public static Button BareButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var b = UiKit.Button(parent, name, "", 20, anchor, pos, size, null, out var label);
            Object.DestroyImmediate(label.gameObject);
            return b;
        }

        // ------------------------------------------------------------------ tarjeta de torre
        public static TowerCardView Card()
        {
            var holder = new GameObject("tmp", typeof(RectTransform));
            var b = BareButton(holder.transform, "TarjetaTorre", C, Vector2.zero, new Vector2(300, 128));
            var rt = (RectTransform)b.transform;
            rt.SetParent(null, false);
            Object.DestroyImmediate(holder);
            var v = b.gameObject.AddComponent<TowerCardView>();
            v.button = b;
            v.group = b.gameObject.AddComponent<CanvasGroup>();
            v.selection = UiKit.Outline(b.gameObject, Palette.Acento, 4f);
            v.selection.enabled = false;
            var stripe = UiKit.Rect("Familia", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 10));
            v.stripe = UiKit.Panel(stripe, Palette.Bloqueo, false);
            v.keyText = UiKit.Text(rt, "Tecla", "[1]", 24, Palette.Texto2, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -16), new Vector2(50, 30));
            v.nameText = UiKit.Text(rt, "Nombre", "Torre", 28, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(12, -16), new Vector2(-60, 34));
            UiKit.OneLine(v.nameText, 15f);
            v.costText = UiKit.Text(rt, "Costo", "100 oro", 26, Palette.Oro, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(12, -6), new Vector2(-20, 32));
            UiKit.OneLine(v.costText, 13f);
            v.targetsText = UiKit.Text(rt, "Objetivos", "Tierra y aire", 20, Palette.Texto2, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(12, 10), new Vector2(-20, 30));
            UiKit.OneLine(v.targetsText, 11f);
            v.hover = b.gameObject.AddComponent<HoverRelay>();
            v.compactWidth = 230f;
            return AssetUtil.SavePrefab<TowerCardView>(b.gameObject, $"{UiRoot}/Prefabs/TarjetaTorre.prefab");
        }

        // ------------------------------------------------------------------ HUD
        public static Hud Build(TowerCardView cardPrefab, Sprite star)
        {
            var go = new GameObject("Hud");
            var h = go.AddComponent<Hud>();
            h.canvas = UiKit.Canvas(go.transform, 10);
            var root = (RectTransform)h.canvas.transform;
            BuildHud(h, root, cardPrefab);
            BuildTowerPanel(h, root);
            BuildOverlays(h, root);
            BuildPause(h, root);
            BuildResult(h, root, star);
            BuildStart(h, root);
            BuildError(h, root);
            foreach (var s in new[] { h.startScreen, h.errorScreen, h.pauseScreen, h.resultScreen, h.hudRoot }) s.SetActive(false);
            return AssetUtil.SavePrefab<Hud>(go, $"{UiRoot}/Prefabs/Hud.prefab");
        }

        static void BuildHud(Hud h, RectTransform root, TowerCardView cardPrefab)
        {
            h.hudRoot = UiKit.Stretch("HUD", root).gameObject;
            var top = UiKit.Rect("FranjaSuperior", h.hudRoot.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 76));
            UiKit.Panel(top, Palette.WithAlpha(Palette.Panel, 0.94f));
            h.vidaText = UiKit.Text(top, "Vida", "VIDA 100/100", 36, Palette.Vida, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(320, 0));
            h.oroText = UiKit.Text(top, "Oro", "ORO 100", 36, Palette.Oro, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(390, 0), new Vector2(300, 0));
            h.oleadaText = UiKit.Text(top, "Oleada", "OLEADA 0/5", 34, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 0));
            OwnCanvas(h.vidaText); OwnCanvas(h.oroText); OwnCanvas(h.oleadaText);
            h.levelText = UiKit.Text(top, "Nivel", "", 22, Palette.Texto2, TextAlignmentOptions.MidlineRight, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-330, 0), new Vector2(400, 0));
            UiKit.OneLine(h.levelText, 15f);
            h.pauseButton = UiKit.Button(top, "Pausa", "Pausa  [P / Esc]", 26, new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(280, 56), null, out _);

            h.bottomBand = UiKit.Rect("FranjaInferior", h.hudRoot.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 151));
            UiKit.Panel(h.bottomBand, Palette.WithAlpha(Palette.Panel, 0.94f));
            h.cardPrefab = cardPrefab;
            h.hintText = UiKit.Text(h.hudRoot.transform, "Ayuda", "", 21, Palette.Texto, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 160), new Vector2(900, 30));
            Outline(h.hintText, 0.2f);

            // ayuda de la tarjeta (UXS-002.4, ley 5: lo que hace una torre está escrito antes de pagarla)
            var tooltipRt = UiKit.Rect("AyudaTorre", h.hudRoot.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0), Vector2.zero, new Vector2(560, 150));
            UiKit.Panel(tooltipRt, Palette.WithAlpha(Palette.Panel, 0.97f), false);
            UiKit.Frame(tooltipRt, Palette.Acento, 2f);
            h.tooltip = tooltipRt.gameObject;
            h.tooltipTitle = UiKit.Text(tooltipRt, "Titulo", "", 26, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -10), new Vector2(-32, 34));
            h.tooltipBody = UiKit.Text(tooltipRt, "Texto", "", 20, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -46), new Vector2(-32, -56));
            h.tooltipBody.textWrappingMode = TextWrappingModes.Normal;
            h.tooltip.SetActive(false);

            // barra del miniboss (UXS-002.5): cambia por cuadro, en su propio canvas
            var bb = UiKit.Rect("Miniboss", h.hudRoot.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(760, 64));
            OwnCanvas(bb);
            UiKit.Panel(bb, Palette.WithAlpha(Palette.Panel, 0.92f), false);
            UiKit.Frame(bb, Palette.Vida, 3f);
            h.bossBar = bb.gameObject;
            h.bossName = UiKit.Text(bb, "Nombre", "", 22, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -4), new Vector2(-28, 28));
            var hpBg = UiKit.Rect("VidaFondo", bb, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(14, 10), new Vector2(-28, 16));
            UiKit.Panel(hpBg, Palette.Zocalo, false);
            h.bossHpFill = UiKit.Rect("Vida", hpBg, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Panel(h.bossHpFill, Palette.Vida, false);
            var arBg = UiKit.Rect("MetalFondo", bb, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(14, 28), new Vector2(-28, 6));
            UiKit.Panel(arBg, Palette.Zocalo, false);
            h.bossArmorFill = UiKit.Rect("Metal", arBg, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Panel(h.bossArmorFill, Palette.Metal, false);
            h.bossArmorRoot = arBg.gameObject;
            h.bossBar.SetActive(false);
        }

        static void BuildTowerPanel(Hud h, RectTransform root)
        {
            var rt = UiKit.Rect("PanelTorre", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -172), new Vector2(520, 430));
            UiKit.Panel(rt, Palette.WithAlpha(Palette.Panel, 0.96f));
            h.towerPanel = rt.gameObject;
            h.tpTitle = UiKit.Text(rt, "Titulo", "", 30, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(-40, 40));
            h.tpStats = UiKit.Text(rt, "Estadisticas", "", 22, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -60), new Vector2(-40, 56));
            h.tpStats.textWrappingMode = TextWrappingModes.Normal;
            h.tpTargets = UiKit.Text(rt, "Objetivos", "", 20, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -120), new Vector2(-40, 28));
            h.tpCollect = UiKit.Button(rt, "Recoger", "", 23, new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(480, 52), null, out h.tpCollectLabel);
            h.tpUpgrade = UiKit.Button(rt, "Mejorar", "", 23, new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(480, 56), null, out h.tpUpgradeLabel);
            h.tpUpgradeHover = h.tpUpgrade.gameObject.AddComponent<HoverRelay>();
            h.tpPreview = UiKit.Text(rt, "Preview", "", 20, Palette.Acento, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -272), new Vector2(-40, 28));
            UiKit.OneLine(h.tpPreview, 13f);
            h.tpPreview.gameObject.SetActive(false);
            h.tpSell = UiKit.Button(rt, "Vender", "", 23, new Vector2(0.5f, 1), new Vector2(0, -334), new Vector2(480, 56), null, out h.tpSellLabel);
            h.tpN3 = UiKit.Text(rt, "N3", "Nivel 3 · bloqueado · próximamente", 20, Palette.Bloqueo, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(20, 34), new Vector2(-40, 26));
            UiKit.Text(rt, "Cerrar", "Cerrar [Esc]", 19, Palette.Texto2, TextAlignmentOptions.MidlineRight, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 8), new Vector2(200, 24));
            h.towerPanel.SetActive(false);
        }

        static void BuildOverlays(Hud h, RectTransform root)
        {
            var t = UiKit.Rect("Aviso", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(1320, 64));
            UiKit.Panel(t, Palette.WithAlpha(Palette.Panel, 0.92f), false);
            UiKit.Frame(t, Palette.Oro, 3f);
            h.toast = t.gameObject;
            h.toastText = UiKit.Fill(t, "Texto", "", 26, Palette.Texto, TextAlignmentOptions.Center);
            UiKit.OneLine(h.toastText, 16f);
            h.toast.SetActive(false);

            h.countdownText = UiKit.Text(root, "Cuenta", "", 190, Palette.Texto, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 40), new Vector2(1400, 300));
            h.countdownText.fontStyle = FontStyles.Bold;
            Outline(h.countdownText, 0.2f);
            OwnCanvas(h.countdownText);
            h.countdownText.gameObject.SetActive(false);

            var tp = UiKit.Rect("Tutorial", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(1150, 118));
            UiKit.Panel(tp, Palette.WithAlpha(Palette.Panel, 0.96f));
            UiKit.Frame(tp, Palette.Oro, 3f);
            h.tutorialPanel = tp.gameObject;
            h.tutorialText = UiKit.Text(tp, "Texto", "", 30, Palette.Texto, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(-340, 0));
            h.tutorialText.textWrappingMode = TextWrappingModes.Normal;
            h.tutorialButton = UiKit.Button(tp, "Siguiente", "Siguiente [Enter]", 25, new Vector2(1, 0.5f), new Vector2(-160, 0), new Vector2(280, 60), null, out h.tutorialButtonText);
            h.tutorialPanel.SetActive(false);

            h.highlightRt = UiKit.Rect("Resaltado", root, C, C, C, Vector2.zero, new Vector2(100, 100));
            UiKit.Frame(h.highlightRt, Palette.Oro, 5f);
            h.highlightRt.gameObject.SetActive(false);

            var cursorRt = UiKit.Rect("EtiquetaCursor", root, Vector2.zero, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(440, 44));
            OwnCanvas(cursorRt);
            UiKit.Panel(cursorRt, Palette.WithAlpha(Palette.Panel, 0.9f), false);
            h.cursorLabel = cursorRt.gameObject;
            h.cursorText = UiKit.Fill(cursorRt, "Texto", "", 24, Palette.Texto, TextAlignmentOptions.Center);
            UiKit.OneLine(h.cursorText, 14f);
            h.cursorLabel.SetActive(false);
        }

        static GameObject Modal(RectTransform root, string name, Vector2 size, out RectTransform panel, float dim = 0.6f)
        {
            var bg = UiKit.Stretch(name, root);
            UiKit.Panel(bg, new Color(0, 0, 0, dim));
            panel = UiKit.Rect("Panel", bg, C, C, C, Vector2.zero, size);
            UiKit.Panel(panel, Palette.Panel);
            return bg.gameObject;
        }

        static void BuildPause(Hud h, RectTransform root)
        {
            h.pauseScreen = Modal(root, "Pausa", new Vector2(640, 600), out var p);
            UiKit.Text(p, "Titulo", "PAUSA", 56, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(600, 70));
            h.pauseResume = UiKit.Button(p, "Reanudar", "Reanudar  [P / Esc]", 28, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(460, 68), null, out _);
            h.pauseRestart = UiKit.Button(p, "Reiniciar", "Reiniciar  [R]", 28, new Vector2(0.5f, 1), new Vector2(0, -236), new Vector2(460, 68), null, out _);
            h.pauseExit = UiKit.Button(p, "Salir", "Salir al inicio  [S]", 28, new Vector2(0.5f, 1), new Vector2(0, -322), new Vector2(460, 68), null, out h.pauseExitLabel);
            UiKit.Text(p, "Volumen", "Volumen de efectos", 24, Palette.Texto2, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(460, 30));
            h.pauseVolume = Slider(p, "BarraVolumen", new Vector2(0, -442), new Vector2(460, 28));
            h.pauseMute = Toggle(p, "Silenciar", "Silenciar", 24, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -494), new Vector2(460, 40));
            h.pauseMute.isOn = false;
            h.pauseScreen.SetActive(false);
        }

        static void BuildResult(Hud h, RectTransform root, Sprite star)
        {
            h.resultScreen = Modal(root, "Resultado", new Vector2(1000, 800), out var p);
            p.anchoredPosition = new Vector2(0, 40);
            var top = new Vector2(0.5f, 1);
            h.resTitle = UiKit.Text(p, "Titulo", "", 64, Palette.Texto, TextAlignmentOptions.Center, top, top, top, new Vector2(0, -24), new Vector2(960, 80));
            h.resStars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var srt = UiKit.Rect("Estrella" + i, p, top, top, top, new Vector2((i - 1) * 110, -112), new Vector2(96, 96));
                h.resStars[i] = srt.gameObject.AddComponent<Image>();
                h.resStars[i].sprite = star;
                h.resStars[i].raycastTarget = false;
            }
            h.resLine = UiKit.Text(p, "Linea", "", 32, Palette.Texto, TextAlignmentOptions.Center, top, top, top, new Vector2(0, -220), new Vector2(960, 44));
            h.resRule = UiKit.Text(p, "Regla", "", 22, Palette.Texto2, TextAlignmentOptions.Center, top, top, top, new Vector2(0, -266), new Vector2(960, 32));
            h.resCause = UiKit.Text(p, "Causa", "", 23, Palette.Texto, TextAlignmentOptions.Center, top, top, top, new Vector2(0, -302), new Vector2(940, 64));
            h.resCause.textWrappingMode = TextWrappingModes.Normal;
            h.resTime = UiKit.Text(p, "Duracion", "", 23, Palette.Texto2, TextAlignmentOptions.Center, top, top, top, new Vector2(0, -370), new Vector2(960, 32));
            h.resReward = UiKit.Text(p, "Recompensa", "", 25, Palette.Texto, TextAlignmentOptions.Top, top, top, top, new Vector2(0, -414), new Vector2(940, 150));
            h.resReward.textWrappingMode = TextWrappingModes.Normal;
            h.resReward.enableAutoSizing = true; h.resReward.fontSizeMin = 18; h.resReward.fontSizeMax = 25;
            var bottom = new Vector2(0.5f, 0);
            h.resTanda = UiKit.Button(p, "Tanda", "Elegir la mejora de torre base  [M]", 26, bottom, new Vector2(0, 190), new Vector2(620, 60), null, out _);
            h.resPrimary = UiKit.Button(p, "Principal", "", 26, bottom, new Vector2(-310, 100), new Vector2(290, 70), null, out h.resPrimaryLabel);
            h.resSecondary = UiKit.Button(p, "Secundario", "", 26, bottom, new Vector2(0, 100), new Vector2(290, 70), null, out h.resSecondaryLabel);
            h.resTertiary = UiKit.Button(p, "Salir", "", 26, bottom, new Vector2(310, 100), new Vector2(290, 70), null, out h.resTertiaryLabel);
            h.resFile = UiKit.Text(p, "Archivo", "", 17, Palette.Texto2, TextAlignmentOptions.Center, bottom, bottom, bottom, new Vector2(0, 20), new Vector2(960, 44));
            h.resFile.enableAutoSizing = true; h.resFile.fontSizeMin = 11; h.resFile.fontSizeMax = 17;
            h.resultScreen.SetActive(false);
        }

        static void BuildStart(Hud h, RectTransform root)
        {
            var bg = UiKit.Stretch("Inicio", root);
            UiKit.Panel(bg, Palette.WithAlpha(Palette.Panel, 0.9f));
            h.startScreen = bg.gameObject;
            UiKit.Text(bg, "Titulo", "CLASH DEFENSE", 96, Palette.Texto, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 260), new Vector2(1400, 120)).fontStyle = FontStyles.Bold;
            UiKit.Text(bg, "Sub", "Prototipo 0 · título provisional", 34, Palette.Texto2, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 180), new Vector2(1400, 50));
            UiKit.Text(bg, "Objetivo", "Protegé la base de cinco oleadas combinando torres.", 32, Palette.Texto, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 110), new Vector2(1400, 50));
            h.startPlay = UiKit.Button(bg, "Jugar", "Jugar  [Enter]", 36, C, new Vector2(0, 0), new Vector2(420, 90), null, out _);
            h.tutorialToggle = Toggle(bg, "Tutorial", "Tutorial", 28, C, C, new Vector2(20, -82), new Vector2(420, 40));
            h.tutorialToggle.isOn = true;
            h.startQuit = UiKit.Button(bg, "Salir", "Salir  [S]", 30, C, new Vector2(0, -170), new Vector2(420, 70), null, out h.startQuitLabel);
            h.startNote = UiKit.Text(bg, "Nota", "", 24, Palette.Acento, TextAlignmentOptions.Center, C, C, C, new Vector2(0, -250), new Vector2(1400, 34));
            h.startFooter = UiKit.Text(bg, "Pie", "", 22, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1400, 34));
            h.startScreen.SetActive(false);
        }

        static void BuildError(Hud h, RectTransform root)
        {
            var bg = UiKit.Stretch("Error", root);
            UiKit.Panel(bg, Palette.Panel);
            h.errorScreen = bg.gameObject;
            UiKit.Text(bg, "Titulo", "Los datos del juego no validan", 48, Palette.Vida, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1600, 70));
            h.errorText = UiKit.Text(bg, "Lista", "", 24, Palette.Texto, TextAlignmentOptions.TopLeft, C, C, C, new Vector2(0, 0), new Vector2(1500, 700));
            h.errorText.textWrappingMode = TextWrappingModes.Normal;
            h.errorQuit = UiKit.Button(bg, "Salir", "Salir  [S]", 30, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(300, 70), null, out _);
            h.errorScreen.SetActive(false);
        }
    }
}
