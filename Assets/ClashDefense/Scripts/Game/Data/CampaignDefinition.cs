using System;
using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    [Serializable]
    public class RosterSlot
    {
        [Tooltip("Torre jugable. Vacío = torre futura (se muestra bloqueada con su nombre).")]
        public TowerDefinition tower;
        [Tooltip("Solo para torres futuras (sin asset todavía).")]
        public string id;
        public string displayName;
        public bool playable = true;
        public string unlockLabel;

        public string Id => tower != null ? tower.Id : id;
    }

    [Serializable]
    public class LevelEntry
    {
        [Tooltip("Datos y escena del nivel. Vacío = nivel fuera de esta entrega (se ve en el mapa, no se juega).")]
        public LevelDefinition level;
        [Tooltip("Solo si no hay nivel: id con el que existe en el mapa y en el guardado.")]
        public string id;
        public int number;
        public string displayName;
        [Tooltip("Torre que se desbloquea al completarlo (Doc 02 §7).")]
        public TowerDefinition rewardTower;
        [Tooltip("Solo para torres futuras (sin asset todavía): id de la torre que se desbloquea.")]
        public string rewardTowerId;
        [Tooltip("Valor base de la moneda persistente (Doc 05 §9: base × multiplicador de estrellas).")]
        public int baseReward;
        public string durationTarget;
        public bool available = true;
        [Tooltip("Nivel del jefe final (estructura del mundo completo, fuera de la beta).")]
        public bool boss;

        public string Id => level != null ? level.id : id;
        public string RewardId => rewardTower != null ? rewardTower.Id : rewardTowerId ?? "";
    }

    [Serializable]
    public class ContinentEntry
    {
        public string id;
        public string displayName;
        public LevelTheme theme;
        [Tooltip("Falso = fuera de esta entrega (se ve como \"próximamente\", no como error).")]
        public bool available = true;
        public string unavailableText;
        public List<LevelEntry> levels = new List<LevelEntry>();
    }

    [Serializable]
    public class WorldEntry
    {
        public string id;
        public string displayName;
        [Tooltip("Nivel que lo desbloquea (id). Vacío = abierto desde el comienzo.")]
        public string unlockedBy;
        public List<ContinentEntry> continents = new List<ContinentEntry>();
    }

    [Serializable]
    public class ShopItemEntry
    {
        public string id;
        public TowerDefinition tower;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public int cost;
        [Tooltip("stat: damage · interval · range · areaRadius · minRange · chainJumps · chainRadius · rampDps · goldPerSecond · goldCapacity · burnDps · burnDuration · flameWidth. op: mul | add.")]
        public StatMod[] mods = new StatMod[0];
    }

    [Serializable]
    public class TandaOptionEntry
    {
        public TowerDefinition tower;
        [TextArea(1, 3)] public string description;
        public StatMod[] mods = new StatMod[0];
    }

    [Serializable]
    public class TandaEntry
    {
        [TextArea(2, 4)] public string description;
        public List<TandaOptionEntry> options = new List<TandaOptionEntry>();
    }

    /// <summary>
    /// La campaña (RQ-002.2, RQ-002.3; GDS-002.2 y GDS-002.3): mundos, continentes y niveles, la moneda, la tienda y la mejora de
    /// tanda. Las torres y los niveles se referencian por asset: renombrar un archivo no rompe nada.
    /// </summary>
    [CreateAssetMenu(menuName = "Clash Defense/Datos/Campaña", fileName = "Campaña_", order = 2)]
    public sealed class CampaignDefinition : ScriptableObject
    {
        public string version = "w1-0.1";
        [Tooltip("Balance con el que se juegan los niveles de la campaña.")]
        public BalanceDefinition balance;
        public string currencyName = "Cristales";
        [Tooltip("Multiplicador de la recompensa por estrellas (Doc 05 §9): 1★, 2★, 3★.")]
        public float[] starMultipliers = { 0.5f, 0.75f, 1f };
        [Range(0f, 1f), Tooltip("Qué fracción de la recompensa paga una victoria repetida (GDS-002.3 S18).")]
        public float replayFactor = 0.25f;
        public List<TowerDefinition> initialTowers = new List<TowerDefinition>();
        [Tooltip("Las doce torres de la beta, en orden de desbloqueo (Doc 04 §7).")]
        public List<RosterSlot> roster = new List<RosterSlot>();
        public List<WorldEntry> worlds = new List<WorldEntry>();
        public List<ShopItemEntry> shop = new List<ShopItemEntry>();
        public TandaEntry tanda = new TandaEntry();
        [TextArea(2, 4), Tooltip("Invitación a seguir el proyecto al desbloquear el Mundo 2 (Doc 02 §16).")]
        public string followText;

        /// <summary>Niveles jugables de la campaña, en orden.</summary>
        public IEnumerable<LevelDefinition> Levels()
        {
            foreach (var w in worlds)
                foreach (var c in w.continents)
                    foreach (var l in c.levels)
                        if (l.level != null) yield return l.level;
        }

        public LevelDefinition FindLevel(string id)
        {
            foreach (var l in Levels()) if (l.id == id) return l;
            return null;
        }

        public ContinentEntry FindContinent(string id)
        {
            foreach (var w in worlds) foreach (var c in w.continents) if (c.id == id) return c;
            return null;
        }

        /// <summary>Copia para el núcleo (Progression).</summary>
        public CampaignData ToCore()
        {
            var c = new CampaignData
            {
                version = version,
                currencyName = currencyName,
                starMultipliers = starMultipliers != null ? (float[])starMultipliers.Clone() : new float[0],
                replayFactor = replayFactor,
                followText = followText,
            };
            var init = new List<string>();
            foreach (var t in initialTowers) if (t != null) init.Add(t.Id);
            c.initialTowers = init.ToArray();

            var roster = new List<RosterEntry>();
            foreach (var r in this.roster)
                roster.Add(new RosterEntry
                {
                    id = r.Id,
                    displayName = !string.IsNullOrEmpty(r.displayName) ? r.displayName : r.tower != null ? r.tower.data.displayName : r.Id,
                    playable = r.playable,
                    unlockLabel = r.unlockLabel,
                });
            c.roster = roster.ToArray();

            var worlds = new List<WorldData>();
            foreach (var w in this.worlds)
            {
                var conts = new List<ContinentData>();
                foreach (var ct in w.continents)
                {
                    var levels = new List<LevelMeta>();
                    foreach (var l in ct.levels)
                        levels.Add(new LevelMeta
                        {
                            id = l.Id,
                            number = l.number,
                            displayName = l.displayName,
                            file = l.level != null ? l.level.id : "",
                            rewardTower = l.RewardId,
                            baseReward = l.baseReward,
                            durationTarget = l.durationTarget,
                            available = l.available,
                            boss = l.boss,
                        });
                    conts.Add(new ContinentData
                    {
                        id = ct.id,
                        displayName = ct.displayName,
                        theme = ct.theme != null ? ct.theme.id : "",
                        available = ct.available,
                        unavailableText = ct.unavailableText,
                        levels = levels.ToArray(),
                    });
                }
                worlds.Add(new WorldData { id = w.id, displayName = w.displayName, unlockedBy = w.unlockedBy, continents = conts.ToArray() });
            }
            c.worlds = worlds.ToArray();

            var shop = new List<ShopItemData>();
            foreach (var s in this.shop)
                shop.Add(new ShopItemData
                {
                    id = s.id,
                    tower = s.tower != null ? s.tower.Id : "",
                    displayName = s.displayName,
                    description = s.description,
                    cost = s.cost,
                    mods = CloneMods(s.mods),
                });
            c.shop = shop.ToArray();

            var opts = new List<TandaOption>();
            if (tanda?.options != null)
                foreach (var o in tanda.options)
                    opts.Add(new TandaOption { tower = o.tower != null ? o.tower.Id : "", description = o.description, mods = CloneMods(o.mods) });
            c.tanda = new TandaData { description = tanda != null ? tanda.description : "", options = opts.ToArray() };
            return c;
        }

        static StatMod[] CloneMods(StatMod[] src)
        {
            if (src == null) return new StatMod[0];
            var r = new StatMod[src.Length];
            for (int i = 0; i < src.Length; i++) r[i] = new StatMod { stat = src[i].stat, op = src[i].op, value = src[i].value };
            return r;
        }

        /// <summary>Lo que el Inspector muestra en rojo.</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            if (balance == null) { e.Add("falta el balance de la campaña"); return e; }
            foreach (var w in worlds)
                foreach (var ct in w.continents)
                    foreach (var l in ct.levels)
                        if (l.available && ct.available && l.level == null) e.Add($"el nivel {l.number} de {ct.displayName} está disponible pero no tiene datos de nivel");
            e.AddRange(Progression.Validate(ToCore(), balance.ToCore()));
            return e;
        }
    }
}
