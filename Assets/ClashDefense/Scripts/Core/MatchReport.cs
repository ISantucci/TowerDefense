using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClashDefense.Core
{
    // Registro de una partida (RQ-001.7, MET-001.7). Arreglos de clases [Serializable]: JsonUtility y System.Text.Json lo escriben igual.

    [Serializable] public class MatchReport
    {
        public string schema = "clashdefense.partida/2";   // /2: recorridos, torres del Mundo 1 y oro recogido (GDS-002.0)
        public string balanceVersion;
        public string levelId;
        public string[] towersAvailable;     // torres que el jugador podía usar en esta partida
        public string buildVersion;
        public string platform;
        public string startedAtUtc;
        public string result;                 // victoria | derrota | abandono
        public float durationSeconds;         // activo: sin tutorial ni pausa
        public bool tutorialPlayed;
        public float realPauseSeconds;        // lo mide la presentación
        public int waveReached;
        public int waveCount;
        public int baseHpLeft;
        public int stars;
        public GoldReport gold = new GoldReport();
        public BuildRecord[] builds;
        public UpgradeRecord[] upgrades;
        public SellRecord[] sells;
        public TowerCountRecord[] towersAtEnd;
        public EnemyRecord[] enemies;
        public TowerDamageRecord[] damageByTower;
        public TypeDamageRecord[] damageByTowerType;
        public WaveRecord[] waves;
        public MetalReport metal = new MetalReport();
        public string[] checks;               // cierres aritméticos: "OK ..." o "FALLA ..."
    }

    [Serializable] public class GoldReport { public int start, earned, collected, spent, refunded, unspent; }
    [Serializable] public class BuildRecord { public float t; public int towerId; public string type; public int cost; public float x, z; }
    [Serializable] public class UpgradeRecord { public float t; public int towerId; public string type; public int cost; public int level; }
    [Serializable] public class SellRecord { public float t; public int towerId; public string type; public int level; public int refund; }
    [Serializable] public class TowerCountRecord { public string type; public int level1, level2; }
    [Serializable] public class EnemyRecord { public string type; public int spawned, killed, leaked, baseDamage; }
    [Serializable] public class TowerDamageRecord { public int towerId; public string type; public float damage; public float armorDamage; public int shots; public int immuneHits; public int kills; }
    [Serializable] public class TypeDamageRecord { public string type; public float damage; public float armorDamage; public int shots; public int immuneHits; public int kills; }
    [Serializable] public class WaveRecord { public int n; public float start, end, duration; public int spawned, killed, leaked; }
    [Serializable] public class MetalReport { public float firstImmuneHitT = -1f; public int wastedShots; public int immuneDiscoveries; public int armorsBroken; }

    /// <summary>Escucha los eventos del núcleo y arma el registro. No toca la simulación.</summary>
    public sealed class MatchRecorder
    {
        readonly List<BuildRecord> builds = new List<BuildRecord>();
        readonly List<UpgradeRecord> upgrades = new List<UpgradeRecord>();
        readonly List<SellRecord> sells = new List<SellRecord>();
        readonly Dictionary<string, EnemyRecord> enemies = new Dictionary<string, EnemyRecord>();
        readonly Dictionary<int, TowerDamageRecord> byTower = new Dictionary<int, TowerDamageRecord>();
        readonly Dictionary<int, WaveRecord> waves = new Dictionary<int, WaveRecord>();
        readonly Dictionary<int, int> enemyWave = new Dictionary<int, int>();
        readonly GoldReport gold = new GoldReport();
        readonly MetalReport metal = new MetalReport();
        readonly List<string> order = new List<string>();
        float lastTime;

        public void Consume(SimEvent e)
        {
            lastTime = e.Time;
            switch (e.Type)
            {
                case SimEventType.GoldChanged:
                    if (e.Text == "inicial") gold.start = e.Int1;
                    else if (e.Text == "baja") gold.earned += e.Int1;
                    else if (e.Text == "venta") gold.refunded += e.Int1;
                    else if (e.Text == "recoleccion") gold.collected += e.Int1;
                    else gold.spent += -e.Int1;
                    gold.unspent = e.Int2;
                    break;
                case SimEventType.TowerBuilt:
                    builds.Add(new BuildRecord { t = e.Time, towerId = e.TowerId, type = e.Text, cost = e.Int1, x = e.Pos.x, z = e.Pos.z });
                    Tower(e.TowerId, e.Text);
                    break;
                case SimEventType.TowerUpgraded:
                    upgrades.Add(new UpgradeRecord { t = e.Time, towerId = e.TowerId, type = e.Text, cost = e.Int1, level = e.Int2 });
                    break;
                case SimEventType.TowerSold:
                    sells.Add(new SellRecord { t = e.Time, towerId = e.TowerId, type = e.Text, refund = e.Int1, level = e.Int2 });
                    break;
                case SimEventType.WaveStarted:
                    waves[e.Int1] = new WaveRecord { n = e.Int1, start = e.Time, end = -1f };
                    break;
                case SimEventType.WaveCleared:
                    if (waves.TryGetValue(e.Int1, out var w)) { w.end = e.Time; w.duration = w.end - w.start; }
                    break;
                case SimEventType.EnemySpawned:
                    Enemy(e.Text).spawned++;
                    enemyWave[e.EnemyId] = e.Int1;
                    if (waves.TryGetValue(e.Int1, out var ws)) ws.spawned++;
                    break;
                case SimEventType.EnemyKilled:
                    Enemy(e.Text).killed++;
                    if (byTower.TryGetValue(e.TowerId, out var tk)) tk.kills++;
                    if (enemyWave.TryGetValue(e.EnemyId, out var wk) && waves.TryGetValue(wk, out var wkr)) wkr.killed++;
                    break;
                case SimEventType.EnemyReachedBase:
                    var er = Enemy(e.Text); er.leaked++; er.baseDamage += e.Int1;
                    if (enemyWave.TryGetValue(e.EnemyId, out var wl) && waves.TryGetValue(wl, out var wlr)) wlr.leaked++;
                    break;
                case SimEventType.Shot:
                    Tower(e.TowerId, e.Text).shots++;
                    break;
                case SimEventType.EnemyDamaged:
                    var td = Tower(e.TowerId, e.Text); td.damage += e.Float1; td.armorDamage += e.Float2;
                    break;
                case SimEventType.AttackImmune:
                    if (e.Int2 == 1) break;   // calentamiento de la Infernal contra metal: no es un disparo desperdiciado (GDS-002.4 S12)
                    Tower(e.TowerId, e.Text).immuneHits++;
                    metal.wastedShots++;
                    if (e.Int1 == 1) metal.immuneDiscoveries++;
                    if (metal.firstImmuneHitT < 0f) metal.firstImmuneHitT = e.Time;
                    break;
                case SimEventType.ArmorBroken:
                    metal.armorsBroken++;
                    break;
            }
        }

        EnemyRecord Enemy(string type)
        {
            if (!enemies.TryGetValue(type, out var r)) { r = new EnemyRecord { type = type }; enemies[type] = r; order.Add(type); }
            return r;
        }

        TowerDamageRecord Tower(int id, string type)
        {
            if (!byTower.TryGetValue(id, out var r)) { r = new TowerDamageRecord { towerId = id, type = type }; byTower[id] = r; }
            return r;
        }

        public MatchReport Build(Match m)
        {
            var rep = new MatchReport
            {
                balanceVersion = m.Balance.version,
                levelId = m.Level.id,
                result = m.Result ?? "en curso",
                durationSeconds = m.ActiveTime,
                tutorialPlayed = m.TutorialPlayed,
                waveReached = m.WaveNumber,
                waveCount = m.WaveCount,
                baseHpLeft = m.BaseHp,
                stars = m.Stars,
                gold = gold,
                towersAvailable = TypeIds(m),
                builds = builds.ToArray(),
                upgrades = upgrades.ToArray(),
                sells = sells.ToArray(),
                metal = metal,
            };
            gold.unspent = m.Gold;

            var counts = new List<TowerCountRecord>();
            foreach (var tt in m.TowerTypes)
            {
                var c = new TowerCountRecord { type = tt.id };
                foreach (var t in m.Towers) if (t.Type.id == tt.id) { if (t.Level == 1) c.level1++; else c.level2++; }
                counts.Add(c);
            }
            rep.towersAtEnd = counts.ToArray();

            var en = new List<EnemyRecord>();
            foreach (var et in m.Balance.enemies) en.Add(enemies.TryGetValue(et.id, out var r) ? r : new EnemyRecord { type = et.id });
            rep.enemies = en.ToArray();

            var dt = new List<TowerDamageRecord>(byTower.Values);
            dt.Sort((a, b) => a.towerId.CompareTo(b.towerId));
            rep.damageByTower = dt.ToArray();

            var byType = new List<TypeDamageRecord>();
            foreach (var tt in m.TowerTypes)
            {
                var r = new TypeDamageRecord { type = tt.id };
                foreach (var d in dt) if (d.type == tt.id) { r.damage += d.damage; r.armorDamage += d.armorDamage; r.shots += d.shots; r.immuneHits += d.immuneHits; r.kills += d.kills; }
                byType.Add(r);
            }
            rep.damageByTowerType = byType.ToArray();

            var wl = new List<WaveRecord>(waves.Values);
            wl.Sort((a, b) => a.n.CompareTo(b.n));
            foreach (var w in wl) if (w.end < 0f) { w.end = m.ActiveTime; w.duration = w.end - w.start; }
            rep.waves = wl.ToArray();

            // cierres aritméticos (RQ-001.7 CA 4)
            var checks = new List<string>();
            int expected = gold.start + gold.earned + gold.collected - gold.spent + gold.refunded;
            checks.Add((expected == m.Gold ? "OK" : "FALLA") + $" oro: {gold.start} + {gold.earned} + {gold.collected} - {gold.spent} + {gold.refunded} = {expected}; final {m.Gold}");
            int spawned = 0, resolved = 0, alive = 0;
            foreach (var r in rep.enemies) { spawned += r.spawned; resolved += r.killed + r.leaked; }
            foreach (var e in m.Enemies) if (e.Alive) alive++;
            checks.Add((spawned == resolved + alive ? "OK" : "FALLA") + $" enemigos: aparecidos {spawned} = eliminados+filtrados {resolved} + activos {alive}");
            rep.checks = checks.ToArray();
            return rep;
        }

        static string[] TypeIds(Match m)
        {
            var l = new List<string>();
            foreach (var t in m.TowerTypes) l.Add(t.id);
            return l.ToArray();
        }

        /// <summary>Resumen legible (RQ-001.7: "un resumen legible al terminar").</summary>
        public static string Summary(MatchReport r)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine($"Clash Defense · balance {r.balanceVersion} · nivel {r.levelId} · build {r.buildVersion}");
            sb.AppendLine($"Resultado: {r.result}  ·  oleada {r.waveReached}/{r.waveCount}  ·  vida {r.baseHpLeft}  ·  estrellas {r.stars}");
            sb.AppendLine($"Duración (sin tutorial ni pausa): {Clock(r.durationSeconds)}  ·  pausa real {r.realPauseSeconds.ToString("0", ci)} s  ·  tutorial: {(r.tutorialPlayed ? "sí" : "no")}");
            sb.AppendLine($"Oro: inicial {r.gold.start}, ganado {r.gold.earned}, recogido {r.gold.collected}, gastado {r.gold.spent}, reembolsado {r.gold.refunded}, sin usar {r.gold.unspent}");
            sb.AppendLine($"Construcciones {r.builds.Length} · mejoras {r.upgrades.Length} · ventas {r.sells.Length}");
            sb.Append("Torres al final:");
            foreach (var t in r.towersAtEnd) sb.Append($"  {t.type} N1×{t.level1} N2×{t.level2}");
            sb.AppendLine();
            sb.Append("Enemigos (eliminados/filtrados):");
            foreach (var e in r.enemies) sb.Append($"  {e.type} {e.killed}/{e.leaked}");
            sb.AppendLine();
            sb.Append("Daño por tipo de torre:");
            foreach (var d in r.damageByTowerType) sb.Append($"  {d.type} {(d.damage + d.armorDamage).ToString("0", ci)}");
            sb.AppendLine();
            sb.AppendLine($"Metal: primer ataque inmune {(r.metal.firstImmuneHitT < 0 ? "—" : Clock(r.metal.firstImmuneHitT))} · disparos desperdiciados {r.metal.wastedShots} · armaduras rotas {r.metal.armorsBroken}");
            foreach (var w in r.waves) sb.AppendLine($"  Oleada {w.n}: {w.duration.ToString("0.0", ci)} s · aparecidos {w.spawned} · eliminados {w.killed} · filtrados {w.leaked}");
            foreach (var c in r.checks) sb.AppendLine("  " + c);
            return sb.ToString();
        }

        public static string Clock(float s)
        {
            // se redondea a décimas ANTES de partir en minutos: 59,97 s es 1:00.0, no 0:60.0
            int tenths = (int)Math.Round(Math.Max(0f, s) * 10.0, MidpointRounding.AwayFromZero);
            int m = tenths / 600;
            float sec = (tenths % 600) / 10f;
            return $"{m}:{sec.ToString("00.0", CultureInfo.InvariantCulture)}";
        }
    }
}
