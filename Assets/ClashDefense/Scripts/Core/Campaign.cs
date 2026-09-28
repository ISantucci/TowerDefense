using System;
using System.Collections.Generic;

namespace ClashDefense.Core
{
    // Progresión permanente del Mundo 1 (RQ-002.2, RQ-002.3; GDS-002.2 y GDS-002.3).
    // Datos de campaña (campaign_w1.json) + estado guardado (SaveData) + las reglas que los relacionan (Progression).
    // Pura: sin UnityEngine. Dónde se guarda el SaveData lo decide la capa Unity.

    [Serializable]
    public class CampaignData
    {
        public string version;
        public string currencyName;
        /// <summary>Multiplicador de la recompensa por estrellas (Doc 05 §9): [1★, 2★, 3★].</summary>
        public float[] starMultipliers;
        /// <summary>Qué fracción de la recompensa paga una victoria repetida (GDS-002.3 S18).</summary>
        public float replayFactor;
        public string[] initialTowers;
        /// <summary>Las doce torres de la beta, en orden de desbloqueo (Doc 04 §7).</summary>
        public RosterEntry[] roster;
        public WorldData[] worlds;
        public ShopItemData[] shop;
        public TandaData tanda;
        /// <summary>Invitación a seguir el proyecto al desbloquear el Mundo 2 (Doc 02 §16).</summary>
        public string followText;
    }

    [Serializable]
    public class RosterEntry
    {
        public string id;
        public string displayName;
        /// <summary>Si está implementada en esta build. Las del Mundo 2 se muestran bloqueadas.</summary>
        public bool playable;
        public string unlockLabel;
    }

    [Serializable]
    public class WorldData
    {
        public string id;
        public string displayName;
        /// <summary>Nivel que lo desbloquea. Vacío = abierto desde el comienzo.</summary>
        public string unlockedBy;
        public ContinentData[] continents;
    }

    [Serializable]
    public class ContinentData
    {
        public string id;
        public string displayName;
        public string theme;
        /// <summary>Falso = fuera de esta entrega (se ve como "próximamente", no como error).</summary>
        public bool available;
        public string unavailableText;
        public LevelMeta[] levels;
    }

    [Serializable]
    public class LevelMeta
    {
        public string id;
        public int number;
        public string displayName;
        /// <summary>Archivo de datos del nivel (sin extensión), en Data/.</summary>
        public string file;
        /// <summary>Torre que se desbloquea al completarlo (Doc 02 §7).</summary>
        public string rewardTower;
        /// <summary>Valor base de la moneda persistente (Doc 05 §9: base × multiplicador de estrellas).</summary>
        public int baseReward;
        /// <summary>Duración objetivo declarada por el owner (Doc 02 §5), para el mapa y la lectura.</summary>
        public string durationTarget;
        public bool available;
        /// <summary>Nivel del jefe final (estructura del mundo completo, fuera de la beta).</summary>
        public bool boss;
    }

    [Serializable]
    public class ShopItemData
    {
        public string id;
        public string tower;
        public string displayName;
        public string description;
        public int cost;
        public StatMod[] mods;
    }

    [Serializable]
    public class TandaData
    {
        public string description;
        public TandaOption[] options;
    }

    [Serializable]
    public class TandaOption
    {
        public string tower;
        public string description;
        public StatMod[] mods;
    }

    [Serializable]
    public class LevelRecord
    {
        public string id;
        public int stars;
        public int bestHp;
        public int wins;
        public int attempts;
        public int currencyEarned;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int currency;
        public LevelRecord[] levels = new LevelRecord[0];
        public string[] unlockedTowers = new string[0];
        public string[] purchased = new string[0];
        /// <summary>Una entrada por mejora de tanda elegida: la torre base que la recibió.</summary>
        public string[] tandaPicks = new string[0];
        /// <summary>Continentes cuya mejora de tanda ya se otorgó.</summary>
        public string[] claimedTandas = new string[0];
        /// <summary>Mejoras de tanda ganadas y todavía sin elegir.</summary>
        public int tandaPending;
        /// <summary>Torres ya anunciadas en una partida (mensaje de desbloqueo, UXS-002.4).</summary>
        public string[] seenTowers = new string[0];
        public bool tutorialDone;
        public bool tutorialRepeat;
        public bool noticeAccepted;
        public string[] seenWorlds = new string[0];
    }

    /// <summary>Resultado de aplicar una victoria a la progresión: lo que la pantalla de resultado tiene que contar.</summary>
    public sealed class VictoryResult
    {
        public int Currency;
        public bool FirstClear;
        public bool NewRecord;
        public int PreviousStars;
        public string TowerUnlocked;
        public string ContinentCompleted;
        public bool TandaEarned;
        public string WorldUnlocked;
    }

