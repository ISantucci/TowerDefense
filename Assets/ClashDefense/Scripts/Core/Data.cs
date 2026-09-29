using System;

namespace ClashDefense.Core
{
    // Formas de datos del balance (RQ-001.8, GDS-002.0) y de los niveles (LDS-001.1, LDS-002.6).
    // Campos públicos y [Serializable]: los lee JsonUtility en Unity y System.Text.Json en la herramienta de simulación.
    // Ningún número de balance vive en la lógica: todo sale de acá. Un campo que falta en el JSON vale 0 / vacío,
    // y el validador dice si ese vacío es legal para ese tipo de torre, enemigo o nivel.

    [Serializable]
    public class BalanceData
    {
        public string version;
        public string source;
        public EconomyData economy;
        public TimingData timing;
        public StarsData stars;
        public TowerTypeData[] towers;
        public EnemyTypeData[] enemies;
        /// <summary>Oleadas del P0 (Doc 05 §8). Los niveles del Mundo 1 traen las suyas en LevelData.waves.</summary>
        public WaveData[] waves;
        /// <summary>Reglas para convertir una composición en orden de aparición (Doc 05 v2.0 §9.2). null = solo secuencias.</summary>
        public WaveRulesData waveRules;
    }

    /// <summary>Doc 05 v2.0 §9.2: prioridad del reparto, dónde entra el miniboss y su separación.</summary>
    [Serializable]
    public class WaveRulesData
    {
        /// <summary>Prioridad del reparto por pasadas, en códigos del balance: "D E V T A C".</summary>
        public string order;
        /// <summary>Fracción de las unidades normales tras la que entra el miniboss (0,75), redondeando hacia arriba.</summary>
        public float minibossAfter;
        /// <summary>Segundos de separación antes y después del miniboss (3).</summary>
        public float minibossGap;
    }

    [Serializable]
    public class EconomyData
    {
        public int startGold;
        /// <summary>Oro inicial de la partida con tutorial (Doc 05 v2.0 §5.1: 100). 0 = startGold.</summary>
        public int tutorialStartGold;
        public int baseHp;
        public float upgradeCostFactor;
        public float sellRefundFactor;
    }

    [Serializable]
    public class TimingData
    {
        public float waveGap;
        public float countdownStep;
        public float defenseDuration;
        /// <summary>Cada cuánto aplica daño la quemadura (GDS-002.4). 0 = 0,25 s.</summary>
        public float burnTick;
    }

    [Serializable]
    public class StarsData
    {
        public int threeStarsMinHp;
        public int twoStarsMinHp;
    }

    /// <summary>Formas de ataque (GDS-002.4). Cada una es un algoritmo del núcleo; los números son del balance.</summary>
    public static class AttackKind
    {
        public const string Single = "single";     // proyectil dirigido, un objetivo (Arqueras, Cañón)
        public const string Area = "area";         // proyectil dirigido, explota donde termina el objetivo (Mago, Bombardera)
        public const string Mortar = "mortar";     // tiro parabólico a un punto fijo, distancia mínima (Mortero)
        public const string Chain = "chain";       // descarga instantánea que rebota (Eléctrica)
        public const string Inferno = "inferno";   // rayo que fija el objetivo y calienta por etapas (Infernal)
        public const string Gold = "gold";         // no ataca: produce oro que se recoge con clic (Oro)
        public const string Flame = "flame";       // ráfaga lineal a un punto fijo, quema y deja fuego (Lanzallamas)

        public static bool IsKnown(string k) =>
            k == Single || k == Area || k == Mortar || k == Chain || k == Inferno || k == Gold || k == Flame;
    }

    [Serializable]
    public class TowerTypeData
    {
        public string id;
        public string displayName;
        /// <summary>Nombre corto para la tarjeta del HUD (UXS-002.4). Vacío = displayName.</summary>
        public string shortName;
        /// <summary>Qué hace, en una línea, para la ayuda de la tarjeta (UXS-002.4, ley 5).</summary>
        public string description;
        public int cost;
        public bool targetsGround;
        public bool targetsAir;
        public float metalEfficiency;
        /// <summary>Ver AttackKind.</summary>
        public string attack;
        public float projectileSpeed;
        public float footprintRadius;
        /// <summary>Niveles funcionales (N1, N2). El N3 no existe en la simulación (CD-010).</summary>
        public TowerLevelData[] levels;

        public string Short => string.IsNullOrEmpty(shortName) ? displayName : shortName;
        public bool Attacks => attack != AttackKind.Gold;
    }

