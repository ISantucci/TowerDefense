using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Un balance completo (RQ-001.8, GDS-002.0): economía, tiempos, estrellas y la lista de torres y enemigos que existen
    /// en él. Hay dos: el del Prototipo 0 (Laboratorio, Doc 05 v1.0 sin tocar) y el del Mundo 1. Todo cambio de balance se
    /// anota con versión nueva en CHANGELOG_balance.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Clash Defense/Datos/Balance", fileName = "Balance_", order = 1)]
    public sealed class BalanceDefinition : ScriptableObject
    {
        [Tooltip("Versión del balance: viaja en cada evento del registro (MET-002.7). Cambiala cuando cambies un número.")]
        public string version = "w1-0.1";
        [TextArea(2, 6)] public string source;
        public EconomyData economy = new EconomyData();
        public TimingData timing = new TimingData();
        public StarsData stars = new StarsData();
        [Tooltip("Torres de este balance, en el orden de las tarjetas.")]
        public List<TowerDefinition> towers = new List<TowerDefinition>();
        [Tooltip("Enemigos de este balance.")]
        public List<EnemyDefinition> enemies = new List<EnemyDefinition>();
        [Tooltip("Oleadas del balance: solo las usa el Laboratorio (P0). Los niveles de la campaña traen las suyas.")]
        public List<WaveData> waves = new List<WaveData>();

        public TowerDefinition Tower(string id)
        {
            foreach (var t in towers) if (t != null && t.Id == id) return t;
            return null;
        }

        public EnemyDefinition Enemy(string id)
        {
            foreach (var e in enemies) if (e != null && e.Id == id) return e;
            return null;
        }

        /// <summary>Copia para el núcleo. Las listas vacías salen como en el JSON de siempre.</summary>
        public BalanceData ToCore()
        {
            var b = new BalanceData
            {
                version = version,
                source = source,
                economy = DataCopy.Clone(economy),
                timing = DataCopy.Clone(timing),
                stars = DataCopy.Clone(stars),
            };
            var t = new List<TowerTypeData>();
            foreach (var d in towers) if (d != null) t.Add(d.ToCore());
            b.towers = t.ToArray();
            var e = new List<EnemyTypeData>();
            foreach (var d in enemies) if (d != null) e.Add(d.ToCore());
            b.enemies = e.ToArray();
            if (waves != null && waves.Count > 0)
            {
                var w = new List<WaveData>();
                foreach (var x in waves) w.Add(DataCopy.Clone(x));
                b.waves = w.ToArray();
            }
            return b;
        }

        /// <summary>Lo que el Inspector muestra en rojo. Vacío = el balance valida.</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            var ids = new HashSet<string>();
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i] == null) { e.Add($"torres: el lugar {i} está vacío"); continue; }
                if (!ids.Add(towers[i].Id)) e.Add($"torres: '{towers[i].Id}' está dos veces");
            }
            ids.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == null) { e.Add($"enemigos: el lugar {i} está vacío"); continue; }
                if (!ids.Add(enemies[i].Id)) e.Add($"enemigos: '{enemies[i].Id}' está dos veces");
            }
            e.AddRange(DataValidator.ValidateBalance(ToCore()));
            return e;
        }
    }
}