    public sealed class Progression
    {
        public CampaignData Campaign { get; }
        public SaveData Save { get; }

        public Progression(CampaignData campaign, SaveData save)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            Save = save ?? new SaveData();
            Normalize();
        }

        void Normalize()
        {
            var s = Save;
            s.levels = s.levels ?? new LevelRecord[0];
            s.unlockedTowers = s.unlockedTowers ?? new string[0];
            s.purchased = s.purchased ?? new string[0];
            s.tandaPicks = s.tandaPicks ?? new string[0];
            s.claimedTandas = s.claimedTandas ?? new string[0];
            s.seenTowers = s.seenTowers ?? new string[0];
            s.seenWorlds = s.seenWorlds ?? new string[0];
            if (Campaign.initialTowers != null)
                foreach (var t in Campaign.initialTowers) s.unlockedTowers = Add(s.unlockedTowers, t);
        }

        static string[] Add(string[] arr, string v)
        {
            if (string.IsNullOrEmpty(v) || Array.IndexOf(arr, v) >= 0) return arr;
            var n = new string[arr.Length + 1];
            Array.Copy(arr, n, arr.Length);
            n[arr.Length] = v;
            return n;
        }

        static int Count(string[] arr, string v) { int c = 0; foreach (var a in arr) if (a == v) c++; return c; }

        // ------------------------------------------------------------------ consulta
        public int Currency => Save.currency;
        public string CurrencyName => string.IsNullOrEmpty(Campaign.currencyName) ? "Cristales" : Campaign.currencyName;

        public IEnumerable<LevelMeta> AllLevels()
        {
            foreach (var w in Campaign.worlds)
                foreach (var c in w.continents)
                    foreach (var l in c.levels) yield return l;
        }

        public LevelMeta FindLevel(string id)
        {
            foreach (var l in AllLevels()) if (l.id == id) return l;
            return null;
        }

        public WorldData WorldOf(string levelId)
        {
            foreach (var w in Campaign.worlds) foreach (var c in w.continents) foreach (var l in c.levels) if (l.id == levelId) return w;
            return null;
        }

        public ContinentData ContinentOf(string levelId)
        {
            foreach (var w in Campaign.worlds) foreach (var c in w.continents) foreach (var l in c.levels) if (l.id == levelId) return c;
            return null;
        }

        /// <summary>Consulta de solo lectura: no agrega registros vacíos al guardado (revisión técnica, hallazgo 16).</summary>
        public LevelRecord Peek(string id)
        {
            foreach (var r in Save.levels) if (r.id == id) return r;
            return new LevelRecord { id = id };
        }

        public LevelRecord Record(string id)
        {
            foreach (var r in Save.levels) if (r.id == id) return r;
            var rec = new LevelRecord { id = id };
            var n = new LevelRecord[Save.levels.Length + 1];
            Array.Copy(Save.levels, n, Save.levels.Length);
            n[Save.levels.Length] = rec;
            Save.levels = n;
            return rec;
        }

        public int StarsOf(string id) { foreach (var r in Save.levels) if (r.id == id) return r.stars; return 0; }
        public bool Completed(string id) => StarsOf(id) > 0;
        public bool IsTowerUnlocked(string id) => Array.IndexOf(Save.unlockedTowers, id) >= 0;
        public bool IsPurchased(string shopId) => Array.IndexOf(Save.purchased, shopId) >= 0;
        public int TandaTier(string towerId) => Count(Save.tandaPicks, towerId);
        public int TotalStars() { int s = 0; foreach (var r in Save.levels) s += r.stars; return s; }

        public bool IsWorldUnlocked(WorldData w) => string.IsNullOrEmpty(w.unlockedBy) || Completed(w.unlockedBy);

        /// <summary>Un nivel se juega si está en esta entrega, su mundo está abierto y el anterior del mundo está completo.</summary>
        public bool IsLevelUnlocked(string id)
        {
            var lm = FindLevel(id);
            if (lm == null || !lm.available) return false;
            var w = WorldOf(id);
            if (w == null || !IsWorldUnlocked(w)) return false;
            LevelMeta prev = null;
            foreach (var c in w.continents)
                foreach (var l in c.levels)
                {
                    if (l.id == id) return prev == null || Completed(prev.id);
                    prev = l;
                }
            return false;
        }

        /// <summary>El siguiente nivel jugable del mismo mundo, o null.</summary>
        public LevelMeta NextLevel(string id)
        {
            var w = WorldOf(id);
            if (w == null) return null;
            bool found = false;
            foreach (var c in w.continents)
                foreach (var l in c.levels)
                {
                    if (found) return l.available ? l : null;
                    if (l.id == id) found = true;
                }
            return null;
        }

