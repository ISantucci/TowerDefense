using System;
using System.Collections.Generic;

namespace ClashDefense.Core
{
    /// <summary>Recorridos de un nivel (GDS-002.6): los del Mundo 1 vienen en routes; el P0 trae un único path.</summary>
    public static class LevelGeometry
    {
        public static List<Vec2[]> RoutePoints(LevelData l)
        {
            var list = new List<Vec2[]>();
            if (l == null) return list;
            if (l.routes != null)
                foreach (var r in l.routes)
                    if (r != null && r.points != null && r.points.Length >= 2) list.Add(r.points);
            if (list.Count == 0 && l.path != null && l.path.Length >= 2) list.Add(l.path);
            return list;
        }

        /// <summary>Entradas distintas (primer punto de cada recorrido, sin repetir).</summary>
        public static List<Vec2> Spawns(LevelData l)
        {
            var list = new List<Vec2>();
            foreach (var r in RoutePoints(l))
            {
                bool seen = false;
                foreach (var s in list) if (Vec2.Distance(s, r[0]) < 0.01f) { seen = true; break; }
                if (!seen) list.Add(r[0]);
            }
            return list;
        }

        /// <summary>Llegadas distintas a la base: dirección del último tramo de cada recorrido (Doc 03 §9: una o dos).</summary>
        public static List<Vec2> ArrivalDirections(LevelData l)
        {
            var list = new List<Vec2>();
            foreach (var r in RoutePoints(l))
            {
                var d = r[r.Length - 1] - r[r.Length - 2];
                float m = d.Magnitude;
                if (m < 1e-4f) continue;
                d = d * (1f / m);
                bool seen = false;
                foreach (var s in list) if (Vec2.Dot(s, d) > 0.99f) { seen = true; break; }
                if (!seen) list.Add(d);
            }
            return list;
        }
    }

    /// <summary>Aplica las mejoras permanentes (tienda e insignias, GDS-002.3, GDS-004.2) sobre una copia del tipo de torre. El balance no se toca.</summary>
    public static class StatMods
    {
        public static TowerTypeData Apply(TowerTypeData src, IList<StatMod> mods)
        {
            var t = new TowerTypeData
            {
                id = src.id, displayName = src.displayName, shortName = src.shortName, description = src.description,
                cost = src.cost, targetsGround = src.targetsGround, targetsAir = src.targetsAir, metalEfficiency = src.metalEfficiency,
                attack = src.attack, projectileSpeed = src.projectileSpeed, footprintRadius = src.footprintRadius,
                levels = new TowerLevelData[src.levels.Length],
            };
            for (int i = 0; i < src.levels.Length; i++) t.levels[i] = Copy(src.levels[i]);
            if (mods == null) return t;
            // 1. mul y add, en orden (tienda). La velocidad del proyectil es del tipo: se aplica una vez.
            foreach (var m in mods)
            {
                if (m == null || m.op == "pct") continue;
                if (m.stat == "projectileSpeed") { t.projectileSpeed = Op(t.projectileSpeed, m); continue; }
                foreach (var lv in t.levels) ApplyOne(lv, m);
            }
            // 2. pct: los porcentajes de la misma estadística se suman y se aplican una vez sobre el nivel temporal (Doc 05 v2.0 §7.1)
            var pct = new Dictionary<string, float>();
            foreach (var m in mods)
                if (m != null && m.op == "pct") pct[m.stat] = (pct.TryGetValue(m.stat, out var v) ? v : 0f) + m.value;
            foreach (var kv in pct)
            {
                var f = new StatMod { stat = kv.Key, op = "mul", value = 1f + kv.Value / 100f };
                if (kv.Key == "projectileSpeed") { t.projectileSpeed = R2(Op(t.projectileSpeed, f)); continue; }
                foreach (var lv in t.levels)
                {
                    ApplyOne(lv, f);
                    Round(lv, kv.Key);
                }
            }
            return t;
        }

        static float R2(float v) => (float)Math.Round(v, 2, MidpointRounding.AwayFromZero);

