using System;
using System.Collections.Generic;

namespace ClashDefense.Core
{
    /// <summary>Validación de los archivos de datos (RQ-001.8 CA 3, GDS-002.0): un dato faltante o inválido se reporta, no se adivina.</summary>
    public static class DataValidator
    {
        public static List<string> Validate(BalanceData b, LevelData l)
        {
            var e = ValidateBalance(b);
            if (b == null) return e;
            e.AddRange(ValidateLevel(b, l));
            return e;
        }

        public static List<string> ValidateBalance(BalanceData b)
        {
            var e = new List<string>();
            if (b == null) { e.Add("balance: archivo vacío o ilegible"); return e; }
            if (string.IsNullOrEmpty(b.version)) e.Add("balance.version: falta");
            if (b.economy == null) e.Add("balance.economy: falta");
            else
            {
                if (b.economy.startGold < 0) e.Add("economy.startGold < 0");
                if (b.economy.tutorialStartGold < 0) e.Add("economy.tutorialStartGold < 0");
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
                if (b.timing.burnTick < 0) e.Add("timing.burnTick < 0");
                if (b.towers != null && b.timing.burnTick <= 0)
                    foreach (var t in b.towers)
                        if (t != null && t.attack == AttackKind.Flame) { e.Add($"timing.burnTick <= 0 y la torre '{t.id}' quema: hace falta el tick de la quemadura"); break; }
            }
            if (b.stars == null) e.Add("balance.stars: falta");
            else if (b.stars.twoStarsMinHp > b.stars.threeStarsMinHp) e.Add("stars: 2 estrellas exige más vida que 3");

            var towerIds = new HashSet<string>();
            if (b.towers == null || b.towers.Length == 0) e.Add("balance.towers: vacío");
            else foreach (var t in b.towers) ValidateTower(t, towerIds, e);

            var codes = new HashSet<string>();
            if (b.enemies == null || b.enemies.Length == 0) e.Add("balance.enemies: vacío");
            else foreach (var en in b.enemies)
            {
                string n = $"enemy '{en?.id}'";
                if (en == null || string.IsNullOrEmpty(en.id)) { e.Add("enemy sin id"); continue; }
                if (string.IsNullOrEmpty(en.code)) e.Add($"{n}: sin code");
                else if (en.code == WaveSequence.Gap) e.Add($"{n}: '.' está reservado para el lugar vacío");
                else if (!codes.Add(en.code)) e.Add($"{n}: code '{en.code}' repetido");
                if (en.layer != "ground" && en.layer != "air") e.Add($"{n}: layer debe ser ground|air");
                if (en.hp <= 0) e.Add($"{n}: hp <= 0");
                if (en.armor < 0) e.Add($"{n}: armor < 0");
                if (en.travelTime <= 0) e.Add($"{n}: travelTime <= 0");
                if (en.armor > 0 && en.travelTimeExposed <= 0) e.Add($"{n}: blindado sin travelTimeExposed");
                if (en.baseDamage < 0) e.Add($"{n}: baseDamage < 0");
                if (en.gold < 0) e.Add($"{n}: gold < 0");
            }
            if (b.waveRules != null)
            {
                if (b.waveRules.minibossAfter < 0 || b.waveRules.minibossAfter > 1) e.Add("waveRules.minibossAfter fuera de [0,1]");
                if (b.waveRules.minibossGap < 0) e.Add("waveRules.minibossGap < 0");
                try
                {
                    var order = WaveSequence.Expand(b.waveRules.order ?? "");
                    if (order.Count == 0) e.Add("waveRules.order: vacío");
                    foreach (var c in order) if (!codes.Contains(c)) e.Add($"waveRules.order: '{c}' no es un enemigo");
                }
                catch (FormatException ex) { e.Add($"waveRules.order: {ex.Message}"); }
            }
            if (b.waves != null) ValidateWaves("balance", b.waves, codes, 1, e, b.waveRules);
            return e;
        }