    [Serializable]
    public class TowerLevelData
    {
        public float damage;
        public float interval;
        public float range;
        public float areaRadius;
        // Mortero
        public float minRange;
        public float flightTime;
        // Eléctrica
        public int chainJumps;
        public float chainRadius;
        // Infernal: daño por segundo de cada etapa y segundos fijado para pasar de etapa
        public float[] rampDps;
        public float rampStep;
        /// <summary>Infernal (Doc 05 v2.0 §6.5): segundos fijado en que empieza cada etapa, [0, 2, 5]. Vacío = etapas de rampStep.</summary>
        public float[] rampTimes;
        // Oro
        public float goldPerSecond;
        public float goldCapacity;
        /// <summary>Oro (Doc 05 v2.0 §6.6): oro que suma cada ciclo completo. Con goldCycle > 0 reemplaza a goldPerSecond.</summary>
        public float goldPerCycle;
        public float goldCycle;
        // Lanzallamas (y cualquier fuente de quemadura)
        public float burnDps;
        public float burnDuration;
        public float flameWidth;
        /// <summary>Lanzallamas (Doc 05 v2.0 §6.7): segundos entre pulsos y duración de la ráfaga; damage es por pulso. 0 = un solo golpe.</summary>
        public float pulseInterval;
        public float burstDuration;
    }

    [Serializable]
    public class EnemyTypeData
    {
        public string id;
        /// <summary>Letra de las oleadas (D, E, V, A, T, G, B).</summary>
        public string code;
        public string displayName;
        /// <summary>"ground" o "air".</summary>
        public string layer;
        public float hp;
        public float armor;
        public float travelTime;
        public float travelTimeExposed;
        public int baseDamage;
        public int gold;
        /// <summary>Pesado (Doc 04 §13). Sin efecto en el Mundo 1: lo lee la Torre de choque del Mundo 2.</summary>
        public bool heavy;
        /// <summary>Miniboss (Doc 04 §15): barra propia y anuncio al aparecer (UXS-002.5).</summary>
        public bool miniboss;
        /// <summary>Texto del aviso de debut (UXS-001.4).</summary>
        public string counterHint;
    }

    [Serializable]
    public class WaveData
    {
        /// <summary>Secuencia como la escribe el owner: "D D E / (D E V A E V)x4 / A D D E A". "." es un lugar vacío (LDS-002.6).</summary>
        public string sequence;
        public float spawnInterval;
        /// <summary>A qué recorrido va cada enemigo, en ciclo: "0 x3 1 x3" = grupos 3/3 (Doc 03 §9). Vacío = todos al 0.</summary>
        public string lanes;
        /// <summary>Composición (Doc 05 v2.0 §10): "D10 E6 V4". Si está, reemplaza a sequence y lanes: el orden y las entradas salen de waveRules.</summary>
        public string composition;
        /// <summary>Código del miniboss de la oleada (Doc 05 v2.0 §9.2). Vacío = sin miniboss.</summary>
        public string miniboss;

        public bool IsComposition => !string.IsNullOrEmpty(composition);
    }

    [Serializable]
    public class RouteData
    {
        public Vec2[] points;
    }

    [Serializable]
    public class LevelData
    {
        public string id;
        public string displayName;
        /// <summary>Recorrido único del P0. Los niveles del Mundo 1 usan routes.</summary>
        public Vec2[] path;
        /// <summary>Recorridos completos, entrada → base. Comparten prefijo en una bifurcación y terminan en la base (GDS-002.6).</summary>
        public RouteData[] routes;
        public float pathWidth;
        public RectData buildArea;
        public CircleData[] blocked;
        public float baseRadius;
        /// <summary>Torre que el tutorial obliga a construir (Doc 05 §5.1). Vacío = el nivel no tiene tutorial.</summary>
        public string tutorialTowerId;
        /// <summary>Zona sugerida en el paso 5 del tutorial (LDS-001.1): donde el camino pasa dos veces por el alcance.</summary>
        public CircleData tutorialHint;
        /// <summary>Oleadas del nivel (LDS-002.6). Vacío = las del balance (P0).</summary>
        public WaveData[] waves;
        /// <summary>Paleta del continente (UXS-002.2): "praderas", "desfiladero". Vacío = gris del P0.</summary>
        public string theme;
    }

    [Serializable]
    public class RectData
    {
        public float minX, minZ, maxX, maxZ;
        public bool Contains(Vec2 p) => p.x >= minX && p.x <= maxX && p.z >= minZ && p.z <= maxZ;
    }

    [Serializable]
    public class CircleData
    {
        public float x, z, radius;
        public Vec2 Center => new Vec2(x, z);
    }

    /// <summary>Modificador permanente de una estadística (tienda y mejora de tanda, GDS-002.3).</summary>
    [Serializable]
    public class StatMod
    {
        /// <summary>damage · interval · range · areaRadius · minRange · chainJumps · chainRadius · rampDps · goldPerSecond · goldCapacity · burnDps · burnDuration · flameWidth
        /// · flightTime · rampTimes · goldCycle · goldPerCycle · projectileSpeed (del tipo, vale para N1 y N2)</summary>
        public string stat;
        /// <summary>"mul", "add" o "pct". "pct" suma porcentajes (dos insignias de +8 % = +16 %) y redondea como el Doc 05 v2.0 §7.1:
        /// el daño al entero más cercano, el resto a dos decimales.</summary>
        public string op;
        public float value;

        public static readonly string[] Stats =
            { "damage", "interval", "range", "areaRadius", "minRange", "chainJumps", "chainRadius", "rampDps", "goldPerSecond", "goldCapacity", "burnDps", "burnDuration", "flameWidth",
              "flightTime", "rampTimes", "goldCycle", "goldPerCycle", "projectileSpeed" };
    }
}