        /// <summary>Torres y mejoras permanentes con que el jugador entra a una partida (GDS-002.0).</summary>
        public MatchOptions Loadout(bool tutorial)
        {
            var mods = new Dictionary<string, List<StatMod>>();
            void AddMods(string tower, StatMod[] list)
            {
                if (list == null || string.IsNullOrEmpty(tower)) return;
                if (!mods.TryGetValue(tower, out var l)) { l = new List<StatMod>(); mods[tower] = l; }
                l.AddRange(list);
            }
            if (Campaign.shop != null)
                foreach (var item in Campaign.shop) if (IsPurchased(item.id)) AddMods(item.tower, item.mods);
            if (Campaign.tanda?.options != null)
                foreach (var pick in Save.tandaPicks)
                    foreach (var o in Campaign.tanda.options) if (o.tower == pick) AddMods(o.tower, o.mods);
            return new MatchOptions { Tutorial = tutorial, AllowedTowers = new List<string>(Save.unlockedTowers), Mods = mods };
        }

        public float StarMultiplier(int stars)
        {
            if (stars <= 0 || Campaign.starMultipliers == null || Campaign.starMultipliers.Length == 0) return 0f;
            int i = Math.Min(stars, Campaign.starMultipliers.Length) - 1;
            return Campaign.starMultipliers[i];
        }

        /// <summary>Lo que pagaría una victoria con esas estrellas, sin aplicarla (la pantalla lo puede anticipar).</summary>
        public int RewardFor(string levelId, int stars)
        {
            var lm = FindLevel(levelId);
            if (lm == null || stars <= 0) return 0;
            var rec = Peek(levelId);
            float m = StarMultiplier(stars);
            if (rec.wins == 0) return (int)Math.Round(lm.baseReward * m);
            int r = (int)Math.Round(lm.baseReward * Campaign.replayFactor * m);
            if (stars > rec.stars) r += (int)Math.Round(lm.baseReward * (m - StarMultiplier(rec.stars)));
            return r;
        }

        // ------------------------------------------------------------------ cambios
        public void ApplyStart(string levelId) => Record(levelId).attempts++;

        /// <summary>Doc 03 §19 y §22: estrellas, moneda, torre, tanda y mundo. La derrota no pasa por acá (§10: no entrega nada).</summary>
        public VictoryResult ApplyVictory(string levelId, int stars, int hp)
        {
            var res = new VictoryResult();
            var lm = FindLevel(levelId);
            if (lm == null || stars <= 0) return res;
            var rec = Record(levelId);
            var worldsBefore = new List<string>();
            foreach (var w in Campaign.worlds) if (IsWorldUnlocked(w)) worldsBefore.Add(w.id);

            res.Currency = RewardFor(levelId, stars);
            res.FirstClear = rec.wins == 0;
            res.PreviousStars = rec.stars;
            res.NewRecord = stars > rec.stars;
            rec.wins++;
            if (stars > rec.stars) rec.stars = stars;
            if (hp > rec.bestHp) rec.bestHp = hp;
            rec.currencyEarned += res.Currency;
            Save.currency += res.Currency;

            if (!string.IsNullOrEmpty(lm.rewardTower) && !IsTowerUnlocked(lm.rewardTower))
            {
                Save.unlockedTowers = Add(Save.unlockedTowers, lm.rewardTower);
                res.TowerUnlocked = lm.rewardTower;
            }

            var cont = ContinentOf(levelId);
            if (cont != null && Array.IndexOf(Save.claimedTandas, cont.id) < 0)
            {
                bool all = true;
                foreach (var l in cont.levels) if (!Completed(l.id)) { all = false; break; }
                if (all)
                {
                    Save.claimedTandas = Add(Save.claimedTandas, cont.id);
                    Save.tandaPending++;
                    res.TandaEarned = true;
                    res.ContinentCompleted = cont.id;
                }
            }

            foreach (var w in Campaign.worlds)
                if (IsWorldUnlocked(w) && !worldsBefore.Contains(w.id)) res.WorldUnlocked = w.id;
            return res;
        }

        public ShopItemData FindShopItem(string id)
        {
            if (Campaign.shop != null) foreach (var s in Campaign.shop) if (s.id == id) return s;
            return null;
        }

        /// <summary>"" si se puede comprar; si no, el motivo en palabras (UXS-002.3, ley 5).</summary>
        public string CannotBuyReason(string shopId)
        {
            var item = FindShopItem(shopId);
            if (item == null) return "no existe";
            if (IsPurchased(shopId)) return "ya comprada";
            if (!IsTowerUnlocked(item.tower)) return "torre bloqueada";
            if (Save.currency < item.cost) return $"faltan {item.cost - Save.currency}";
            return "";
        }