        static void ValidateTower(TowerTypeData t, HashSet<string> ids, List<string> e)
        {
            string n = $"tower '{t?.id}'";
            if (t == null || string.IsNullOrEmpty(t.id)) { e.Add("tower sin id"); return; }
            if (!ids.Add(t.id)) e.Add($"{n}: id repetido");
            if (t.cost <= 0) e.Add($"{n}: cost <= 0");
            if (!AttackKind.IsKnown(t.attack)) { e.Add($"{n}: attack '{t.attack}' desconocido"); return; }
            bool gold = t.attack == AttackKind.Gold;
            if (!gold && !t.targetsAir && !t.targetsGround) e.Add($"{n}: no ataca ninguna capa");
            if ((t.attack == AttackKind.Single || t.attack == AttackKind.Area) && t.projectileSpeed <= 0) e.Add($"{n}: projectileSpeed <= 0");
            if (t.footprintRadius <= 0) e.Add($"{n}: footprintRadius <= 0");
            if (t.metalEfficiency < 0) e.Add($"{n}: metalEfficiency < 0");
            if (t.attack == AttackKind.Inferno && t.metalEfficiency <= 0) e.Add($"{n}: el Infernal necesita metalEfficiency > 0 para su etapa máxima");
            if (t.levels == null || t.levels.Length == 0) { e.Add($"{n}: sin niveles"); return; }
            for (int i = 0; i < t.levels.Length; i++)
            {
                var lv = t.levels[i];
                string ln = $"{n} N{i + 1}";
                if (lv == null) { e.Add($"{ln}: falta"); continue; }
                if (gold)
                {
                    if (lv.goldCycle > 0) { if (lv.goldPerCycle <= 0) e.Add($"{ln}: goldPerCycle <= 0 con goldCycle"); }
                    else if (lv.goldPerSecond <= 0) e.Add($"{ln}: ni goldPerSecond ni goldCycle");
                    if (lv.goldCapacity < 1) e.Add($"{ln}: goldCapacity < 1");
                    continue;
                }
                if (lv.interval <= 0) e.Add($"{ln}: interval <= 0");
                if (lv.range <= 0) e.Add($"{ln}: range <= 0");
                switch (t.attack)
                {
                    case AttackKind.Inferno:
                        if (lv.rampDps == null || lv.rampDps.Length == 0) e.Add($"{ln}: rampDps vacío");
                        else foreach (var d in lv.rampDps) if (d <= 0) { e.Add($"{ln}: rampDps con valor <= 0"); break; }
                        if (lv.rampTimes != null && lv.rampTimes.Length > 0)
                        {
                            if (lv.rampDps == null || lv.rampTimes.Length != lv.rampDps.Length) e.Add($"{ln}: rampTimes y rampDps deben tener el mismo largo");
                            else if (lv.rampTimes[0] != 0f) e.Add($"{ln}: rampTimes debe empezar en 0");
                            else for (int k = 1; k < lv.rampTimes.Length; k++) if (lv.rampTimes[k] <= lv.rampTimes[k - 1]) { e.Add($"{ln}: rampTimes no crece"); break; }
                        }
                        else if (lv.rampStep <= 0) e.Add($"{ln}: ni rampStep ni rampTimes");
                        break;
                    case AttackKind.Mortar:
                        if (lv.damage <= 0) e.Add($"{ln}: damage <= 0");
                        if (lv.areaRadius <= 0) e.Add($"{ln}: areaRadius <= 0 en Mortero");
                        if (lv.flightTime <= 0) e.Add($"{ln}: flightTime <= 0");
                        if (lv.minRange < 0 || lv.minRange >= lv.range) e.Add($"{ln}: minRange fuera de [0, range)");
                        break;
                    case AttackKind.Chain:
                        if (lv.damage <= 0) e.Add($"{ln}: damage <= 0");
                        if (lv.chainJumps < 0) e.Add($"{ln}: chainJumps < 0");
                        if (lv.chainJumps > 0 && lv.chainRadius <= 0) e.Add($"{ln}: chainRadius <= 0 con rebotes");
                        break;
                    case AttackKind.Flame:
                        if (lv.damage <= 0) e.Add($"{ln}: damage <= 0");
                        if (lv.flameWidth <= 0) e.Add($"{ln}: flameWidth <= 0");
                        if (lv.burnDps < 0 || lv.burnDuration < 0) e.Add($"{ln}: quemadura negativa");
                        if (lv.pulseInterval < 0 || lv.burstDuration < 0) e.Add($"{ln}: pulsos negativos");
                        if (lv.pulseInterval > 0 && lv.burstDuration < lv.pulseInterval) e.Add($"{ln}: burstDuration menor que pulseInterval");
                        break;
                    default:
                        if (lv.damage <= 0) e.Add($"{ln}: damage <= 0");
                        if (t.attack == AttackKind.Area && lv.areaRadius <= 0) e.Add($"{ln}: areaRadius <= 0 en torre de área");
                        break;
                }
            }
        }

