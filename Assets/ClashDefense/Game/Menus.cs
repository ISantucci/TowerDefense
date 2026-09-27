using System;
using System.Collections.Generic;
using ClashDefense.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    public enum MenuScreen { None, Title, Map, Shop, Options, Notice, Tanda }

    /// <summary>
    /// Menús de la campaña (UXS-002.1 … UXS-002.3): pantalla inicial, mapa del mundo, tienda, opciones, aviso de datos y
    /// elección de la mejora de tanda. Muestra y comunica: las reglas viven en Progression (núcleo); lo que el jugador pide
    /// sale por eventos y lo resuelve GameBootstrap.
    /// </summary>
    public sealed class Menus : MonoBehaviour
    {
        public event Action PlayClicked, ShopClicked, OptionsClicked, LabClicked, QuitClicked, NoticeAccepted, ResetProgress;
        public event Action<MenuScreen> BackClicked;
        public event Action<string, bool> LevelPlay;
        public event Action<string> LevelSelected, LevelLocked, BuyClicked, BuyRejected, TandaChosen;
        public event Action<float> VolumeChanged;
        public event Action<bool> MuteChanged, TutorialRepeatChanged;

        Canvas canvas;
        RectTransform root;
        Progression prog;
        BalanceData balance;
        string version;

        GameObject title, map, shop, options, notice, tanda;
        RectTransform mapContent, shopContent, tandaContent;
        TextMeshProUGUI titleFooter, noticeText, optionsData;
        Toggle optTutorial, optMute; Slider optVolume;
        TextMeshProUGUI optResetLabel; float resetArmedAt = -10f;
        GameObject titleQuit;
        string selectedLevel;
        int worldTab;
        string armedBuy; float armedBuyAt;
        bool levelTutorial = true;
        MenuScreen shopReturn = MenuScreen.Title, optionsReturn = MenuScreen.Title;
        string dataFolder = "";

        public MenuScreen Current { get; private set; } = MenuScreen.None;
        public string SelectedLevel => selectedLevel;
        public int WorldTab => worldTab;

        // ------------------------------------------------------------------ construcción
        public void Build(Progression progression, BalanceData bal, string versionLabel, string metricsFolder)
        {
            prog = progression; balance = bal; version = versionLabel; dataFolder = metricsFolder ?? "";
            canvas = UiKit.Canvas(transform, 20);
            root = (RectTransform)canvas.transform;
            BuildTitle();
            map = Screen("Mapa", 0.95f); mapContent = UiKit.Stretch("Contenido", map.transform);
            shop = Screen("Tienda", 0.97f); shopContent = UiKit.Stretch("Contenido", shop.transform);
            BuildOptions();
            BuildNotice();
            tanda = Screen("Tanda", 0.9f); tandaContent = UiKit.Stretch("Contenido", tanda.transform);
            HideAll();
        }

        GameObject Screen(string name, float alpha)
        {
            var bg = UiKit.Stretch(name, root);
            UiKit.Panel(bg, Palette.WithAlpha(Palette.Panel, alpha));
            return bg.gameObject;
        }

        static void Clear(RectTransform rt) { for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject); }

        public void HideAll()
        {
            foreach (var s in new[] { title, map, shop, options, notice, tanda }) if (s != null) s.SetActive(false);
            Current = MenuScreen.None;
        }

        void Show(GameObject s, MenuScreen m)
        {
            foreach (var o in new[] { title, map, shop, options, notice, tanda }) if (o != null) o.SetActive(o == s);
            Current = m;
        }

        // ------------------------------------------------------------------ pantalla inicial (UXS-002.1)
        void BuildTitle()
        {
            title = Screen("Inicio", 0.84f);
            var t = title.transform;
            var head = UiKit.Text(t, "Titulo", "CLASH DEFENSE", 112, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1500, 140));
            head.fontStyle = FontStyles.Bold; head.outlineWidth = 0.15f; head.outlineColor = new Color32(20, 22, 28, 255);
            UiKit.Text(t, "Sub", "Título provisional · Preproducción · Mundo 1", 32, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 245), new Vector2(1500, 46));
            UiKit.Text(t, "Promesa", "Protegé tu base combinando torres: cada enemigo pide su respuesta.", 30, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 185), new Vector2(1500, 46));
            float y = 80;
            UiKit.Button(t, "Jugar", "Jugar  [Enter]", 38, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(560, 96), () => PlayClicked?.Invoke(), out _);
            UiKit.Button(t, "Tienda", "Tienda  [T]", 30, new Vector2(0.5f, 0.5f), new Vector2(0, y - 112), new Vector2(560, 76), () => ShopClicked?.Invoke(), out _);
            UiKit.Button(t, "Opciones", "Opciones  [O]", 30, new Vector2(0.5f, 0.5f), new Vector2(0, y - 204), new Vector2(560, 76), () => OptionsClicked?.Invoke(), out _);
            UiKit.Button(t, "Laboratorio", "Laboratorio: Prototipo 0  [L]", 26, new Vector2(0.5f, 0.5f), new Vector2(0, y - 296), new Vector2(560, 70), () => LabClicked?.Invoke(), out _);
            titleQuit = UiKit.Button(t, "Salir", "Salir  [S]", 28, new Vector2(0.5f, 0.5f), new Vector2(0, y - 386), new Vector2(560, 70), () => QuitClicked?.Invoke(), out _).gameObject;
            titleFooter = UiKit.Text(t, "Pie", "", 22, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1700, 34));
        }

        public void ShowTitle()
        {
            Show(title, MenuScreen.Title);
            titleQuit.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
            titleFooter.text = $"versión {version} · {prog.CurrencyName}: {prog.Currency} · estrellas {prog.TotalStars()} · el progreso se guarda en esta computadora";
        }

        // ------------------------------------------------------------------ mapa del mundo (UXS-002.2)
        public void ShowMap(string select = null)
        {
            if (!string.IsNullOrEmpty(select)) selectedLevel = select;
            if (string.IsNullOrEmpty(selectedLevel) || !prog.IsLevelUnlocked(selectedLevel)) selectedLevel = LastUnlocked();
            var w = prog.WorldOf(selectedLevel);
            if (w != null) worldTab = Array.IndexOf(prog.Campaign.worlds, w);
            Show(map, MenuScreen.Map);
            RebuildMap();
        }

        string LastUnlocked()
        {
            string last = null;
            foreach (var l in prog.AllLevels()) if (prog.IsLevelUnlocked(l.id)) { last = l.id; if (!prog.Completed(l.id)) break; }
            return last;
        }

        public void SetWorldTab(int i)
        {
            worldTab = Mathf.Clamp(i, 0, prog.Campaign.worlds.Length - 1);
            RebuildMap();
        }

        void RebuildMap()
        {
            Clear(mapContent);
            var c = mapContent;
            var world = prog.Campaign.worlds[worldTab];
            bool worldOpen = prog.IsWorldUnlocked(world);

            // cabecera: mundo, pestañas, moneda y estrellas
            var head = UiKit.Text(c, "Mundo", world.displayName.ToUpperInvariant(), 56, Palette.Texto, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -24), new Vector2(520, 70));
            head.fontStyle = FontStyles.Bold;
            for (int i = 0; i < prog.Campaign.worlds.Length; i++)
            {
                var wd = prog.Campaign.worlds[i];
                bool open = prog.IsWorldUnlocked(wd);
                int idx = i;
                var b = UiKit.Button(c, "Pestaña" + i, open ? wd.displayName : $"{wd.displayName} · bloqueado", 24, new Vector2(0.5f, 1), new Vector2(-150 + i * 300, -58), new Vector2(280, 58), () => SetWorldTab(idx), out var bl);
                if (i == worldTab) UiKit.Frame((RectTransform)b.transform, Palette.Acento, 3f);
                if (!open) bl.color = Palette.Bloqueo;
            }
            UiKit.Text(c, "Tab", "cambiar  [Tab]", 18, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(300, 24));
            UiKit.Text(c, "Moneda", $"{prog.CurrencyName} {prog.Currency}", 32, Palette.Cristal, TextAlignmentOptions.MidlineRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -22), new Vector2(460, 40));
            int maxStars = 0; foreach (var l in prog.AllLevels()) if (l.available) maxStars += 3;
            UiKit.Text(c, "Estrellas", $"Estrellas {prog.TotalStars()}/{maxStars}", 24, Palette.Oro, TextAlignmentOptions.MidlineRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -66), new Vector2(460, 32));

            if (!worldOpen)
            {
                string by = prog.FindLevel(world.unlockedBy) != null ? $"Completá {prog.WorldOf(world.unlockedBy).displayName} · Nivel {prog.FindLevel(world.unlockedBy).number} para abrir el {world.displayName}." : "Bloqueado.";
                UiKit.Text(c, "Bloqueado", by, 36, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1400, 60));
                UiKit.Text(c, "Contenido", ContinentSummary(world), 26, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1400, 60));
                BottomButtons(c, false);
                return;
            }

            // continentes en columnas
            int n = world.continents.Length;
            float colW = n <= 1 ? 900f : 580f;
            for (int ci = 0; ci < n; ci++)
            {
                var cont = world.continents[ci];
                float x = (ci - (n - 1) * 0.5f) * 600f;
                var col = UiKit.Rect("Continente_" + cont.id, c, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -130), new Vector2(colW, 620));
                UiKit.Panel(col, Palette.WithAlpha(Palette.PanelClaro, cont.available ? 0.55f : 0.25f), false);
                Palette.Theme(cont.theme, out var terrain, out _, out _, out _);
                var stripe = UiKit.Rect("Franja", col, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 10));
                UiKit.Panel(stripe, cont.available ? terrain : Palette.Bloqueo, false);
                int done = 0; foreach (var l in cont.levels) if (prog.Completed(l.id)) done++;
                UiKit.Text(col, "Nombre", cont.displayName, 32, cont.available ? Palette.Texto : Palette.Bloqueo, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(-40, 40));
                string sub = cont.available ? (Array.IndexOf(prog.Save.claimedTandas, cont.id) >= 0 ? $"Tanda completa {done}/{cont.levels.Length} · mejora de torre base ganada" : $"Tanda {done}/{cont.levels.Length} · al completarla: mejora de torre base") : cont.unavailableText;
                var st = UiKit.Text(col, "Tanda", sub, 19, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -60), new Vector2(-40, 46));
                st.textWrappingMode = TextWrappingModes.Normal;
                float cardH = cont.levels.Length > 3 ? 112f : 150f;
                float gap = cont.levels.Length > 3 ? 10f : 20f;
                for (int li = 0; li < cont.levels.Length; li++)
                    LevelCard(col, cont.levels[li], cont, new Vector2(0, -116 - li * (cardH + gap)), new Vector2(colW - 40, cardH));
            }
            DetailPanel(c);
        }

        string ContinentSummary(WorldData w)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var cont in w.continents) sb.Append(cont.displayName).Append(": ").Append(cont.levels.Length).Append(" niveles. ");
            return sb.ToString();
        }

        void LevelCard(RectTransform parent, LevelMeta lm, ContinentData cont, Vector2 pos, Vector2 size)
        {
            bool unlocked = prog.IsLevelUnlocked(lm.id);
            bool available = lm.available && cont.available;
            bool done = prog.Completed(lm.id);
            string label = lm.boss ? $"Nivel {lm.number} · {lm.displayName}" : $"Nivel {lm.number} · {lm.displayName}";
            var b = UiKit.Button(parent, "Nivel_" + lm.id, "", 20, new Vector2(0.5f, 1), new Vector2(pos.x, pos.y - size.y * 0.5f), size, () =>
            {
                if (!unlocked || !available) { LevelLocked?.Invoke(lm.id); return; }   // Ley 4: el clic en un candado responde (UXS-002.2)
                if (selectedLevel == lm.id) LevelPlay?.Invoke(lm.id, levelTutorial);
                else { selectedLevel = lm.id; LevelSelected?.Invoke(lm.id); RebuildMap(); }
            }, out var dummy);
            dummy.gameObject.SetActive(false);
            var rt = (RectTransform)b.transform;
            var group = b.gameObject.AddComponent<CanvasGroup>();
            group.alpha = available ? (unlocked ? 1f : 0.9f) : 0.8f;   // lo bloqueado se dice con palabras; el atenuado no baja el texto de 4,5:1 (UXS-002.2)
            if (lm.id == selectedLevel) UiKit.Frame(rt, Palette.Acento, 4f);
            UiKit.Text(rt, "Nombre", label, size.y > 120 ? 28 : 24, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(-160, 36));
            // estrellas
            for (int i = 0; i < 3 && available; i++)
            {
                var s = UiKit.Rect("Estrella" + i, rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16 - (2 - i) * 40, -12), new Vector2(34, 34));
                var img = s.gameObject.AddComponent<Image>(); img.sprite = UiKit.Star(); img.raycastTarget = false;
                img.color = i < prog.StarsOf(lm.id) ? Palette.Oro : Palette.WithAlpha(Palette.Bloqueo, 0.35f);
            }
            string state;
            Color stateColor = Palette.Texto2;
            if (!available) state = lm.boss ? "Jefe final del mundo · fuera de la beta" : "Próximamente";
            else if (!unlocked) { state = $"Bloqueado · completá el nivel {lm.number - 1}"; stateColor = Palette.Texto2; }
            else if (done) { var r = prog.Peek(lm.id); state = $"Completado · mejor vida {r.bestHp} · {lm.durationTarget}"; stateColor = Palette.Acento; }
            else { state = $"Disponible · {lm.durationTarget}"; stateColor = Palette.Texto; }
            var stt = UiKit.Text(rt, "Estado", state, 20, stateColor, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(16, size.y > 120 ? -4 : -10), new Vector2(-32, 28));
            UiKit.OneLine(stt, 13f);
            if (!string.IsNullOrEmpty(lm.rewardTower) && size.y > 120)
            {
                string tn = TowerName(lm.rewardTower);
                bool got = prog.IsTowerUnlocked(lm.rewardTower);
                var rw = UiKit.Text(rt, "Premio", got ? $"Desbloqueó: {tn}" : $"Premio: {tn}", 20, got ? Palette.Texto2 : Palette.Oro, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(16, 12), new Vector2(-32, 28));
                UiKit.OneLine(rw, 13f);
            }
        }

        void DetailPanel(RectTransform c)
        {
            var p = UiKit.Rect("Detalle", c, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 250));
            UiKit.Panel(p, Palette.WithAlpha(Palette.Panel, 0.97f), false);
            var lm = prog.FindLevel(selectedLevel);
            if (prog.Save.tandaPending > 0)
            {
                UiKit.Button(c, "Tanda", $"Tenés {prog.Save.tandaPending} mejora de torre base para elegir  [M]", 24, new Vector2(0.5f, 0), new Vector2(0, 285), new Vector2(760, 56), () => ShowTanda(), out var tl);
                tl.color = Palette.Acento;
            }
            if (lm == null) { BottomButtons(c, false); return; }
            var world = prog.WorldOf(lm.id);
            var cont = prog.ContinentOf(lm.id);
            UiKit.Text(p, "Titulo", $"{world.displayName} · {cont.displayName} · Nivel {lm.number} — {lm.displayName}", 36, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -22), new Vector2(1100, 46));
            var r = prog.Peek(lm.id);
            string rec = r.stars > 0 ? $"Récord: {r.stars} de 3 estrellas · mejor vida {r.bestHp} · victorias {r.wins} · intentos {r.attempts}" : r.attempts > 0 ? $"Sin completar · intentos {r.attempts}" : "Sin jugar todavía";
            UiKit.Text(p, "Record", $"Duración objetivo {lm.durationTarget}  ·  {rec}", 22, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -76), new Vector2(1100, 32));
            int next = prog.RewardFor(lm.id, 3);
            string reward = !string.IsNullOrEmpty(lm.rewardTower) && !prog.IsTowerUnlocked(lm.rewardTower)
                ? $"Al completarlo: {TowerName(lm.rewardTower)} + hasta {next} {prog.CurrencyName} (según estrellas)"
                : $"Al completarlo de nuevo: hasta {next} {prog.CurrencyName} (según estrellas)";
            UiKit.Text(p, "Premio", reward, 24, Palette.Oro, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -116), new Vector2(1100, 34));
            if (!string.IsNullOrEmpty(TutorialLevel()) && lm.id == TutorialLevel())
            {
                var tg = DefaultControls.CreateToggle(new DefaultControls.Resources());
                tg.transform.SetParent(p, false);
                var trt = (RectTransform)tg.transform;
                trt.anchorMin = trt.anchorMax = new Vector2(0, 1); trt.pivot = new Vector2(0, 1); trt.anchoredPosition = new Vector2(60, -164); trt.sizeDelta = new Vector2(520, 40);
                var toggle = tg.GetComponent<Toggle>();
                UiKit.StyleToggle(tg);
                UiKit.ReplaceLegacyLabel(tg, "Jugar con tutorial  [Y]", 24);
                toggle.isOn = levelTutorial;
                toggle.onValueChanged.AddListener(v => levelTutorial = v);
            }
            BottomButtons(c, prog.IsLevelUnlocked(lm.id));
        }

        void BottomButtons(RectTransform c, bool canPlay)
        {
            if (canPlay) UiKit.Button(c, "Jugar", "Jugar  [Enter]", 32, new Vector2(1, 0), new Vector2(-300, 150), new Vector2(420, 84), () => LevelPlay?.Invoke(selectedLevel, levelTutorial), out _);
            UiKit.Button(c, "TiendaBtn", "Tienda  [T]", 24, new Vector2(1, 0), new Vector2(-400, 58), new Vector2(220, 60), () => ShopClicked?.Invoke(), out _);
            UiKit.Button(c, "Volver", "Volver  [Esc]", 24, new Vector2(1, 0), new Vector2(-170, 58), new Vector2(220, 60), () => BackClicked?.Invoke(MenuScreen.Map), out _);
            UiKit.Text(c, "Teclas", "Elegir nivel  [flechas]", 20, Palette.Texto2, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 204), new Vector2(420, 28));
        }

        string TutorialLevel()
        {
            foreach (var w in prog.Campaign.worlds) foreach (var c in w.continents) foreach (var l in c.levels) if (l.available) return l.id;
            return null;
        }

        public bool LevelTutorialChecked => levelTutorial;
        public void SetLevelTutorial(bool on) { levelTutorial = on; if (Current == MenuScreen.Map) RebuildMap(); }

        /// <summary>Mueve la selección entre los niveles disponibles del mundo visible (flechas).</summary>
        public void MoveSelection(int delta)
        {
            var list = new List<string>();
            foreach (var cont in prog.Campaign.worlds[worldTab].continents)
                foreach (var l in cont.levels) if (prog.IsLevelUnlocked(l.id)) list.Add(l.id);
            if (list.Count == 0) return;
            int i = list.IndexOf(selectedLevel);
            i = i < 0 ? 0 : Mathf.Clamp(i + delta, 0, list.Count - 1);
            selectedLevel = list[i];
            LevelSelected?.Invoke(selectedLevel);
            RebuildMap();
        }

        string TowerName(string id)
        {
            if (balance?.towers != null) foreach (var t in balance.towers) if (t.id == id) return t.displayName;
            if (prog.Campaign.roster != null) foreach (var r in prog.Campaign.roster) if (r.id == id) return r.displayName;
            return id;
        }

        TowerTypeData TowerType(string id)
        {
            if (balance?.towers != null) foreach (var t in balance.towers) if (t.id == id) return t;
            return null;
        }

        // ------------------------------------------------------------------ tienda (UXS-002.3)
        public void ShowShop(MenuScreen returnTo)
        {
            shopReturn = returnTo;
            Show(shop, MenuScreen.Shop);
            RebuildShop();
        }

        public MenuScreen ShopReturn => shopReturn;
        public MenuScreen OptionsReturn => optionsReturn;

        public void RebuildShop()
        {
            Clear(shopContent);
            var c = shopContent;
            var head = UiKit.Text(c, "Titulo", "TIENDA", 56, Palette.Texto, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -20), new Vector2(600, 70));
            head.fontStyle = FontStyles.Bold;
            UiKit.Text(c, "Moneda", $"{prog.CurrencyName} {prog.Currency}", 34, Palette.Cristal, TextAlignmentOptions.MidlineRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -28), new Vector2(520, 46));
            // Doc 02 §9: la interfaz distingue las tres capas de mejora
            var legend = UiKit.Text(c, "Capas", "Tres formas de mejorar una torre:  <color=#F2F2F2>En la partida</color>, el nivel 2 se paga con oro y se pierde al terminar.  <color=#8FD3FF>Acá</color>, las mejoras permanentes de las torres que desbloqueaste, con cristales.  <color=#45D0C4>Tanda</color>, al completar un continente elegís una mejora permanente para una torre inicial.",
                21, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -96), new Vector2(-120, 64));
            legend.textWrappingMode = TextWrappingModes.Normal;
            var roster = prog.Campaign.roster ?? new RosterEntry[0];
            float cw = 430f, ch = 238f, gx = 16f, gy = 14f;
            for (int i = 0; i < roster.Length; i++)
            {
                int col = i % 4, row = i / 4;
                float x = (col - 1.5f) * (cw + gx);
                float y = -178f - row * (ch + gy);
                ShopCard(c, roster[i], new Vector2(x, y), new Vector2(cw, ch));
            }
            UiKit.Button(c, "Volver", "Volver  [Esc]", 26, new Vector2(1, 0), new Vector2(-170, 50), new Vector2(260, 62), () => BackClicked?.Invoke(MenuScreen.Shop), out _);
        }

        void ShopCard(RectTransform parent, RosterEntry r, Vector2 pos, Vector2 size)
        {
            var card = UiKit.Rect("Torre_" + r.id, parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, size);
            bool unlocked = prog.IsTowerUnlocked(r.id);
            bool initial = Array.IndexOf(prog.Campaign.initialTowers, r.id) >= 0;
            UiKit.Panel(card, Palette.WithAlpha(Palette.PanelClaro, unlocked ? 0.95f : 0.5f), false);
            var stripe = UiKit.Rect("Familia", card, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 0));
            UiKit.Panel(stripe, r.playable ? Palette.ForTower(r.id) : Palette.Bloqueo, false);
            UiKit.Text(card, "Nombre", r.displayName, 26, unlocked ? Palette.Texto : Palette.Bloqueo, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -10), new Vector2(-40, 34));
            string status = initial ? "Torre inicial" : unlocked ? "Desbloqueada" : r.playable ? $"Bloqueada · {r.unlockLabel}" : r.unlockLabel;
            var st = UiKit.Text(card, "Estado", status, 18, unlocked ? Palette.Acento : Palette.Bloqueo, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -44), new Vector2(-40, 24));
            UiKit.OneLine(st, 12f);
            if (initial)
            {
                int tier = prog.TandaTier(r.id);
                string eff = "";
                if (prog.Campaign.tanda?.options != null) foreach (var o in prog.Campaign.tanda.options) if (o.tower == r.id) eff = o.description;
                var tt = UiKit.Text(card, "Tanda", $"<color=#45D0C4>Mejora de tanda:</color> {tier} {(tier == 1 ? "vez" : "veces")}\nCada una: {eff}\nSe gana al completar un continente.", 19, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -76), new Vector2(-40, 140));
                tt.textWrappingMode = TextWrappingModes.Normal;
                return;
            }
            if (!r.playable)
            {
                var ft = UiKit.Text(card, "Futuro", "Llega con el Mundo 2. Sus mejoras aparecen acá cuando la desbloquees.", 19, Palette.Bloqueo, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -76), new Vector2(-40, 120));
                ft.textWrappingMode = TextWrappingModes.Normal;
                return;
            }
            int k = 0;
            if (prog.Campaign.shop != null)
                foreach (var item in prog.Campaign.shop)
                {
                    if (item.tower != r.id) continue;
                    float y = -78 - k * 78;
                    var line = UiKit.Text(card, "Mejora_" + item.id, $"{item.displayName}\n<size=17><color=#A9B0BB>{item.description}</color></size>", 20, unlocked ? Palette.Texto : Palette.Bloqueo, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, y), new Vector2(-230, 60));
                    line.textWrappingMode = TextWrappingModes.Normal;
                    string reason = prog.CannotBuyReason(item.id);
                    string label; Color col = Palette.Texto;
                    if (prog.IsPurchased(item.id)) { label = "Comprada"; col = Palette.Acento; }
                    else if (!unlocked) { label = $"{item.cost} · bloqueada"; col = Palette.Bloqueo; }
                    else if (armedBuy == item.id && Time.unscaledTime - armedBuyAt < 3f) { label = $"¿Confirmar? {item.cost}"; col = Palette.Oro; }
                    else if (reason == "") { label = $"Comprar · {item.cost}"; col = Palette.Cristal; }
                    else { label = $"{item.cost} · {reason}"; col = Palette.Invalido; }
                    string id = item.id;
                    var b = UiKit.Button(card, "Comprar_" + item.id, label, 19, new Vector2(1, 1), new Vector2(-108, y - 28), new Vector2(196, 52), () => OnBuy(id), out var bl);
                    bl.color = col;
                    UiKit.OneLine(bl, 12f);
                    // todo precio responde al clic (Ley 4): lo que no se puede comprar suena a rechazo y el botón ya dice por qué

                    k++;
                }
        }

        void OnBuy(string id)
        {
            if (prog.CannotBuyReason(id) != "") { armedBuy = null; BuyRejected?.Invoke(id); RebuildShop(); return; }
            if (armedBuy == id && Time.unscaledTime - armedBuyAt < 3f)
            {
                armedBuy = null;
                BuyClicked?.Invoke(id);
            }
            else { armedBuy = id; armedBuyAt = Time.unscaledTime; }
            RebuildShop();
        }

        // ------------------------------------------------------------------ opciones (UXS-002.1)
        void BuildOptions()
        {
            options = Screen("Opciones", 0.94f);
            var p = UiKit.Rect("Panel", options.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 760));
            UiKit.Panel(p, Palette.Panel);
            UiKit.Text(p, "Titulo", "OPCIONES", 52, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(900, 66));
            UiKit.Text(p, "Volumen", "Volumen de efectos", 26, Palette.Texto2, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(760, 34));
            var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGo.transform.SetParent(p, false);
            var srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1); srt.pivot = new Vector2(0.5f, 1); srt.anchoredPosition = new Vector2(0, -154); srt.sizeDelta = new Vector2(760, 30);
            optVolume = sliderGo.GetComponent<Slider>();
            UiKit.StyleSlider(sliderGo);
            optVolume.onValueChanged.AddListener(v => VolumeChanged?.Invoke(v));
            optMute = MakeToggle(p, "Silenciar", new Vector2(0, -206), v => MuteChanged?.Invoke(v));
            optTutorial = MakeToggle(p, "Mostrar el tutorial cada vez que juegue el nivel 1", new Vector2(0, -260), v => TutorialRepeatChanged?.Invoke(v));
            optionsData = UiKit.Text(p, "Datos", "", 21, Palette.Texto2, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -320), new Vector2(760, 180));
            optionsData.textWrappingMode = TextWrappingModes.Normal;
            var rb = UiKit.Button(p, "Borrar", "Borrar todo el progreso", 24, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(560, 62), OnReset, out optResetLabel);
            optResetLabel.color = Palette.Invalido;
            UiKit.Button(p, "Volver", "Volver  [Esc]", 26, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(360, 66), () => BackClicked?.Invoke(MenuScreen.Options), out _);
        }

        Toggle MakeToggle(RectTransform parent, string label, Vector2 pos, Action<bool> onChange)
        {
            var tg = DefaultControls.CreateToggle(new DefaultControls.Resources());
            tg.transform.SetParent(parent, false);
            var trt = (RectTransform)tg.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1); trt.pivot = new Vector2(0.5f, 1); trt.anchoredPosition = pos; trt.sizeDelta = new Vector2(760, 40);
            var toggle = tg.GetComponent<Toggle>();
            UiKit.StyleToggle(tg);
            UiKit.ReplaceLegacyLabel(tg, label, 25);
            toggle.onValueChanged.AddListener(v => onChange(v));
            return toggle;
        }

        void OnReset()
        {
            if (Time.unscaledTime - resetArmedAt < 3f)
            {
                resetArmedAt = -10f;
                optResetLabel.text = "Borrar todo el progreso";
                ResetProgress?.Invoke();
            }
            else
            {
                resetArmedAt = Time.unscaledTime;
                optResetLabel.text = "¿Seguro? Clic de nuevo para borrar";
            }
        }

        public void ShowOptions(MenuScreen returnTo, float volume, bool muted)
        {
            optionsReturn = returnTo;
            Show(options, MenuScreen.Options);
            optVolume.SetValueWithoutNotify(volume);
            optMute.SetIsOnWithoutNotify(muted);
            optTutorial.SetIsOnWithoutNotify(prog.Save.tutorialRepeat);
            optResetLabel.text = "Borrar todo el progreso";
            optionsData.text = DataNotice();
        }

        /// <summary>Respuesta visible al borrado (UXS-002.1, Ley 4): el botón dice qué pasó hasta volver a entrar.</summary>
        public void ShowResetDone() { if (optResetLabel != null) optResetLabel.text = "Listo: progreso borrado (como recién instalado)"; }

        string DataNotice() => Application.platform == RuntimePlatform.WebGLPlayer
            ? "Datos: tu progreso (niveles, estrellas, cristales y mejoras) se guarda solo en este navegador. Esta versión no envía nada: " +
              "el registro de cada partida queda en el almacenamiento del navegador."
            : "Datos: tu progreso (niveles, estrellas, cristales y mejoras) se guarda solo en esta computadora. Esta versión no envía nada: " +
              $"cada partida deja un registro local para medir el balance, en «{dataFolder}».";

        public void Rebind(Progression p) { prog = p; }

        // ------------------------------------------------------------------ aviso de primera vez (Doc 02 §13)
        void BuildNotice()
        {
            notice = Screen("Aviso", 0.9f);
            var p = UiKit.Rect("Panel", notice.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 460));
            UiKit.Panel(p, Palette.Panel);
            UiKit.Frame(p, Palette.Acento, 3f);
            UiKit.Text(p, "Titulo", "Antes de empezar", 44, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(1000, 56));
            noticeText = UiKit.Text(p, "Texto", "", 25, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(980, 220));
            noticeText.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Button(p, "Entendido", "Entendido  [Enter]", 30, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(420, 76), () => NoticeAccepted?.Invoke(), out _);
        }

        public void ShowNotice()
        {
            Show(notice, MenuScreen.Notice);
            noticeText.text = DataNotice() + "\n\nEs una versión de preproducción: los números de balance son hipótesis y se van a ajustar con lo que se mida jugando. Lo podés volver a leer en Opciones.";
        }

        // ------------------------------------------------------------------ mejora de tanda (Doc 02 §8, UXS-002.3)
        public void ShowTanda()
        {
            if (prog.Save.tandaPending <= 0 || prog.Campaign.tanda?.options == null) return;
            Show(tanda, MenuScreen.Tanda);
            Clear(tandaContent);
            var p = UiKit.Rect("Panel", tandaContent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320, 640));
            UiKit.Panel(p, Palette.Panel);
            UiKit.Frame(p, Palette.Acento, 3f);
            UiKit.Text(p, "Titulo", "Mejora de torre base", 48, Palette.Texto, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1200, 60));
            var d = UiKit.Text(p, "Texto", prog.Campaign.tanda.description, 24, Palette.Texto2, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(1200, 70));
            d.textWrappingMode = TextWrappingModes.Normal;
            var opts = prog.Campaign.tanda.options;
            for (int i = 0; i < opts.Length; i++)
            {
                var o = opts[i];
                int tier = prog.TandaTier(o.tower);
                string id = o.tower;
                float x = (i - (opts.Length - 1) * 0.5f) * 410f;
                var b = UiKit.Button(p, "Opcion_" + o.tower, "", 20, new Vector2(0.5f, 0.5f), new Vector2(x, -40), new Vector2(380, 300), () => TandaChosen?.Invoke(id), out var dummy);
                dummy.gameObject.SetActive(false);
                var rt = (RectTransform)b.transform;
                var stripe = UiKit.Rect("Familia", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 12));
                UiKit.Panel(stripe, Palette.ForTower(o.tower), false);
                UiKit.Text(rt, "Tecla", $"[{i + 1}]", 26, Palette.Texto2, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -20), new Vector2(60, 34));
                UiKit.Text(rt, "Nombre", TowerName(o.tower), 32, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -22), new Vector2(-80, 44));
                var eff = UiKit.Text(rt, "Efecto", $"{o.description}\n\nMejoras de tanda: {tier} → {tier + 1}", 24, Palette.Texto, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -84), new Vector2(-40, 180));
                eff.textWrappingMode = TextWrappingModes.Normal;
            }
            UiKit.Button(p, "Despues", "Elegir después  [Esc]", 24, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(360, 60), () => BackClicked?.Invoke(MenuScreen.Tanda), out _);
        }

        public string TandaOptionAt(int i)
        {
            var opts = prog.Campaign.tanda?.options;
            return opts != null && i >= 0 && i < opts.Length ? opts[i].tower : null;
        }

        void Update()
        {
            if (Current == MenuScreen.Shop && armedBuy != null && Time.unscaledTime - armedBuyAt >= 3f) { armedBuy = null; RebuildShop(); }
            if (Current == MenuScreen.Options && resetArmedAt > 0f && Time.unscaledTime - resetArmedAt >= 3f) { resetArmedAt = -10f; optResetLabel.text = "Borrar todo el progreso"; }
        }
    }
}