        /// <summary>Redondeo de las insignias: daño al entero más cercano, alcance y área (y el resto) a dos decimales.</summary>
        static void Round(TowerLevelData lv, string stat)
        {
            switch (stat)
            {
                case "damage": lv.damage = (float)Math.Round(lv.damage, 0, MidpointRounding.AwayFromZero); break;
                case "range": lv.range = R2(lv.range); break;
                case "areaRadius": lv.areaRadius = R2(lv.areaRadius); break;
                case "interval": lv.interval = R2(lv.interval); break;
            }
        }

        static TowerLevelData Copy(TowerLevelData s) => new TowerLevelData
        {
            damage = s.damage, interval = s.interval, range = s.range, areaRadius = s.areaRadius,
            minRange = s.minRange, flightTime = s.flightTime, chainJumps = s.chainJumps, chainRadius = s.chainRadius,
            rampDps = s.rampDps != null ? (float[])s.rampDps.Clone() : null, rampStep = s.rampStep,
            rampTimes = s.rampTimes != null ? (float[])s.rampTimes.Clone() : null,
            goldPerSecond = s.goldPerSecond, goldCapacity = s.goldCapacity, goldPerCycle = s.goldPerCycle, goldCycle = s.goldCycle,
            burnDps = s.burnDps, burnDuration = s.burnDuration, flameWidth = s.flameWidth,
            pulseInterval = s.pulseInterval, burstDuration = s.burstDuration,
        };

        static float Op(float v, StatMod m) => m.op == "add" ? v + m.value : v * m.value;

        static void ApplyOne(TowerLevelData lv, StatMod m)
        {
            switch (m.stat)
            {
                case "damage": lv.damage = (float)Math.Round(Op(lv.damage, m), 2); break;
                case "interval": lv.interval = Op(lv.interval, m); break;
                case "range": lv.range = Op(lv.range, m); break;
                case "areaRadius": lv.areaRadius = Op(lv.areaRadius, m); break;
                case "minRange": lv.minRange = Math.Max(0f, Op(lv.minRange, m)); break;
                case "chainJumps": lv.chainJumps = (int)Math.Round(Op(lv.chainJumps, m)); break;
                case "chainRadius": lv.chainRadius = Op(lv.chainRadius, m); break;
                case "rampDps": if (lv.rampDps != null) for (int i = 0; i < lv.rampDps.Length; i++) lv.rampDps[i] = Op(lv.rampDps[i], m); break;
                case "goldPerSecond": lv.goldPerSecond = Op(lv.goldPerSecond, m); break;
                case "goldCapacity": lv.goldCapacity = (float)Math.Round(Op(lv.goldCapacity, m)); break;
                case "burnDps": lv.burnDps = Op(lv.burnDps, m); break;
                case "burnDuration": lv.burnDuration = Op(lv.burnDuration, m); break;
                case "flameWidth": lv.flameWidth = Op(lv.flameWidth, m); break;
                case "flightTime": lv.flightTime = R2(Op(lv.flightTime, m)); break;
                case "rampTimes":
                    if (lv.rampTimes != null) for (int i = 0; i < lv.rampTimes.Length; i++) lv.rampTimes[i] = R2(Op(lv.rampTimes[i], m));
                    else if (lv.rampStep > 0f) lv.rampStep = R2(Op(lv.rampStep, m));
                    break;
                case "goldCycle": lv.goldCycle = R2(Op(lv.goldCycle, m)); break;
                case "goldPerCycle": lv.goldPerCycle = Op(lv.goldPerCycle, m); break;
            }
        }

        public static bool IsValid(StatMod m, out string error)
        {
            error = null;
            if (m == null) { error = "modificador vacío"; return false; }
            if (Array.IndexOf(StatMod.Stats, m.stat) < 0) { error = $"stat '{m.stat}' desconocida"; return false; }
            if (m.op != "mul" && m.op != "add" && m.op != "pct") { error = $"op '{m.op}' debe ser mul|add|pct"; return false; }
            if (m.op == "pct" && m.value <= -100f) { error = "pct <= -100"; return false; }
            if (m.op == "mul" && m.value <= 0f) { error = "mul <= 0"; return false; }
            return true;
        }
    }
}
