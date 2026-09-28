using ClashDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Genera el prefab de los menús de la campaña y los prefabs de sus piezas repetidas (pestaña de mundo, columna de
    /// continente, tarjeta de nivel, tarjeta de tienda, fila de mejora, opción de tanda) con la misma disposición que antes
    /// se armaba en código (Menus de TL-002, UXS-002.1 … UXS-002.3).
    /// </summary>
    static class MenusBuilder
    {
        static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        static readonly Vector2 Top = new Vector2(0.5f, 1);
        static readonly Vector2 TopLeft = new Vector2(0, 1);
        static readonly Vector2 TopRight = new Vector2(1, 1);
        static readonly Vector2 Bottom = new Vector2(0.5f, 0);
        static readonly Vector2 BottomRight = new Vector2(1, 0);

        static string P(string name) => $"{HudBuilder.UiRoot}/Prefabs/{name}.prefab";

        static RectTransform Root(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            return (RectTransform)go.transform;
        }

        // ------------------------------------------------------------------ piezas
        public static WorldTabView Tab()
        {
            var holder = Root("tmp");
            var b = UiKit.Button(holder, "PestanaMundo", "Mundo 1", 24, Top, new Vector2(-150, -58), new Vector2(280, 58), null, out var label);
            b.transform.SetParent(null, false);
            Object.DestroyImmediate(holder.gameObject);
            var v = b.gameObject.AddComponent<WorldTabView>();
            v.button = b;
            v.label = label;
            v.selectionFrame = UiKit.Frame((RectTransform)b.transform, Palette.Acento, 3f).gameObject;
            v.selectionFrame.SetActive(false);
            return AssetUtil.SavePrefab<WorldTabView>(b.gameObject, P("PestanaMundo"));
        }

        public static ContinentColumnView Column()
        {
            var col = Root("ColumnaContinente");
            col.anchorMin = col.anchorMax = Top; col.pivot = Top; col.sizeDelta = new Vector2(580, 620);
            var v = col.gameObject.AddComponent<ContinentColumnView>();
            v.panel = UiKit.Panel(col, Palette.WithAlpha(Palette.PanelClaro, 0.55f), false);
            var stripe = UiKit.Rect("Franja", col, TopLeft, TopRight, Top, Vector2.zero, new Vector2(0, 10));
            v.stripe = UiKit.Panel(stripe, Palette.Bloqueo, false);
            v.nameText = UiKit.Text(col, "Nombre", "Continente", 32, Palette.Texto, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(20, -20), new Vector2(-40, 40));
            v.tandaText = UiKit.Text(col, "Tanda", "", 19, Palette.Texto2, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(20, -60), new Vector2(-40, 46));
            v.tandaText.textWrappingMode = TextWrappingModes.Normal;
            v.cards = UiKit.Stretch("Niveles", col);
            return AssetUtil.SavePrefab<ContinentColumnView>(col.gameObject, P("ColumnaContinente"));
        }

        public static LevelCardView LevelCard(Sprite star)
        {
            var holder = Root("tmp");
            var b = HudBuilder.BareButton(holder, "TarjetaNivel", Top, new Vector2(0, -191), new Vector2(540, 150));
            b.transform.SetParent(null, false);
            Object.DestroyImmediate(holder.gameObject);
            var rt = (RectTransform)b.transform;
            var v = b.gameObject.AddComponent<LevelCardView>();
            v.button = b;
            v.group = b.gameObject.AddComponent<CanvasGroup>();
            v.selectionFrame = UiKit.Frame(rt, Palette.Acento, 4f).gameObject;
            v.selectionFrame.SetActive(false);
            v.nameText = UiKit.Text(rt, "Nombre", "Nivel 1 · Nombre", 28, Palette.Texto, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(16, -12), new Vector2(-160, 36));
            v.stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var s = UiKit.Rect("Estrella" + i, rt, TopRight, TopRight, TopRight, new Vector2(-16 - (2 - i) * 40, -12), new Vector2(34, 34));
                var img = s.gameObject.AddComponent<Image>();
                img.sprite = star;
                img.raycastTarget = false;
                img.color = Palette.WithAlpha(Palette.Bloqueo, 0.35f);
                v.stars[i] = img;
            }
            v.stateText = UiKit.Text(rt, "Estado", "Disponible", 20, Palette.Texto2, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(16, -4), new Vector2(-32, 28));
            UiKit.OneLine(v.stateText, 13f);
            v.rewardText = UiKit.Text(rt, "Premio", "", 20, Palette.Oro, TextAlignmentOptions.BottomLeft, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(16, 12), new Vector2(-32, 28));
            UiKit.OneLine(v.rewardText, 13f);
            return AssetUtil.SavePrefab<LevelCardView>(b.gameObject, P("TarjetaNivel"));
        }

        public static ShopCardView ShopCard()
        {
            var card = Root("TarjetaTienda");
            card.anchorMin = card.anchorMax = Top; card.pivot = Top; card.sizeDelta = new Vector2(430, 238);
            var v = card.gameObject.AddComponent<ShopCardView>();
            v.panel = UiKit.Panel(card, Palette.WithAlpha(Palette.PanelClaro, 0.95f), false);
            var stripe = UiKit.Rect("Familia", card, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 0));
            v.stripe = UiKit.Panel(stripe, Palette.Bloqueo, false);
            v.nameText = UiKit.Text(card, "Nombre", "Torre", 26, Palette.Texto, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(24, -10), new Vector2(-40, 34));
            v.statusText = UiKit.Text(card, "Estado", "", 18, Palette.Acento, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(24, -44), new Vector2(-40, 24));
            UiKit.OneLine(v.statusText, 12f);
            v.tandaText = UiKit.Text(card, "Tanda", "", 19, Palette.Texto2, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(24, -76), new Vector2(-40, 140));
            v.tandaText.textWrappingMode = TextWrappingModes.Normal;
            v.futureText = UiKit.Text(card, "Futuro", "Llega con el Mundo 2. Sus mejoras aparecen acá cuando la desbloquees.", 19, Palette.Bloqueo, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(24, -76), new Vector2(-40, 120));
            v.futureText.textWrappingMode = TextWrappingModes.Normal;
            v.rows = UiKit.Stretch("Mejoras", card);
            return AssetUtil.SavePrefab<ShopCardView>(card.gameObject, P("TarjetaTienda"));
        }

        public static ShopRowView ShopRow()
        {
            var row = Root("FilaMejora");
            row.anchorMin = TopLeft; row.anchorMax = TopRight; row.pivot = Top; row.sizeDelta = new Vector2(0, 78);
            var v = row.gameObject.AddComponent<ShopRowView>();
            v.text = UiKit.Text(row, "Texto", "Mejora\n<size=17><color=#A9B0BB>efecto</color></size>", 20, Palette.Texto, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(24, -78), new Vector2(-230, 60));
            v.text.textWrappingMode = TextWrappingModes.Normal;
            v.buy = UiKit.Button(row, "Comprar", "Comprar · 60", 19, TopRight, new Vector2(-108, -106), new Vector2(196, 52), null, out v.buyLabel);
            UiKit.OneLine(v.buyLabel, 12f);
            return AssetUtil.SavePrefab<ShopRowView>(row.gameObject, P("FilaMejora"));
        }

        public static TandaOptionView TandaOption()
        {
            var holder = Root("tmp");
            var b = HudBuilder.BareButton(holder, "OpcionTanda", C, new Vector2(0, -40), new Vector2(380, 300));
            b.transform.SetParent(null, false);
            Object.DestroyImmediate(holder.gameObject);
            var rt = (RectTransform)b.transform;
            var v = b.gameObject.AddComponent<TandaOptionView>();
            v.button = b;
            var stripe = UiKit.Rect("Familia", rt, TopLeft, TopRight, Top, Vector2.zero, new Vector2(0, 12));
            v.stripe = UiKit.Panel(stripe, Palette.Bloqueo, false);
            v.keyText = UiKit.Text(rt, "Tecla", "[1]", 26, Palette.Texto2, TextAlignmentOptions.TopRight, TopRight, TopRight, TopRight, new Vector2(-14, -20), new Vector2(60, 34));
            v.nameText = UiKit.Text(rt, "Nombre", "Torre", 32, Palette.Texto, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(20, -22), new Vector2(-80, 44));
            v.effectText = UiKit.Text(rt, "Efecto", "", 24, Palette.Texto, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(20, -84), new Vector2(-40, 180));
            v.effectText.textWrappingMode = TextWrappingModes.Normal;
            return AssetUtil.SavePrefab<TandaOptionView>(b.gameObject, P("OpcionTanda"));
        }

        // ------------------------------------------------------------------ menús
        static GameObject Screen(RectTransform root, string name, float alpha)
        {
            var bg = UiKit.Stretch(name, root);
            UiKit.Panel(bg, Palette.WithAlpha(Palette.Panel, alpha));
            return bg.gameObject;
        }

        public static Menus Build(Sprite star)
        {
            var go = new GameObject("Menus");
            var m = go.AddComponent<Menus>();
            m.tabPrefab = Tab();
            m.columnPrefab = Column();
            m.levelCardPrefab = LevelCard(star);
            m.shopCardPrefab = ShopCard();
            m.shopRowPrefab = ShopRow();
            m.tandaOptionPrefab = TandaOption();
            var root = (RectTransform)UiKit.Canvas(go.transform, 20).transform;
            BuildTitle(m, root);
            BuildMap(m, root);
            BuildShop(m, root);
            BuildOptions(m, root);
            BuildNotice(m, root);
            BuildTanda(m, root);
            foreach (var s in new[] { m.title, m.map, m.shop, m.options, m.notice, m.tanda }) s.SetActive(false);
            return AssetUtil.SavePrefab<Menus>(go, P("Menus"));
        }

        static void BuildTitle(Menus m, RectTransform root)
        {
            m.title = Screen(root, "Inicio", 0.84f);
            var t = m.title.transform;
            var head = UiKit.Text(t, "Titulo", "CLASH DEFENSE", 112, Palette.Texto, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 330), new Vector2(1500, 140));
            head.fontStyle = FontStyles.Bold;
            var outline = MaterialLibrary.TmpOutline(0.15f);
            if (outline != null) head.fontSharedMaterial = outline;
            UiKit.Text(t, "Sub", "Título provisional · Preproducción · Mundo 1", 32, Palette.Texto2, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 245), new Vector2(1500, 46));
            UiKit.Text(t, "Promesa", "Protegé tu base combinando torres: cada enemigo pide su respuesta.", 30, Palette.Texto, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 185), new Vector2(1500, 46));
            float y = 80;
            m.titlePlay = UiKit.Button(t, "Jugar", "Jugar  [Enter]", 38, C, new Vector2(0, y), new Vector2(560, 96), null, out _);
            m.titleShop = UiKit.Button(t, "Tienda", "Tienda  [T]", 30, C, new Vector2(0, y - 112), new Vector2(560, 76), null, out _);
            m.titleOptions = UiKit.Button(t, "Opciones", "Opciones  [O]", 30, C, new Vector2(0, y - 204), new Vector2(560, 76), null, out _);
            m.titleLab = UiKit.Button(t, "Laboratorio", "Laboratorio: Prototipo 0  [L]", 26, C, new Vector2(0, y - 296), new Vector2(560, 70), null, out _);
            m.titleQuit = UiKit.Button(t, "Salir", "Salir  [S]", 28, C, new Vector2(0, y - 386), new Vector2(560, 70), null, out _);
            m.titleFooter = UiKit.Text(t, "Pie", "", 22, Palette.Texto2, TextAlignmentOptions.Center, Bottom, Bottom, Bottom, new Vector2(0, 30), new Vector2(1700, 34));
        }

        static void BuildMap(Menus m, RectTransform root)
        {
            m.map = Screen(root, "Mapa", 0.95f);
            var c = UiKit.Stretch("Contenido", m.map.transform);
            m.mapWorldTitle = UiKit.Text(c, "Mundo", "MUNDO 1", 56, Palette.Texto, TextAlignmentOptions.MidlineLeft, TopLeft, TopLeft, TopLeft, new Vector2(60, -24), new Vector2(520, 70));
            m.mapWorldTitle.fontStyle = FontStyles.Bold;
            m.mapTabs = UiKit.Stretch("Pestanas", c);
            UiKit.Text(c, "Tab", "cambiar  [Tab]", 18, Palette.Texto2, TextAlignmentOptions.Center, Top, Top, Top, new Vector2(0, -92), new Vector2(300, 24));
            m.mapCurrency = UiKit.Text(c, "Moneda", "Cristales 0", 32, Palette.Cristal, TextAlignmentOptions.MidlineRight, TopRight, TopRight, TopRight, new Vector2(-60, -22), new Vector2(460, 40));
            m.mapStars = UiKit.Text(c, "Estrellas", "Estrellas 0/18", 24, Palette.Oro, TextAlignmentOptions.MidlineRight, TopRight, TopRight, TopRight, new Vector2(-60, -66), new Vector2(460, 32));

            var locked = UiKit.Stretch("MundoBloqueado", c);
            m.mapLocked = locked.gameObject;
            m.mapLockedText = UiKit.Text(locked, "Texto", "", 36, Palette.Texto, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 80), new Vector2(1400, 60));
            m.mapLockedSummary = UiKit.Text(locked, "Contenido", "", 26, Palette.Texto2, TextAlignmentOptions.Center, C, C, C, new Vector2(0, 10), new Vector2(1400, 60));
            m.mapLocked.SetActive(false);

            m.mapColumns = UiKit.Stretch("Continentes", c);

            var p = UiKit.Rect("Detalle", c, Vector2.zero, new Vector2(1, 0), Bottom, Vector2.zero, new Vector2(0, 250));
            UiKit.Panel(p, Palette.WithAlpha(Palette.Panel, 0.97f), false);
            m.detailPanel = p.gameObject;
            m.detailTitle = UiKit.Text(p, "Titulo", "", 36, Palette.Texto, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(60, -22), new Vector2(1100, 46));
            m.detailRecord = UiKit.Text(p, "Record", "", 22, Palette.Texto2, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(60, -76), new Vector2(1100, 32));
            m.detailReward = UiKit.Text(p, "Premio", "", 24, Palette.Oro, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(60, -116), new Vector2(1100, 34));
            m.detailTutorial = HudBuilder.Toggle(p, "Tutorial", "Jugar con tutorial  [Y]", 24, TopLeft, TopLeft, new Vector2(60, -164), new Vector2(520, 40));
            m.detailTutorial.isOn = true;

            m.mapTanda = UiKit.Button(c, "Tanda", "Tenés 1 mejora de torre base para elegir  [M]", 24, Bottom, new Vector2(0, 285), new Vector2(760, 56), null, out m.mapTandaLabel);
            m.mapTandaLabel.color = Palette.Acento;
            m.mapTanda.gameObject.SetActive(false);
            m.mapPlay = UiKit.Button(c, "Jugar", "Jugar  [Enter]", 32, BottomRight, new Vector2(-300, 150), new Vector2(420, 84), null, out _);
            m.mapShop = UiKit.Button(c, "TiendaBtn", "Tienda  [T]", 24, BottomRight, new Vector2(-400, 58), new Vector2(220, 60), null, out _);
            m.mapBack = UiKit.Button(c, "Volver", "Volver  [Esc]", 24, BottomRight, new Vector2(-170, 58), new Vector2(220, 60), null, out _);
            UiKit.Text(c, "Teclas", "Elegir nivel  [flechas]", 20, Palette.Texto2, TextAlignmentOptions.BottomRight, BottomRight, BottomRight, BottomRight, new Vector2(-60, 204), new Vector2(420, 28));
            m.columnSpacing = 600f; m.columnWidthMany = 580f; m.columnWidthSingle = 900f; m.columnHeight = 620f; m.columnTop = -130f; m.cardTop = -116f;
        }

        static void BuildShop(Menus m, RectTransform root)
        {
            m.shop = Screen(root, "Tienda", 0.97f);
            var c = UiKit.Stretch("Contenido", m.shop.transform);
            var head = UiKit.Text(c, "Titulo", "TIENDA", 56, Palette.Texto, TextAlignmentOptions.MidlineLeft, TopLeft, TopLeft, TopLeft, new Vector2(60, -20), new Vector2(600, 70));
            head.fontStyle = FontStyles.Bold;
            m.shopCurrency = UiKit.Text(c, "Moneda", "Cristales 0", 34, Palette.Cristal, TextAlignmentOptions.MidlineRight, TopRight, TopRight, TopRight, new Vector2(-60, -28), new Vector2(520, 46));
            // Doc 02 §9: la interfaz distingue las tres capas de mejora
            var legend = UiKit.Text(c, "Capas", "Tres formas de mejorar una torre:  <color=#F2F2F2>En la partida</color>, el nivel 2 se paga con oro y se pierde al terminar.  <color=#8FD3FF>Acá</color>, las mejoras permanentes de las torres que desbloqueaste, con cristales.  <color=#45D0C4>Tanda</color>, al completar un continente elegís una mejora permanente para una torre inicial.",
                21, Palette.Texto2, TextAlignmentOptions.TopLeft, TopLeft, TopRight, TopLeft, new Vector2(60, -96), new Vector2(-120, 64));
            legend.textWrappingMode = TextWrappingModes.Normal;
            m.shopCards = UiKit.Stretch("Torres", c);
            m.shopCardSize = new Vector2(430f, 238f); m.shopCardGap = new Vector2(16f, 14f); m.shopTop = -178f; m.shopRowHeight = 78f; m.shopColumns = 4;
            m.shopBack = UiKit.Button(c, "Volver", "Volver  [Esc]", 26, BottomRight, new Vector2(-170, 50), new Vector2(260, 62), null, out _);
        }

        static void BuildOptions(Menus m, RectTransform root)
        {
            m.options = Screen(root, "Opciones", 0.94f);
            var p = UiKit.Rect("Panel", m.options.transform, C, C, C, Vector2.zero, new Vector2(1000, 760));
            UiKit.Panel(p, Palette.Panel);
            UiKit.Text(p, "Titulo", "OPCIONES", 52, Palette.Texto, TextAlignmentOptions.Center, Top, Top, Top, new Vector2(0, -24), new Vector2(900, 66));
            UiKit.Text(p, "Volumen", "Volumen de efectos", 26, Palette.Texto2, TextAlignmentOptions.MidlineLeft, Top, Top, Top, new Vector2(0, -110), new Vector2(760, 34));
            m.optVolume = HudBuilder.Slider(p, "BarraVolumen", new Vector2(0, -154), new Vector2(760, 30));
            m.optMute = HudBuilder.Toggle(p, "Silenciar", "Silenciar", 25, Top, Top, new Vector2(0, -206), new Vector2(760, 40));
            m.optTutorial = HudBuilder.Toggle(p, "TutorialSiempre", "Mostrar el tutorial cada vez que juegue el nivel 1", 25, Top, Top, new Vector2(0, -260), new Vector2(760, 40));
            m.optMute.isOn = false; m.optTutorial.isOn = false;
            m.optionsData = UiKit.Text(p, "Datos", "", 21, Palette.Texto2, TextAlignmentOptions.TopLeft, Top, Top, Top, new Vector2(0, -320), new Vector2(760, 180));
            m.optionsData.textWrappingMode = TextWrappingModes.Normal;
            m.optReset = UiKit.Button(p, "Borrar", "Borrar todo el progreso", 24, Bottom, new Vector2(0, 150), new Vector2(560, 62), null, out m.optResetLabel);
            m.optResetLabel.color = Palette.Invalido;
            m.optBack = UiKit.Button(p, "Volver", "Volver  [Esc]", 26, Bottom, new Vector2(0, 60), new Vector2(360, 66), null, out _);
        }

        static void BuildNotice(Menus m, RectTransform root)
        {
            m.notice = Screen(root, "Aviso", 0.9f);
            var p = UiKit.Rect("Panel", m.notice.transform, C, C, C, Vector2.zero, new Vector2(1100, 460));
            UiKit.Panel(p, Palette.Panel);
            UiKit.Frame(p, Palette.Acento, 3f);
            UiKit.Text(p, "Titulo", "Antes de empezar", 44, Palette.Texto, TextAlignmentOptions.Center, Top, Top, Top, new Vector2(0, -28), new Vector2(1000, 56));
            m.noticeText = UiKit.Text(p, "Texto", "", 25, Palette.Texto, TextAlignmentOptions.TopLeft, Top, Top, Top, new Vector2(0, -110), new Vector2(980, 220));
            m.noticeText.textWrappingMode = TextWrappingModes.Normal;
            m.noticeOk = UiKit.Button(p, "Entendido", "Entendido  [Enter]", 30, Bottom, new Vector2(0, 60), new Vector2(420, 76), null, out _);
        }

        static void BuildTanda(Menus m, RectTransform root)
        {
            m.tanda = Screen(root, "Tanda", 0.9f);
            var p = UiKit.Rect("Panel", m.tanda.transform, C, C, C, Vector2.zero, new Vector2(1320, 640));
            UiKit.Panel(p, Palette.Panel);
            UiKit.Frame(p, Palette.Acento, 3f);
            UiKit.Text(p, "Titulo", "Mejora de torre base", 48, Palette.Texto, TextAlignmentOptions.Center, Top, Top, Top, new Vector2(0, -24), new Vector2(1200, 60));
            m.tandaDescription = UiKit.Text(p, "Texto", "", 24, Palette.Texto2, TextAlignmentOptions.Center, Top, Top, Top, new Vector2(0, -92), new Vector2(1200, 70));
            m.tandaDescription.textWrappingMode = TextWrappingModes.Normal;
            m.tandaOptions = UiKit.Stretch("Opciones", p);
            m.tandaLater = UiKit.Button(p, "Despues", "Elegir después  [Esc]", 24, Bottom, new Vector2(0, 46), new Vector2(360, 60), null, out _);
        }
    }
}