        static void ValidateWaves(string owner, WaveData[] waves, HashSet<string> codes, int routeCount, List<string> e, WaveRulesData rules)
        {
            for (int w = 0; w < waves.Length; w++)
            {
                var wd = waves[w];
                string n = $"{owner} wave {w + 1}";
                if (wd == null) { e.Add($"{n}: falta"); continue; }
                if (wd.spawnInterval <= 0) e.Add($"{n}: spawnInterval <= 0");
                if (wd.IsComposition)
                {
                    if (rules == null) e.Add($"{n}: usa composition y el balance no trae waveRules");
                    try
                    {
                        int real = 0;
                        foreach (var kv in WaveSchedule.ParseComposition(wd.composition))
                        {
                            real += kv.Value;
                            if (!codes.Contains(kv.Key)) e.Add($"{n}: código '{kv.Key}' no es un enemigo");
                        }
                        if (real == 0) e.Add($"{n}: composición sin enemigos");
                    }
                    catch (FormatException ex) { e.Add($"{n}: {ex.Message}"); }
                    if (!string.IsNullOrEmpty(wd.miniboss) && !codes.Contains(wd.miniboss)) e.Add($"{n}: miniboss '{wd.miniboss}' no es un enemigo");
                    continue;
                }
                try
                {
                    var seq = WaveSequence.Expand(wd.sequence);
                    int real = 0;
                    foreach (var c in seq)
                    {
                        if (c == WaveSequence.Gap) continue;
                        real++;
                        if (!codes.Contains(c)) e.Add($"{n}: código '{c}' no es un enemigo");
                    }
                    if (real == 0) e.Add($"{n}: secuencia sin enemigos");
                }
                catch (FormatException ex) { e.Add($"{n}: {ex.Message}"); }
                if (!string.IsNullOrEmpty(wd.lanes))
                {
                    try
                    {
                        var lanes = WaveSequence.Expand(wd.lanes);
                        if (lanes.Count == 0) e.Add($"{n}: lanes vacío");
                        foreach (var l in lanes)
                            if (!int.TryParse(l, out int r) || r < 0 || r >= routeCount) { e.Add($"{n}: lane '{l}' no es un recorrido (0..{routeCount - 1})"); break; }
                    }
                    catch (FormatException ex) { e.Add($"{n} lanes: {ex.Message}"); }
                }
            }
        }

        public static List<string> ValidateLevel(BalanceData b, LevelData l)
        {
            var e = new List<string>();
            if (l == null) { e.Add("level: archivo vacío o ilegible"); return e; }
            string n = $"level '{l.id}'";
            if (string.IsNullOrEmpty(l.id)) e.Add("level.id: falta");
            var routes = LevelGeometry.RoutePoints(l);
            if (l.routes != null)
                for (int i = 0; i < l.routes.Length; i++)
                    if (l.routes[i] == null || l.routes[i].points == null || l.routes[i].points.Length < 2)
                        e.Add($"{n}: el recorrido {i} tiene menos de 2 puntos (correría los índices de los carriles)");
            if (routes.Count == 0) e.Add($"{n}: necesita path o routes con al menos 2 puntos");
            if (l.pathWidth <= 0) e.Add($"{n}: pathWidth <= 0");
            if (l.buildArea == null) e.Add($"{n}: buildArea falta");
            else if (l.buildArea.maxX <= l.buildArea.minX || l.buildArea.maxZ <= l.buildArea.minZ) e.Add($"{n}: buildArea es un rectángulo vacío");
            if (l.baseRadius <= 0) e.Add($"{n}: baseRadius <= 0");
            if (routes.Count > 1)
            {
                var end = routes[0][routes[0].Length - 1];
                for (int i = 1; i < routes.Count; i++)
                    if (Vec2.Distance(routes[i][routes[i].Length - 1], end) > 0.01f) e.Add($"{n}: el recorrido {i} no termina en la base");
            }
            var codes = new HashSet<string>();
            if (b?.enemies != null) foreach (var en in b.enemies) if (en != null && !string.IsNullOrEmpty(en.code)) codes.Add(en.code);
            bool levelWaves = l.waves != null && l.waves.Length > 0;
            if (levelWaves) ValidateWaves(n, l.waves, codes, Math.Max(1, routes.Count), e, b?.waveRules);
            else if (b?.waves == null || b.waves.Length == 0) e.Add($"{n}: sin oleadas (ni en el nivel ni en el balance)");
            else if (routes.Count > 1) e.Add($"{n}: varios recorridos pero las oleadas del balance no los reparten");
            if (!string.IsNullOrEmpty(l.tutorialTowerId) && b?.towers != null && Array.Find(b.towers, t => t != null && t.id == l.tutorialTowerId) == null)
                e.Add($"{n}: tutorialTowerId '{l.tutorialTowerId}' no es una torre");
            return e;
        }
    }
}
