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
    /// sale por eventos y lo resuelve el flujo del juego.
    /// TL-003: vive en el prefab Menus (UI/Prefabs). Las tarjetas de nivel, de tienda y de tanda son prefabs propios que se
    /// crean una vez y se actualizan en el lugar: cambiar la selección ya no destruye y reconstruye la pantalla.
    /// </summary>
    public sealed class Menus : MonoBehaviour
    {
        public event Action PlayClicked, ShopClicked, OptionsClicked, LabClicked, QuitClicked, NoticeAccepted, ResetProgress;
        public event Action<MenuScreen> BackClicked;
        public event Action<string, bool> LevelPlay;
        public event Action<string> LevelSelected, LevelLocked, BuyClicked, BuyRejected, TandaChosen;
        public event Action<float> VolumeChanged;
        public event Action<bool> MuteChanged, TutorialRepeatChanged;

        [Header("Pantallas")]
        [SerializeField] internal GameObject title, map, shop, options, notice, tanda;
        [Header("Inicio")]
        [SerializeField] internal Button titlePlay, titleShop, titleOptions, titleLab, titleQuit;
        [SerializeField] internal TextMeshProUGUI titleFooter;
        [Header("Mapa")]
        [SerializeField] internal TextMeshProUGUI mapWorldTitle, mapCurrency, mapStars;
        [SerializeField] internal RectTransform mapTabs;
        [SerializeField] internal WorldTabView tabPrefab;
        [SerializeField] internal GameObject mapLocked;
        [SerializeField] internal TextMeshProUGUI mapLockedText, mapLockedSummary;
        [SerializeField] internal RectTransform mapColumns;
        [SerializeField] internal ContinentColumnView columnPrefab;
        [SerializeField] internal LevelCardView levelCardPrefab;
        [SerializeField] internal GameObject detailPanel;
        [SerializeField] internal TextMeshProUGUI detailTitle, detailRecord, detailReward;
        [SerializeField] internal Toggle detailTutorial;
        [SerializeField] internal Button mapTanda, mapPlay, mapShop, mapBack;
        [SerializeField] internal TextMeshProUGUI mapTandaLabel;
        [Header("Mapa: medidas (editables)")]
        [SerializeField] internal float columnSpacing = 600f;
        [SerializeField] internal float columnWidthMany = 580f, columnWidthSingle = 900f, columnHeight = 620f, columnTop = -130f;
        [SerializeField] internal float cardTop = -116f;
        [Header("Tienda")]
        [SerializeField] internal TextMeshProUGUI shopCurrency;
        [SerializeField] internal RectTransform shopCards;
        [SerializeField] internal ShopCardView shopCardPrefab;
        [SerializeField] internal ShopRowView shopRowPrefab;
        [SerializeField] internal Button shopBack;
        [SerializeField] internal Vector2 shopCardSize = new Vector2(430f, 238f);
        [SerializeField] internal Vector2 shopCardGap = new Vector2(16f, 14f);
        [SerializeField] internal float shopTop = -178f, shopRowHeight = 78f;
        [SerializeField] internal int shopColumns = 4;
        [Header("Opciones")]
        [SerializeField] internal Slider optVolume;
        [SerializeField] internal Toggle optMute, optTutorial;
        [SerializeField] internal TextMeshProUGUI optionsData, optResetLabel;
        [SerializeField] internal Button optReset, optBack;
        [Header("Aviso de datos")]
        [SerializeField] internal TextMeshProUGUI noticeText;
        [SerializeField] internal Button noticeOk;
        [Header("Mejora de tanda")]
        [SerializeField] internal TextMeshProUGUI tandaDescription;
        [SerializeField] internal RectTransform tandaOptions;
        [SerializeField] internal TandaOptionView tandaOptionPrefab;
        [SerializeField] internal Button tandaLater;

        Progression prog;
        CampaignDefinition campaign;
        string version, dataFolder = "";
        string selectedLevel;
        int worldTab;
        string armedBuy; float armedBuyAt;
        bool levelTutorial = true;
        float resetArmedAt = -10f;
        MenuScreen shopReturn = MenuScreen.Title, optionsReturn = MenuScreen.Title;
        GameObject[] screens;

        // vistas creadas una vez y actualizadas en el lugar
        readonly List<WorldTabView> tabs = new List<WorldTabView>();
        readonly Dictionary<int, List<ContinentColumnView>> worldColumns = new Dictionary<int, List<ContinentColumnView>>();
        readonly List<(LevelCardView view, LevelMeta meta, ContinentData cont)> levelCards = new List<(LevelCardView, LevelMeta, ContinentData)>();
        readonly List<(ShopCardView view, RosterEntry roster)> shopViews = new List<(ShopCardView, RosterEntry)>();
        readonly List<ShopRowView> shopRows = new List<ShopRowView>();
        readonly List<TandaOptionView> tandaViews = new List<TandaOptionView>();

        public MenuScreen Current { get; private set; } = MenuScreen.None;
        public string SelectedLevel => selectedLevel;
        public int WorldTab => worldTab;
        public MenuScreen ShopReturn => shopReturn;
        public MenuScreen OptionsReturn => optionsReturn;
        public bool LevelTutorialChecked => levelTutorial;

        // ------------------------------------------------------------------ enlace
        void Awake()
        {
            screens = new[] { title, map, shop, options, notice, tanda };
            titlePlay.onClick.AddListener(() => PlayClicked?.Invoke());
            titleShop.onClick.AddListener(() => ShopClicked?.Invoke());
            titleOptions.onClick.AddListener(() => OptionsClicked?.Invoke());
            titleLab.onClick.AddListener(() => LabClicked?.Invoke());
            titleQuit.onClick.AddListener(() => QuitClicked?.Invoke());
            mapTanda.onClick.AddListener(ShowTanda);
            mapPlay.onClick.AddListener(() => LevelPlay?.Invoke(selectedLevel, levelTutorial));
            mapShop.onClick.AddListener(() => ShopClicked?.Invoke());
            mapBack.onClick.AddListener(() => BackClicked?.Invoke(MenuScreen.Map));
            detailTutorial.onValueChanged.AddListener(v => levelTutorial = v);
            shopBack.onClick.AddListener(() => BackClicked?.Invoke(MenuScreen.Shop));
            optVolume.onValueChanged.AddListener(v => VolumeChanged?.Invoke(v));
            optMute.onValueChanged.AddListener(v => MuteChanged?.Invoke(v));
            optTutorial.onValueChanged.AddListener(v => TutorialRepeatChanged?.Invoke(v));
            optReset.onClick.AddListener(OnReset);
            optBack.onClick.AddListener(() => BackClicked?.Invoke(MenuScreen.Options));
            noticeOk.onClick.AddListener(() => NoticeAccepted?.Invoke());
            tandaLater.onClick.AddListener(() => BackClicked?.Invoke(MenuScreen.Tanda));
            HideAll();
        }

        public void Setup(Progression progression, CampaignDefinition campaignDef, string versionLabel, string metricsFolder)
        {
            prog = progression; campaign = campaignDef; version = versionLabel; dataFolder = metricsFolder ?? "";
        }

        public void Rebind(Progression p) { prog = p; }

        public void HideAll()
        {
            if (screens == null) screens = new[] { title, map, shop, options, notice, tanda };
            foreach (var s in screens) if (s != null && s.activeSelf) s.SetActive(false);
            Current = MenuScreen.None;
        }

        void Show(GameObject s, MenuScreen m)
        {
            if (screens == null) screens = new[] { title, map, shop, options, notice, tanda };
            foreach (var o in screens) if (o != null && o.activeSelf != (o == s)) o.SetActive(o == s);
            Current = m;
        }

        string TowerName(string id)
        {
            var t = campaign != null && campaign.balance != null ? campaign.balance.Tower(id) : null;
            if (t != null) return t.data.displayName;
            if (prog.Campaign.roster != null) foreach (var r in prog.Campaign.roster) if (r.id == id) return r.displayName;
            return id;
        }

        Color TowerColor(string id)
        {
            var t = campaign != null && campaign.balance != null ? campaign.balance.Tower(id) : null;
            return t != null ? t.color : Palette.Bloqueo;
        }

        Color ThemeColor(string continentId)
        {
            var c = campaign != null ? campaign.FindContinent(continentId) : null;
            return c != null && c.theme != null ? c.theme.StripeColor : Palette.Bloqueo;
        }

        // ------------------------------------------------------------------ pantalla inicial (UXS-002.1)
        public void ShowTitle()
        {
            Show(title, MenuScreen.Title);
            titleQuit.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
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
            RefreshMap();
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
            RefreshMap();
        }

        void EnsureTabs()
        {
            var worlds = prog.Campaign.worlds;
            while (tabs.Count < worlds.Length)
            {
                int idx = tabs.Count;
                var t = Instantiate(tabPrefab, mapTabs);
                t.name = "Pestaña" + idx;
                ((RectTransform)t.transform).anchoredPosition = new Vector2(-150 + idx * 300, -58);
                t.button.onClick.AddListener(() => SetWorldTab(idx));
                tabs.Add(t);
            }
        }

        List<ContinentColumnView> EnsureColumns(int w)
        {
            if (worldColumns.TryGetValue(w, out var cols)) return cols;
            cols = new List<ContinentColumnView>();
            var world = prog.Campaign.worlds[w];
            int n = world.continents.Length;
            float colW = n <= 1 ? columnWidthSingle : columnWidthMany;
            for (int ci = 0; ci < n; ci++)
            {
                var cont = world.continents[ci];
                var col = Instantiate(columnPrefab, mapColumns);
                col.name = "Continente_" + cont.id;
                col.Rect.anchoredPosition = new Vector2((ci - (n - 1) * 0.5f) * columnSpacing, columnTop);
                col.Rect.sizeDelta = new Vector2(colW, columnHeight);
                float cardH = cont.levels.Length > 3 ? 112f : 150f;
                float gap = cont.levels.Length > 3 ? 10f : 20f;
                for (int li = 0; li < cont.levels.Length; li++)
                {
                    var lm = cont.levels[li];
                    var card = Instantiate(levelCardPrefab, col.cards);
                    card.name = "Nivel_" + lm.id;
                    card.LevelId = lm.id;
                    card.Rect.anchoredPosition = new Vector2(0f, cardTop - li * (cardH + gap) - cardH * 0.5f);
                    card.Rect.sizeDelta = new Vector2(colW - 40f, cardH);
                    var meta = lm; var continent = cont;
                    card.button.onClick.AddListener(() => OnLevelCard(meta, continent));
                    levelCards.Add((card, lm, cont));
                }
                cols.Add(col);
            }
            worldColumns[w] = cols;
            return cols;
        }

        void OnLevelCard(LevelMeta lm, ContinentData cont)
        {
            bool unlocked = prog.IsLevelUnlocked(lm.id);
            bool available = lm.available && cont.available;
            if (!unlocked || !available) { LevelLocked?.Invoke(lm.id); return; }   // Ley 4: el clic en un candado responde (UXS-002.2)
            if (selectedLevel == lm.id) LevelPlay?.Invoke(lm.id, levelTutorial);
            else { selectedLevel = lm.id; LevelSelected?.Invoke(lm.id); RefreshMap(); }
        }

        void RefreshMap()
        {
            var world = prog.Campaign.worlds[worldTab];
            bool worldOpen = prog.IsWorldUnlocked(world);

            // cabecera: mundo, pestañas, moneda y estrellas
            mapWorldTitle.text = world.displayName.ToUpperInvariant();
            EnsureTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                var wd = prog.Campaign.worlds[i];
                tabs[i].Bind(wd.displayName, prog.IsWorldUnlocked(wd), i == worldTab);
            }
            mapCurrency.text = $"{prog.CurrencyName} {prog.Currency}";
            int maxStars = 0; foreach (var l in prog.AllLevels()) if (l.available) maxStars += 3;
            mapStars.text = $"Estrellas {prog.TotalStars()}/{maxStars}";

            foreach (var kv in worldColumns) foreach (var c in kv.Value) c.gameObject.SetActive(kv.Key == worldTab && worldOpen);
            mapLocked.SetActive(!worldOpen);
            detailPanel.SetActive(worldOpen);
            if (!worldOpen)
            {
                mapLockedText.text = prog.FindLevel(world.unlockedBy) != null ? $"Completá {prog.WorldOf(world.unlockedBy).displayName} · Nivel {prog.FindLevel(world.unlockedBy).number} para abrir el {world.displayName}." : "Bloqueado.";
                var sb = new System.Text.StringBuilder();
                foreach (var cont in world.continents) sb.Append(cont.displayName).Append(": ").Append(cont.levels.Length).Append(" niveles. ");
                mapLockedSummary.text = sb.ToString();
                mapTanda.gameObject.SetActive(false);
                mapPlay.gameObject.SetActive(false);
                return;
            }

            // continentes en columnas
            var cols = EnsureColumns(worldTab);
            for (int ci = 0; ci < world.continents.Length; ci++)
            {
                var cont = world.continents[ci];
                var col = cols[ci];
                col.panel.color = Palette.WithAlpha(Palette.PanelClaro, cont.available ? 0.55f : 0.25f);
                col.stripe.color = cont.available ? ThemeColor(cont.id) : Palette.Bloqueo;
                col.nameText.text = cont.displayName;
                col.nameText.color = cont.available ? Palette.Texto : Palette.Bloqueo;
                int done = 0; foreach (var l in cont.levels) if (prog.Completed(l.id)) done++;
                col.tandaText.text = cont.available ? (Array.IndexOf(prog.Save.claimedTandas, cont.id) >= 0 ? $"Tanda completa {done}/{cont.levels.Length} · mejora de torre base ganada" : $"Tanda {done}/{cont.levels.Length} · al completarla: mejora de torre base") : cont.unavailableText;
            }
            foreach (var (view, lm, cont) in levelCards) if (view.gameObject.activeInHierarchy) RefreshCard(view, lm, cont);
            RefreshDetail();
        }

        void RefreshCard(LevelCardView v, LevelMeta lm, ContinentData cont)
        {
            bool unlocked = prog.IsLevelUnlocked(lm.id);
            bool available = lm.available && cont.available;
            bool done = prog.Completed(lm.id);
            bool big = v.Rect.sizeDelta.y > 120f;
            v.group.alpha = available ? (unlocked ? 1f : 0.9f) : 0.8f;   // lo bloqueado se dice con palabras; el atenuado no baja el texto de 4,5:1 (UXS-002.2)
            v.selectionFrame.SetActive(lm.id == selectedLevel);
            v.nameText.text = $"Nivel {lm.number} · {lm.displayName}";
            v.nameText.fontSize = big ? 28 : 24;
            for (int i = 0; i < 3; i++)
            {
                v.stars[i].gameObject.SetActive(available);
                v.stars[i].color = i < prog.StarsOf(lm.id) ? Palette.Oro : Palette.WithAlpha(Palette.Bloqueo, 0.35f);
            }
            string state;
            Color stateColor = Palette.Texto2;
            if (!available) state = lm.boss ? "Jefe final del mundo · fuera de la beta" : "Próximamente";
            else if (!unlocked) state = $"Bloqueado · completá el nivel {lm.number - 1}";
            else if (done) { var r = prog.Peek(lm.id); state = $"Completado · mejor vida {r.bestHp} · {lm.durationTarget}"; stateColor = Palette.Acento; }
            else { state = $"Disponible · {lm.durationTarget}"; stateColor = Palette.Texto; }
            v.stateText.text = state;
            v.stateText.color = stateColor;
            v.stateText.rectTransform.anchoredPosition = new Vector2(16f, big ? -4f : -10f);
            bool reward = !string.IsNullOrEmpty(lm.rewardTower) && big;
            v.rewardText.gameObject.SetActive(reward);
            if (reward)
            {
                string tn = TowerName(lm.rewardTower);
                bool got = prog.IsTowerUnlocked(lm.rewardTower);
                v.rewardText.text = got ? $"Desbloqueó: {tn}" : $"Premio: {tn}";
                v.rewardText.color = got ? Palette.Texto2 : Palette.Oro;
            }
        }

        void RefreshDetail()
        {
            mapTanda.gameObject.SetActive(prog.Save.tandaPending > 0);
            if (prog.Save.tandaPending > 0) mapTandaLabel.text = $"Tenés {prog.Save.tandaPending} mejora de torre base para elegir  [M]";
            var lm = prog.FindLevel(selectedLevel);
            detailTitle.gameObject.SetActive(lm != null);
            detailRecord.gameObject.SetActive(lm != null);
            detailReward.gameObject.SetActive(lm != null);
            detailTutorial.gameObject.SetActive(lm != null && lm.id == TutorialLevel());
            mapPlay.gameObject.SetActive(lm != null && prog.IsLevelUnlocked(lm.id));
            if (lm == null) return;
            var world = prog.WorldOf(lm.id);
            var cont = prog.ContinentOf(lm.id);
            detailTitle.text = $"{world.displayName} · {cont.displayName} · Nivel {lm.number} — {lm.displayName}";
            var r = prog.Peek(lm.id);
            string rec = r.stars > 0 ? $"Récord: {r.stars} de 3 estrellas · mejor vida {r.bestHp} · victorias {r.wins} · intentos {r.attempts}" : r.attempts > 0 ? $"Sin completar · intentos {r.attempts}" : "Sin jugar todavía";
            detailRecord.text = $"Duración objetivo {lm.durationTarget}  ·  {rec}";
            int next = prog.RewardFor(lm.id, 3);
            detailReward.text = !string.IsNullOrEmpty(lm.rewardTower) && !prog.IsTowerUnlocked(lm.rewardTower)
                ? $"Al completarlo: {TowerName(lm.rewardTower)} + hasta {next} {prog.CurrencyName} (según estrellas)"
                : $"Al completarlo de nuevo: hasta {next} {prog.CurrencyName} (según estrellas)";
            detailTutorial.SetIsOnWithoutNotify(levelTutorial);
        }

        string TutorialLevel()
        {
            foreach (var w in prog.Campaign.worlds) foreach (var c in w.continents) foreach (var l in c.levels) if (l.available) return l.id;
            return null;
        }

        public void SetLevelTutorial(bool on) { levelTutorial = on; if (Current == MenuScreen.Map) RefreshMap(); }

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
            RefreshMap();
        }

        // ------------------------------------------------------------------ tienda (UXS-002.3)
        public void ShowShop(MenuScreen returnTo)
        {
            shopReturn = returnTo;
            Show(shop, MenuScreen.Shop);
            RebuildShop();
        }

        void EnsureShop()
        {
            if (shopViews.Count > 0) return;
            var roster = prog.Campaign.roster ?? new RosterEntry[0];
            for (int i = 0; i < roster.Length; i++)
            {
                int col = i % shopColumns, row = i / shopColumns;
                var card = Instantiate(shopCardPrefab, shopCards);
                card.name = "Torre_" + roster[i].id;
                card.Rect.anchoredPosition = new Vector2((col - (shopColumns - 1) * 0.5f) * (shopCardSize.x + shopCardGap.x), shopTop - row * (shopCardSize.y + shopCardGap.y));
                card.Rect.sizeDelta = shopCardSize;
                int k = 0;
                if (prog.Campaign.shop != null)
                    foreach (var item in prog.Campaign.shop)
                    {
                        if (item.tower != roster[i].id) continue;
                        var row2 = Instantiate(shopRowPrefab, card.rows);
                        row2.name = "Mejora_" + item.id;
                        row2.ItemId = item.id;
                        row2.buy.name = "Comprar_" + item.id;
                        row2.Rect.anchoredPosition = new Vector2(0f, -k * shopRowHeight);
                        string id = item.id;
                        row2.buy.onClick.AddListener(() => OnBuy(id));
                        shopRows.Add(row2);
                        k++;
                    }
                shopViews.Add((card, roster[i]));
            }
        }

        /// <summary>Actualiza la tienda en el lugar (precios, estados, confirmación).</summary>
        public void RebuildShop()
        {
            EnsureShop();
            shopCurrency.text = $"{prog.CurrencyName} {prog.Currency}";
            foreach (var (card, r) in shopViews)
            {
                bool unlocked = prog.IsTowerUnlocked(r.id);
                bool initial = Array.IndexOf(prog.Campaign.initialTowers, r.id) >= 0;
                card.panel.color = Palette.WithAlpha(Palette.PanelClaro, unlocked ? 0.95f : 0.5f);
                card.stripe.color = r.playable ? TowerColor(r.id) : Palette.Bloqueo;
                card.nameText.text = r.displayName;
                card.nameText.color = unlocked ? Palette.Texto : Palette.Bloqueo;
                card.statusText.text = initial ? "Torre inicial" : unlocked ? "Desbloqueada" : r.playable ? $"Bloqueada · {r.unlockLabel}" : r.unlockLabel;
                card.statusText.color = unlocked ? Palette.Acento : Palette.Bloqueo;
                card.tandaText.gameObject.SetActive(initial);
                card.futureText.gameObject.SetActive(!initial && !r.playable);
                card.rows.gameObject.SetActive(!initial && r.playable);
                if (initial)
                {
                    int tier = prog.TandaTier(r.id);
                    string eff = "";
                    if (prog.Campaign.tanda?.options != null) foreach (var o in prog.Campaign.tanda.options) if (o.tower == r.id) eff = o.description;
                    card.tandaText.text = $"<color=#45D0C4>Mejora de tanda:</color> {tier} {(tier == 1 ? "vez" : "veces")}\nCada una: {eff}\nSe gana al completar un continente.";
                }
            }
            foreach (var row in shopRows)
            {
                var item = prog.FindShopItem(row.ItemId);
                if (item == null) continue;
                bool unlocked = prog.IsTowerUnlocked(item.tower);
                row.text.text = $"{item.displayName}\n<size=17><color=#A9B0BB>{item.description}</color></size>";
                row.text.color = unlocked ? Palette.Texto : Palette.Bloqueo;
                string reason = prog.CannotBuyReason(item.id);
                string label; Color col;
                if (prog.IsPurchased(item.id)) { label = "Comprada"; col = Palette.Acento; }
                else if (!unlocked) { label = $"{item.cost} · bloqueada"; col = Palette.Bloqueo; }
                else if (armedBuy == item.id && Time.unscaledTime - armedBuyAt < 3f) { label = $"¿Confirmar? {item.cost}"; col = Palette.Oro; }
                else if (reason == "") { label = $"Comprar · {item.cost}"; col = Palette.Cristal; }
                else { label = $"{item.cost} · {reason}"; col = Palette.Invalido; }
                // todo precio responde al clic (Ley 4): lo que no se puede comprar suena a rechazo y el botón ya dice por qué
                row.buyLabel.text = label;
                row.buyLabel.color = col;
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

        // ------------------------------------------------------------------ aviso de primera vez (Doc 02 §13)
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
            tandaDescription.text = prog.Campaign.tanda.description;
            var opts = prog.Campaign.tanda.options;
            while (tandaViews.Count < opts.Length)
            {
                var v = Instantiate(tandaOptionPrefab, tandaOptions);
                v.button.onClick.AddListener(() => TandaChosen?.Invoke(v.TowerId));
                tandaViews.Add(v);
            }
            for (int i = 0; i < tandaViews.Count; i++)
            {
                var v = tandaViews[i];
                v.gameObject.SetActive(i < opts.Length);
                if (i >= opts.Length) continue;
                var o = opts[i];
                int tier = prog.TandaTier(o.tower);
                v.TowerId = o.tower;
                v.name = "Opcion_" + o.tower;
                v.Rect.anchoredPosition = new Vector2((i - (opts.Length - 1) * 0.5f) * 410f, -40f);
                v.stripe.color = TowerColor(o.tower);
                v.keyText.text = $"[{i + 1}]";
                v.nameText.text = TowerName(o.tower);
                v.effectText.text = $"{o.description}\n\nMejoras de tanda: {tier} → {tier + 1}";
            }
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
