using System;
using System.Collections.Generic;
using System.IO;
using ClashDefense.Core;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Migración de TL-003: pasa el proyecto de "todo por código y JSON" a assets de Unity que se ven y se editan.
    ///  · datos → ScriptableObjects (balance, torres, enemigos, niveles, campaña, temas) en Assets/ClashDefense/Data;
    ///  · formas que se armaban en cada partida → prefabs (torres, enemigos, proyectiles, efectos, HUD y menús);
    ///  · cada nivel → su escena en Scenes/Niveles, con caminos, rocas y base como objetos que se mueven con el mouse;
    ///  · escenas de arranque (juego y Prototipo 0) con sus referencias, y Build Settings.
    /// Lee los JSON de Tools/ClashDefenseSim/Datos y, al final, exporta los datos desde los assets y los compara con esos
    /// mismos JSON: si todo da igual, la migración no cambió ningún número. Se puede correr dos veces (actualiza en el lugar).
    /// </summary>
    public static class ProjectMigration
    {
        public const string DataRoot = "Assets/ClashDefense/Data";
        public const string ScenesRoot = "Assets/ClashDefense/Scenes";
        public const string P0Scene = ScenesRoot + "/Prototipo0.unity";
        public const string Version = "w1-0.1";
        public const string CampaignConfigPath = DataRoot + "/Juego_Mundo1.asset";
        public const string P0ConfigPath = DataRoot + "/Juego_Prototipo0.asset";
        const string ReportDir = "Logs/ClashDefenseMigracion";

        public static void EnsureFolder(string path) => AssetUtil.EnsureFolder(path);

        [MenuItem("Clash Defense/Mantenimiento/Migrar datos, prefabs y escenas (TL-003)", priority = 90)]
        static void Menu()
        {
            if (!EditorUtility.DisplayDialog("Clash Defense", "Regenera los assets de datos, los prefabs, las escenas de nivel y las de arranque a partir de los JSON de Tools/ClashDefenseSim/Datos. " +
                                             "Lo que se haya editado en esos assets desde la migración se pisa.", "Migrar", "Cancelar")) return;
            var r = Run();
            Debug.Log("[ClashDefense] " + r);
        }

        /// <summary>Punto de entrada (menú y bandeja). Devuelve el informe; empieza con ERROR si no pudo.</summary>
        public static string Run()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty) return $"ERROR: la escena abierta '{s.path}' tiene cambios sin guardar. No se toca: guardala o descartala y volvé a correr.";
            }
            var log = new List<string>();
            try
            {
                string report = Migrate(log);
                Directory.CreateDirectory(ReportDir);
                File.WriteAllText(Path.Combine(ReportDir, "informe.txt"), report);
                return report;
            }
            catch (Exception ex)
            {
                log.Add("ERROR: " + ex);
                string report = "ERROR en la migración\n - " + string.Join("\n - ", log);
                Directory.CreateDirectory(ReportDir);
                File.WriteAllText(Path.Combine(ReportDir, "informe.txt"), report);
                return report;
            }
        }

        static Dictionary<string, string> ReadOriginals(List<string> log)
        {
            var d = new Dictionary<string, string>();
            if (!Directory.Exists(DataExporter.OutDir)) throw new Exception($"no está la carpeta {DataExporter.OutDir} con los JSON");
            foreach (var f in Directory.GetFiles(DataExporter.OutDir, "*.json")) d[Path.GetFileName(f)] = File.ReadAllText(f);
            log.Add($"JSON leídos de {DataExporter.OutDir}: {d.Count}");
            return d;
        }

        static T Json<T>(Dictionary<string, string> src, string file)
        {
            if (!src.TryGetValue(file, out var text)) throw new Exception($"falta {DataExporter.OutDir}/{file}");
            return JsonUtility.FromJson<T>(text);
        }

        static string Migrate(List<string> log)
        {
            var originals = ReadOriginals(log);
            var w1 = Json<BalanceData>(originals, "balance_w1.json");
            var p0 = Json<BalanceData>(originals, "balance_p0.json");
            var camp = Json<CampaignData>(originals, "campaign_w1.json");
            var p0Level = Json<LevelData>(originals, "level_p0.json");

            MaterialLibrary.Reset();
            PresentationBuilder.Reset();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // escena de trabajo, no se guarda

            // ---- presentación: temas, efectos, prefabs de torres, enemigos y proyectiles
            var themes = PresentationBuilder.Themes(log);
            var lib = PresentationBuilder.Library(log);
            var towerPrefabs = new Dictionary<string, TowerVisual>();
            var projectiles = new Dictionary<string, ProjectileVisual>();
            var enemyPrefabs = new Dictionary<string, EnemyVisual>();
            foreach (var bal in new[] { w1, p0 })
            {
                foreach (var t in bal.towers)
                    if (!towerPrefabs.ContainsKey(t.id)) { towerPrefabs[t.id] = PresentationBuilder.Tower(t); projectiles[t.id] = PresentationBuilder.Projectile(t.id); }
                foreach (var e in bal.enemies)
                    if (!enemyPrefabs.ContainsKey(e.id)) enemyPrefabs[e.id] = PresentationBuilder.Enemy(e);
            }
            log.Add($"prefabs: {towerPrefabs.Count} torres, {enemyPrefabs.Count} enemigos, {new HashSet<ProjectileVisual>(projectiles.Values).Count - (projectiles.ContainsValue(null) ? 1 : 0)} proyectiles");

            // ---- interfaz
            var star = HudBuilder.Star();
            var card = HudBuilder.Card();
            var hud = HudBuilder.Build(card, star);
            var menus = MenusBuilder.Build(star);
            log.Add("interfaz: prefabs Hud, Menus, TarjetaTorre, PestanaMundo, ColumnaContinente, TarjetaNivel, TarjetaTienda, FilaMejora, OpcionTanda");

            // ---- datos
            var balW1 = MakeBalance(w1, "Mundo1", "Balance_Mundo1", towerPrefabs, projectiles, enemyPrefabs);
            var balP0 = MakeBalance(p0, "Laboratorio", "Balance_P0", towerPrefabs, projectiles, enemyPrefabs);
            log.Add($"balances: Mundo 1 ({balW1.towers.Count} torres, {balW1.enemies.Count} enemigos) y Laboratorio ({balP0.towers.Count} torres, {balP0.enemies.Count} enemigos, {balP0.waves.Count} oleadas)");

            // rutas, no referencias: cada escena nueva (modo Single) descarga los assets que ninguna escena usa
            var levels = new List<(string path, LevelData data)>();
            var byId = new Dictionary<string, LevelDefinition>();
            for (int wi = 0; wi < camp.worlds.Length; wi++)
                foreach (var ct in camp.worlds[wi].continents)
                    foreach (var lm in ct.levels)
                    {
                        if (string.IsNullOrEmpty(lm.file)) continue;
                        var data = Json<LevelData>(originals, lm.file + ".json");
                        var def = MakeLevel(data, $"M{wi + 1}_N{lm.number}_{AssetUtil.Pascal(lm.displayName)}", balW1, themes);
                        levels.Add((AssetDatabase.GetAssetPath(def), data));
                        byId[lm.id] = def;
                    }
            var p0Def = MakeLevel(p0Level, "P0_MapaGris", balP0, themes);
            levels.Add((AssetDatabase.GetAssetPath(p0Def), p0Level));
            log.Add($"niveles: {levels.Count} ({levels.Count - 1} de la campaña y el del Laboratorio)");

            var campaign = MakeCampaign(camp, balW1, byId, themes);
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            cfg.version = Version; cfg.campaign = campaign; cfg.labBalance = balP0; cfg.labLevel = p0Def; cfg.presentation = lib;
            cfg = AssetUtil.Save(cfg, CampaignConfigPath);
            var cfgP0 = ScriptableObject.CreateInstance<GameConfig>();
            cfgP0.version = Version; cfgP0.campaign = null; cfgP0.labBalance = balP0; cfgP0.labLevel = p0Def; cfgP0.presentation = lib;
            cfgP0 = AssetUtil.Save(cfgP0, P0ConfigPath);
            string hudPath = AssetDatabase.GetAssetPath(hud), menusPath = AssetDatabase.GetAssetPath(menus);
            AssetDatabase.SaveAssets();
            log.Add("configuración: Juego_Mundo1 (campaña + Laboratorio) y Juego_Prototipo0");

            // ---- escenas de nivel (hornean la forma en cada LevelDefinition al guardarse)
            foreach (var (path, data) in levels)
                LevelSceneBuilder.Build(path, data, $"{LevelSceneBuilder.LevelScenes}/{Path.GetFileNameWithoutExtension(path)}.unity", log);

            // ---- escenas de arranque y Build Settings
            BootScene(GameBootstrap.GameScenePath, CampaignConfigPath, hudPath, menusPath, log);
            BootScene(P0Scene, P0ConfigPath, hudPath, null, log);
            BuildSettings(levels, log);
            PlayerSettings.productName = "Clash Defense";
            PlayerSettings.bundleVersion = Version;
            AssetDatabase.SaveAssets();

            // ---- prueba de equivalencia: lo exportado desde los assets contra los JSON originales
            var exported = DataExporter.Files(AssetDatabase.LoadAssetAtPath<GameConfig>(CampaignConfigPath));
            var cmp = DataExporter.Compare(originals, exported);
            int diffs = 0; foreach (var c in cmp) if (!c.EndsWith(": igual")) diffs++;
            Directory.CreateDirectory(Path.Combine(ReportDir, "original"));
            foreach (var kv in originals) File.WriteAllText(Path.Combine(ReportDir, "original", kv.Key), kv.Value);
            DataExporter.ExportAll(false);
            log.Add($"equivalencia con los JSON originales: {(diffs == 0 ? "TODO IGUAL" : diffs + " DISTINTOS")}");
            foreach (var c in cmp) log.Add("  " + c);
            log.Add($"originales guardados en {ReportDir}/original; exportación nueva en {DataExporter.OutDir}");

            EditorSceneManager.OpenScene(GameBootstrap.GameScenePath);
            return (diffs == 0 ? "OK" : "OK CON DIFERENCIAS") + " migración TL-003\n - " + string.Join("\n - ", log);
        }

        // ------------------------------------------------------------------ datos
        static BalanceDefinition MakeBalance(BalanceData d, string folder, string name, Dictionary<string, TowerVisual> tp, Dictionary<string, ProjectileVisual> pp, Dictionary<string, EnemyVisual> ep)
        {
            var b = ScriptableObject.CreateInstance<BalanceDefinition>();
            b.version = d.version; b.source = d.source;
            b.economy = d.economy ?? new EconomyData(); b.timing = d.timing ?? new TimingData(); b.stars = d.stars ?? new StarsData();
            foreach (var t in d.towers)
            {
                var td = ScriptableObject.CreateInstance<TowerDefinition>();
                td.data = t; td.color = Palette.ForTower(t.id); td.prefab = tp[t.id]; td.projectile = pp[t.id];
                b.towers.Add(AssetUtil.Save(td, $"{DataRoot}/{folder}/Torres/Torre_{PresentationBuilder.FileName(t.id)}.asset"));
            }
            foreach (var e in d.enemies)
            {
                var ed = ScriptableObject.CreateInstance<EnemyDefinition>();
                ed.data = e; ed.color = Palette.ForEnemy(e.id); ed.prefab = ep[e.id];
                b.enemies.Add(AssetUtil.Save(ed, $"{DataRoot}/{folder}/Enemigos/Enemigo_{PresentationBuilder.FileName(e.id)}.asset"));
            }
            b.waves = d.waves != null ? new List<WaveData>(d.waves) : new List<WaveData>();
            return AssetUtil.Save(b, $"{DataRoot}/{folder}/{name}.asset");
        }

        static LevelDefinition MakeLevel(LevelData data, string name, BalanceDefinition bal, Dictionary<string, LevelTheme> themes)
        {
            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.id = data.id;
            def.displayName = data.displayName;
            themes.TryGetValue(data.theme ?? "", out def.theme);
            def.balance = bal;
            def.tutorialTower = string.IsNullOrEmpty(data.tutorialTowerId) ? null : bal.Tower(data.tutorialTowerId);
            def.waves = data.waves != null ? new List<WaveData>(data.waves) : new List<WaveData>();
            // forma provisoria (la escena la vuelve a hornear al guardarse): así el asset nunca queda vacío
            var routes = LevelGeometry.RoutePoints(data);
            var r = new RouteData[routes.Count];
            for (int i = 0; i < r.Length; i++) r[i] = new RouteData { points = routes[i] };
            def.SetShape(new LevelShape
            {
                routes = r, pathWidth = data.pathWidth, buildArea = data.buildArea ?? new RectData(), blocked = data.blocked ?? new CircleData[0],
                baseRadius = data.baseRadius, tutorialHint = data.tutorialHint ?? new CircleData(),
            });
            return AssetUtil.Save(def, $"{DataRoot}/Niveles/{name}.asset");
        }

        static CampaignDefinition MakeCampaign(CampaignData d, BalanceDefinition bal, Dictionary<string, LevelDefinition> levels, Dictionary<string, LevelTheme> themes)
        {
            var c = ScriptableObject.CreateInstance<CampaignDefinition>();
            c.version = d.version; c.balance = bal; c.currencyName = d.currencyName;
            c.starMultipliers = d.starMultipliers ?? new float[0]; c.replayFactor = d.replayFactor; c.followText = d.followText;
            c.initialTowers = new List<TowerDefinition>();
            foreach (var id in d.initialTowers ?? new string[0]) c.initialTowers.Add(bal.Tower(id));
            c.roster = new List<RosterSlot>();
            foreach (var r in d.roster ?? new RosterEntry[0])
                c.roster.Add(new RosterSlot { tower = bal.Tower(r.id), id = r.id, displayName = r.displayName, playable = r.playable, unlockLabel = r.unlockLabel });
            c.worlds = new List<WorldEntry>();
            foreach (var w in d.worlds)
            {
                var we = new WorldEntry { id = w.id, displayName = w.displayName, unlockedBy = w.unlockedBy };
                foreach (var ct in w.continents)
                {
                    themes.TryGetValue(ct.theme ?? "", out var theme);
                    var ce = new ContinentEntry { id = ct.id, displayName = ct.displayName, theme = string.IsNullOrEmpty(ct.theme) ? null : theme, available = ct.available, unavailableText = ct.unavailableText };
                    foreach (var l in ct.levels)
                    {
                        levels.TryGetValue(l.id, out var def);
                        var reward = string.IsNullOrEmpty(l.rewardTower) ? null : bal.Tower(l.rewardTower);
                        ce.levels.Add(new LevelEntry
                        {
                            level = string.IsNullOrEmpty(l.file) ? null : def, id = l.id, number = l.number, displayName = l.displayName,
                            rewardTower = reward, rewardTowerId = reward == null ? l.rewardTower : "", baseReward = l.baseReward,
                            durationTarget = l.durationTarget, available = l.available, boss = l.boss,
                        });
                    }
                    we.continents.Add(ce);
                }
                c.worlds.Add(we);
            }
            c.shop = new List<ShopItemEntry>();
            foreach (var s in d.shop ?? new ShopItemData[0])
                c.shop.Add(new ShopItemEntry { id = s.id, tower = bal.Tower(s.tower), displayName = s.displayName, description = s.description, cost = s.cost, mods = s.mods ?? new StatMod[0] });
            c.tanda = new TandaEntry { description = d.tanda?.description };
            if (d.tanda?.options != null)
                foreach (var o in d.tanda.options)
                    c.tanda.options.Add(new TandaOptionEntry { tower = bal.Tower(o.tower), description = o.description, mods = o.mods ?? new StatMod[0] });
            return AssetUtil.Save(c, $"{DataRoot}/Mundo1/Campaña_Mundo1.asset");
        }

        // ------------------------------------------------------------------ escenas de arranque
        static void BootScene(string path, string cfgPath, string hudPath, string menusPath, List<string> log)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(cfgPath);
            var hudPrefab = AssetDatabase.LoadAssetAtPath<Hud>(hudPath);
            var menusPrefab = menusPath != null ? AssetDatabase.LoadAssetAtPath<Menus>(menusPath) : null;
            if (cfg == null || hudPrefab == null || (menusPath != null && menusPrefab == null)) throw new Exception($"faltan assets para {path}: {cfgPath}, {hudPath}, {menusPath}");
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>().orthographic = true;
            camGo.AddComponent<AudioListener>();
            var lightGo = new GameObject("Luz");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.62f);
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var boot = new GameObject("ClashDefense");
            var gb = boot.AddComponent<GameBootstrap>();
            gb.config = cfg;
            var hudGo = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab.gameObject, scene);
            gb.hud = hudGo.GetComponent<Hud>();
            if (menusPrefab != null)
            {
                var mGo = (GameObject)PrefabUtility.InstantiatePrefab(menusPrefab.gameObject, scene);
                gb.menus = mGo.GetComponent<Menus>();
            }
            AssetUtil.EnsureFolder(Path.GetDirectoryName(path));
            if (!EditorSceneManager.SaveScene(scene, path)) throw new Exception("no se pudo guardar " + path);
            log.Add($"escena de arranque {path}: {(cfg.IsCampaign ? "campaña" : "Prototipo 0")}, HUD{(menusPrefab != null ? " y menús" : "")} como instancias de prefab");
        }

        static void BuildSettings(List<(string path, LevelData data)> levels, List<string> log)
        {
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(GameBootstrap.GameScenePath, true) };
            foreach (var (path, _) in levels)
            {
                var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (def != null && !string.IsNullOrEmpty(def.ScenePath)) list.Add(new EditorBuildSettingsScene(def.ScenePath, true));
            }
            list.Add(new EditorBuildSettingsScene(P0Scene, false));
            foreach (var s in EditorBuildSettings.scenes)
                if (!list.Exists(x => x.path == s.path)) list.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = list.ToArray();
            log.Add($"Build Settings: juego primero, {levels.Count} escenas de nivel habilitadas (se cargan al jugar), Prototipo0 y {list.Count - levels.Count - 2} escenas viejas deshabilitadas");
        }
    }
}
