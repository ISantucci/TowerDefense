using System;

namespace ClashDefense.Core
{
    // Formas de datos del balance (RQ-001.8) y del nivel (LDS-001.1).
    // Campos públicos y [Serializable]: los lee JsonUtility en Unity y System.Text.Json en la herramienta de simulación.
    // Ningún número de balance vive en la lógica: todo sale de acá.

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
        public WaveData[] waves;
    }

    [Serializable]
    public class EconomyData
    {
        public int startGold;
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
    }

    [Serializable]
    public class StarsData
    {
        public int threeStarsMinHp;
        public int twoStarsMinHp;
    }

    [Serializable]
    public class TowerTypeData
    {
        public string id;
        public string displayName;
        public int cost;
        public bool targetsGround;
        public bool targetsAir;
        public float metalEfficiency;
        /// <summary>"single" o "area".</summary>
        public string attack;
        public float projectileSpeed;
        public float footprintRadius;
        /// <summary>Niveles funcionales (N1, N2). El N3 no existe en la simulación.</summary>
        public TowerLevelData[] levels;
    }

    [Serializable]
    public class TowerLevelData
    {
        public int damage;
        public float interval;
        public float range;
        public float areaRadius;
    }

    [Serializable]
    public class EnemyTypeData
    {
        public string id;
        /// <summary>Letra de las oleadas del Doc 05 (D, E, V, A).</summary>
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
        /// <summary>Texto del aviso de debut (UXS-001.4).</summary>
        public string counterHint;
    }

    [Serializable]
    public class WaveData
    {
        /// <summary>Secuencia como la escribe el owner: "D D E / (D E V A E V)x4 / A D D E A".</summary>
        public string sequence;
        public float spawnInterval;
    }

    [Serializable]
    public class LevelData
    {
        public string id;
        public string displayName;
        public Vec2[] path;
        public float pathWidth;
        public RectData buildArea;
        public CircleData[] blocked;
        public float baseRadius;
        /// <summary>Torre que el tutorial obliga a construir (Doc 05 §5.1). Vacío = el nivel no tiene tutorial.</summary>
        public string tutorialTowerId;
        /// <summary>Zona sugerida en el paso 5 del tutorial (LDS-001.1): donde el camino pasa dos veces por el alcance.</summary>
        public CircleData tutorialHint;
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
}