        public bool Buy(string shopId)
        {
            if (CannotBuyReason(shopId) != "") return false;
            var item = FindShopItem(shopId);
            Save.currency -= item.cost;
            Save.purchased = Add(Save.purchased, shopId);
            return true;
        }

        /// <summary>Aplica una mejora de tanda pendiente a una torre base (Doc 02 §8). Se puede repetir la misma torre.</summary>
        public bool ChooseTanda(string towerId)
        {
            if (Save.tandaPending <= 0 || Campaign.tanda?.options == null) return false;
            bool valid = false;
            foreach (var o in Campaign.tanda.options) if (o.tower == towerId) valid = true;
            if (!valid) return false;
            var n = new string[Save.tandaPicks.Length + 1];
            Array.Copy(Save.tandaPicks, n, Save.tandaPicks.Length);
            n[Save.tandaPicks.Length] = towerId;
            Save.tandaPicks = n;
            Save.tandaPending--;
            return true;
        }

        public void MarkTowerSeen(string id) => Save.seenTowers = Add(Save.seenTowers, id);
        public bool TowerSeen(string id) => Array.IndexOf(Save.seenTowers, id) >= 0;
        public void MarkWorldSeen(string id) => Save.seenWorlds = Add(Save.seenWorlds, id);
        public bool WorldSeen(string id) => Array.IndexOf(Save.seenWorlds, id) >= 0;

        /// <summary>Errores de los datos de campaña contra el balance (torres que no existen, niveles repetidos…).</summary>
        public static List<string> Validate(CampaignData c, BalanceData b)
        {
            var e = new List<string>();
            if (c == null) { e.Add("campaña: archivo vacío o ilegible"); return e; }
            var towers = new HashSet<string>();
            if (b?.towers != null) foreach (var t in b.towers) if (t != null) towers.Add(t.id);
            if (c.starMultipliers == null || c.starMultipliers.Length != 3) e.Add("campaña.starMultipliers: deben ser 3");
            if (c.replayFactor < 0 || c.replayFactor > 1) e.Add("campaña.replayFactor fuera de [0,1]");
            if (c.initialTowers == null || c.initialTowers.Length == 0) e.Add("campaña.initialTowers: vacío");
            else foreach (var t in c.initialTowers) if (!towers.Contains(t)) e.Add($"campaña.initialTowers: '{t}' no está en el balance");
            if (c.worlds == null || c.worlds.Length == 0) { e.Add("campaña.worlds: vacío"); return e; }
            var ids = new HashSet<string>();
            foreach (var w in c.worlds)
                foreach (var cont in w.continents ?? new ContinentData[0])
                    foreach (var l in cont.levels ?? new LevelMeta[0])
                    {
                        if (!ids.Add(l.id)) e.Add($"campaña: nivel '{l.id}' repetido");
                        if (l.available && string.IsNullOrEmpty(l.file)) e.Add($"campaña: nivel '{l.id}' disponible sin archivo");
                        if (l.available && !string.IsNullOrEmpty(l.rewardTower) && !towers.Contains(l.rewardTower)) e.Add($"campaña: premio '{l.rewardTower}' de '{l.id}' no está en el balance");
                        if (l.baseReward < 0) e.Add($"campaña: '{l.id}' baseReward < 0");
                    }
            foreach (var w in c.worlds)
                if (!string.IsNullOrEmpty(w.unlockedBy) && !ids.Contains(w.unlockedBy)) e.Add($"campaña: el mundo '{w.id}' lo desbloquea '{w.unlockedBy}', que no existe");
            if (c.shop != null)
                foreach (var s in c.shop)
                {
                    if (!towers.Contains(s.tower)) e.Add($"tienda '{s.id}': torre '{s.tower}' no está en el balance");
                    if (s.cost <= 0) e.Add($"tienda '{s.id}': cost <= 0");
                    if (s.mods == null || s.mods.Length == 0) e.Add($"tienda '{s.id}': sin modificadores");
                    else foreach (var m in s.mods) if (!StatMods.IsValid(m, out var err)) e.Add($"tienda '{s.id}': {err}");
                }
            if (c.tanda?.options != null)
                foreach (var o in c.tanda.options)
                {
                    if (!towers.Contains(o.tower)) e.Add($"tanda: torre '{o.tower}' no está en el balance");
                    if (o.mods != null) foreach (var m in o.mods) if (!StatMods.IsValid(m, out var err)) e.Add($"tanda '{o.tower}': {err}");
                }
            return e;
        }
    }
}
