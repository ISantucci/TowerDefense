using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ClashDefense.Game
{
    /// <summary>
    /// Raíz de composición del juego (TL-003): lee el GameConfig, valida los datos, arma las piezas (cámara, mundo, HUD, menús,
    /// sonido, registro) y las entrega al flujo (GameFlow) y a la partida (MatchController). No tiene reglas de juego ni de
    /// navegación: antes de TL-003 era un "manager dios" de 900 líneas (Vaultrum Core: Riesgo de manager dios).
    /// Con campaña en el GameConfig es el juego; sin ella, el Prototipo 0 como se entregó.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Juego")]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string GameScenePath = "Assets/ClashDefense/Scenes/ClashDefense.unity";

        [Tooltip("Qué juego se arma: campaña, Laboratorio y presentación.")]
        [SerializeField] internal GameConfig config;
        [Tooltip("HUD de la partida (instancia del prefab UI/Prefabs/Hud).")]
        [SerializeField] internal Hud hud;
        [Tooltip("Menús de la campaña (instancia del prefab UI/Prefabs/Menus). Vacío en el Prototipo 0.")]
        [SerializeField] internal Menus menus;

        static LevelDefinition testLevel;
        static Scene testScene;

        GameServices s;
        MatchController mc;
        GameFlow flow;

        public Match CurrentMatch => mc?.Match;
        public int MaxStepsPerFrame { get => mc != null ? mc.MaxStepsPerFrame : 8; set { if (mc != null) mc.MaxStepsPerFrame = value; } }
        public GameConfig Config => config;

        /// <summary>
        /// Play desde la escena de un nivel (TL-003): se abre la escena del juego al lado y arranca directo en ese nivel.
        /// Solo en el editor: en una build el juego siempre empieza en su escena.
        /// </summary>
        public static void RequestLevelTest(LevelDefinition def, Scene levelScene)
        {
#if UNITY_EDITOR
            if (testLevel != null) return;
            testLevel = def;
            testScene = levelScene;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(GameScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
        }

        // ------------------------------------------------------------------ arranque
        // En Start y no en Awake: el HUD y los menús se enlazan en su propio Awake, y Unity no garantiza el orden de los Awake
        // entre objetos de una escena (depende del archivo). Todos los Awake corren antes que cualquier Start (orden 054:
        // con la composición en Awake, los menús todavía no estaban enlazados y el juego arrancaba roto).
        void Start()
        {
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            // el teclado lo maneja el juego (UXS-001.6/002.x): si el EventSystem también manda Submit al último botón clickeado,
            // Enter actúa dos veces (revisión técnica, hallazgo 4)
            var evs = EventSystem.current != null ? EventSystem.current : FindAnyObjectByType<EventSystem>();
            if (evs != null) evs.sendNavigationEvents = false;

            var cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }

            var errors = new List<string>();
            if (config == null) errors.Add("GameBootstrap: falta el GameConfig");
            if (hud == null) errors.Add("GameBootstrap: falta el HUD (prefab UI/Prefabs/Hud)");
            if (config != null && config.IsCampaign && menus == null) errors.Add("GameBootstrap: la campaña necesita los menús (prefab UI/Prefabs/Menus)");
            if (hud == null) { Debug.LogError("[ClashDefense] " + string.Join("\n", errors)); return; }

            s = new GameServices
            {
                Config = config,
                Camera = cam,
                Hud = hud,
                Menus = menus,
                Stage = new LevelStage(),
                Sfx = gameObject.AddComponent<Sfx>(),
                Metrics = new MetricsWriter(),
            };
            if (menus != null && (config == null || !config.IsCampaign)) menus.gameObject.SetActive(false);
            mc = new MatchController(s);
            if (config != null)
            {
                flow = new GameFlow(s, mc, config);
                errors.AddRange(flow.Load());
            }
            if (errors.Count > 0)
            {
                Debug.LogError("[ClashDefense] Datos inválidos:\n - " + string.Join("\n - ", errors));
                hud.ShowError(errors);
                return;
            }
            var world = new GameObject("Mundo").AddComponent<WorldView>();
            world.transform.SetParent(transform, false);
            world.Init(config.presentation, cam, s.Stage);
            s.World = world;
            hud.SetTiming((config.IsCampaign ? config.campaign.balance : config.labBalance).timing);

            var pending = testLevel;
            testLevel = null;
            if (pending != null && gameObject.scene.IsValid()) SceneManager.SetActiveScene(gameObject.scene);   // la luz y el cielo son los del juego
            flow.Start(pending, testScene);
            QaStarted = true;
        }

        void OnDestroy() { testLevel = null; }

        void OnApplicationQuit() { flow?.OnApplicationQuit(); }

        // ------------------------------------------------------------------ cuadro a cuadro
        void Update()
        {
            if (flow == null || s.World == null) { if (hud != null && hud.ErrorVisible && Input.GetKeyDown(KeyCode.S)) Quit(); return; }
            s.Rig?.Tick();
            flow.HandleKeys();
            mc.Tick();
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ API de QA
        // La usan los pilotos (solo en el editor). Recorre los mismos caminos que la entrada del jugador,
        // salvo el mouse físico: el puntero se fija en un punto del mundo.
        /// <summary>El arranque terminó sin excepciones (si Start lanza, Unity deshabilita el componente y nada avanza).</summary>
        public bool QaStarted { get; private set; }
        public Hud QaHud => hud;
        public Menus QaMenus => menus;
        public WorldView QaWorld => s?.World;
        public MetricsWriter QaMetrics => s?.Metrics;
        public Progression QaProgression => flow?.Progression;
        public bool QaCampaign => flow != null && flow.IsCampaign;
        public GameConfig QaConfig => config;
        public string QaSelectedType => mc.SelectedType;
        public string QaLevelId => flow.CurrentLevelId;
        public void QaTakeControl() { mc.QaDriven = true; }
        public void QaStart(bool tutorial) { if (flow.IsCampaign) flow.OpenLab(); flow.StartP0(tutorial); }
        public void QaSelectType(string id) => mc.SelectType(id);
        public void QaPointerAt(Vector3? world) => mc.QaPointer = world;
        public void QaSelectTower(int id) => mc.SelectTower(id);
        public void QaUpgrade() => mc.Upgrade();
        public void QaSell() => mc.Sell();
        public void QaCollect() => mc.Collect();
        public void QaCancel() => mc.ClearSelection();
        public void QaPause() => mc.Pause();
        public void QaResume() => mc.Resume();
        public void QaRestart() => flow.Restart();
        public void QaExitToStart() => flow.ExitFromMatch();
        public void QaNext() => flow.NextLevel();
        public void QaTutorialNext() => mc.TutorialNext();
        public void QaUpgradeHover(bool on) { mc.SetUpgradeHover(on); hud.ShowUpgradePreview(on); }
        public void QaShowTitle() => flow.ShowTitle();
        public void QaShowMap(string id) => flow.ShowMap(id);
        public void QaShowShop() => menus.ShowShop(MenuScreen.Map);
        public void QaShowOptions() => menus.ShowOptions(MenuScreen.Title, s.Sfx.Volume, s.Sfx.Muted);
        public void QaStartLevel(string id, bool tutorial) => flow.StartLevel(id, tutorial);
        public void QaBuy(string id) => flow.Buy(id);
        public void QaChooseTanda(string tower) => flow.ChooseTanda(tower);
        public void QaShowTanda() => menus.ShowTanda();
        public void QaAcceptNotice() => flow.AcceptNotice();
        public void QaResetProgress() => flow.ResetProgress();
        /// <summary>El piloto juega con un guardado propio (y opcionalmente vacío): nunca toca el progreso del owner.</summary>
        public void QaUseSaveKey(string key, bool fresh) => flow.UseSaveKey(key, fresh);
        /// <summary>Clic izquierdo en el punto del mundo, por el mismo camino que el mouse.</summary>
        public bool QaClick(Vector3 world) => mc.QaClick(world);
    }
}
