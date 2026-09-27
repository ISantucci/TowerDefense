using System;
using System.Collections.Generic;

namespace ClashDefense.Core
{
    /// <summary>Validación de los archivos de datos (RQ-001.8 CA 3): un dato faltante o inválido se reporta, no se adivina.</summary>
    public static class DataValidator
    {
        public static List<string> Validate(BalanceData b, LevelData l)
        {
            var e = new List<string>();
            if (b == null) { e.Add("balance: archivo vacío o ilegible"); return e; }
            if (string.IsNullOrEmpty(b.version)) e.Add("balance.version: falta");
            if (b.economy == null) e.Add("balance.economy: falta");
            else
            {
                if (b.economy.startGold < 0) e.Add("economy.startGold < 0");
                if (b.economy.baseHp <= 0) e.Add("economy.baseHp <= 0");
                if (b.economy.upgradeCostFactor <= 0) e.Add("economy.upgradeCostFactor <= 0");
                if (b.economy.sellRefundFactor < 0 || b.economy.sellRefundFactor > 1) e.Add("economy.sellRefundFactor fuera de [0,1]");
            }
            if (b.timing == null) e.Add("balance.timing: falta");
            else
            {
                if (b.timing.waveGap < 0) e.Add("timing.waveGap < 0");
                if (b.timing.countdownStep <= 0) e.Add("timing.countdownStep <= 0");
                if (b.timing.defenseDuration <= 0) e.Add("timing.defenseDuration <= 0");
            }
            if (b.stars == null) e.Add("balance.stars: falta");
            else if (b.stars.twoStarsMinHp > b.stars.threeStarsMinHp) e.Add("stars: 2 estrellas exige más vida que 3");

            var towerIds = new HashSet<string>();
            if (b.towers == null || b.towers.Length == 0) e.Add("balance.towers: vacío");
            else foreach (var t in b.towers)
            {
                string n = $"tower '{t?.id}'";
                if (t == null || string.IsNullOrEmpty(t.id)) { e.Add("tower sin id"); continue; }
                if (!towerIds.Add(t.id)) e.Add($"{n}: id repetido");
                if (t.cost <= 0) e.Add($"{n}: cost <= 0");
                if (!t.targetsAir && !t.targetsGround) e.Add($"{n}: no ataca ninguna capa");
                if (t.attack != "single" && t.attack != "area") e.Add($"{n}: attack debe ser single|area");
                if (t.projectileSpeed <= 0) e.Add($"{n}: projectileSpeed <= 0");
                if (t.footprintRadius <= 0) e.Add($"{n}: footprintRadius <= 0");
                if (t.metalEfficiency < 0) e.Add($"{n}: metalEfficiency < 0");
                if (t.levels == null || t.levels.Length == 0) e.Add($"{n}: sin niveles");
                else for (int i = 0; i < t.levels.Length; i++)
                {
                    var lv = t.levels[i];
                    if (lv == null) { e.Add($"{n} N{i + 1}: falta"); continue; }
                    if (lv.damage <= 0) e.Add($"{n} N{i + 1}: damage <= 0");
                    if (lv.interval <= 0) e.Add($"{n} N{i + 1}: interval <= 0");
                    if (lv.range <= 0) e.Add($"{n} N{i + 1}: range <= 0");
                    if (t.attack == "area" && lv.areaRadius <= 0) e.Add($"{n} N{i + 1}: areaRadius <= 0 en torre de área");
                }
            }

            var codes = new HashSet<string>();
            if (b.enemies == null || b.enemies.Length == 0) e.Add("balance.enemies: vacío");
            else foreach (var en in b.enemies)
            {
                string n = $"enemy '{en?.id}'";
                if (en == null || string.IsNullOrEmpty(en.id)) { e.Add("enemy sin id"); continue; }
                if (string.IsNullOrEmpty(en.code)) e.Add($"{n}: sin code");
                else if (!codes.Add(en.code)) e.Add($"{n}: code '{en.code}' repetido");
                if (en.layer != "ground" && en.layer != "air") e.Add($"{n}: layer debe ser ground|air");
                if (en.hp <= 0) e.Add($"{n}: hp <= 0");
                if (en.armor < 0) e.Add($"{n}: armor < 0");
                if (en.travelTime <= 0) e.Add($"{n}: travelTime <= 0");
                if (en.armor > 0 && en.travelTimeExposed <= 0) e.Add($"{n}: blindado sin travelTimeExposed");
                if (en.baseDamage < 0) e.Add($"{n}: baseDamage < 0");
                if (en.gold < 0) e.Add($"{n}: gold < 0");
            }

            if (b.waves == null || b.waves.Length == 0) e.Add("balance.waves: vacío");
            else for (int w = 0; w < b.waves.Length; w++)
            {
                var wd = b.waves[w];
                if (wd == null) { e.Add($"wave {w + 1}: falta"); continue; }
                if (wd.spawnInterval <= 0) e.Add($"wave {w + 1}: spawnInterval <= 0");
                try
                {
                    var seq = WaveSequence.Expand(wd.sequence);
                    if (seq.Count == 0) e.Add($"wave {w + 1}: secuencia vacía");
                    foreach (var c in seq) if (!codes.Contains(c)) e.Add($"wave {w + 1}: código '{c}' no es un enemigo");
                }
                catch (FormatException ex) { e.Add($"wave {w + 1}: {ex.Message}"); }
            }

            if (l == null) { e.Add("level: archivo vacío o ilegible"); return e; }
            if (string.IsNullOrEmpty(l.id)) e.Add("level.id: falta");
            if (l.path == null || l.path.Length < 2) e.Add("level.path: necesita al menos 2 puntos");
            if (l.pathWidth <= 0) e.Add("level.pathWidth <= 0");
            if (l.buildArea == null) e.Add("level.buildArea: falta");
            else if (l.buildArea.maxX <= l.buildArea.minX || l.buildArea.maxZ <= l.buildArea.minZ) e.Add("level.buildArea: rectángulo vacío");
            if (l.baseRadius <= 0) e.Add("level.baseRadius <= 0");
            return e;
        }
    }
}
