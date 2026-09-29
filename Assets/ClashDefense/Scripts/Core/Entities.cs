using System.Collections.Generic;

namespace ClashDefense.Core
{
    public enum Layer { Ground, Air }

    /// <summary>Enemigo en la partida (GDS-001.1, GDS-001.4, GDS-002.5).</summary>
    public sealed class Enemy
    {
        public int Id;               // orden de aparición: desempate de objetivo (S1)
        public int Wave;             // oleada a la que pertenece (1..N)
        public int Route;            // recorrido asignado (GDS-002.6)
        public EnemyTypeData Type;
        public Layer Layer;
        public float Hp;
        public float Armor;
        public float Distance;       // d: distancia recorrida sobre su recorrido
        public float Remaining;      // distancia que le falta hasta su llegada (prioridad de objetivo)
        public float Speed;          // u/s
        public bool Alive = true;    // falso al morir o llegar
        public bool Arrived;
        public Vec2 Position;
        // quemadura (GDS-002.4): no se acumula, una nueva aplicación reinicia la duración
        public float BurnTime;
        public float BurnDps;
        public float BurnTick;
        public int BurnTowerId;
        public TowerTypeData BurnSource;

        public bool Armored => Armor > 0f;
        public bool Burning => BurnTime > 0f;
    }

    /// <summary>Torre en la partida (GDS-001.2, GDS-001.3, GDS-002.4).</summary>
    public sealed class Tower
    {
        public int Id;
        public TowerTypeData Type;
        public int Level = 1;        // 1 o 2
        public Vec2 Position;
        public float Cooldown;       // <= 0: lista para atacar
        public int Invested;         // costo + mejoras pagadas
        public bool Sold;
        /// <summary>Sin uso desde GDS-004.2: el descubrimiento de inmunidad es por tipo de torre y por nivel (Doc 05 v2.0 §8.2),
        /// y lo guarda la partida (Match.ImmuneKnown). Se conserva para no romper a quien lo lea.</summary>
        public readonly HashSet<int> KnownImmune = new HashSet<int>();
        // Infernal: objetivo fijado y tiempo que lleva fijado
        public int LockedId;
        public float LockTime;
        public bool LockImmuneShown;
        // Oro: lo guardado y si llegó al tope
        public float Stored;
        public bool Full;
        /// <summary>Oro por ciclo (Doc 05 v2.0 §6.6): segundos del ciclo en curso.</summary>
        public float CycleTime;
        // Lanzallamas por pulsos (Doc 05 v2.0 §6.7): pulsos que faltan de la ráfaga, espera al siguiente y la línea fijada
        public int PulsesLeft;
        public float PulseTimer;
        public Vec2 BurstA, BurstB;

        public TowerLevelData Stats => Type.levels[Level - 1];
        public bool CanUpgrade => Level < Type.levels.Length;

        /// <summary>Etapa actual del Infernal (0..n-1).</summary>
        public int InfernoStage
        {
            get
            {
                var s = Stats;
                if (s.rampDps == null || s.rampDps.Length == 0) return 0;
                if (s.rampTimes != null && s.rampTimes.Length == s.rampDps.Length)
                {
                    // umbrales explícitos (Doc 05 v2.0 §6.5): la etapa i empieza a los rampTimes[i] segundos fijado
                    int k = 0;
                    for (int i = 1; i < s.rampTimes.Length; i++) if (LockTime >= s.rampTimes[i] - 1e-4f) k = i;
                    return k;
                }
                if (s.rampStep <= 0f) return 0;
                int st = (int)(LockTime / s.rampStep + 1e-4f);
                return st < 0 ? 0 : st >= s.rampDps.Length ? s.rampDps.Length - 1 : st;
            }
        }
        public bool InfernoMax => Stats.rampDps != null && Stats.rampDps.Length > 0 && InfernoStage == Stats.rampDps.Length - 1;
    }

    public enum ProjectileKind { Homing, Lob }

    /// <summary>Proyectil (GDS-001.2 S3: dirigido; GDS-002.4: parabólico del Mortero, a un punto fijo).</summary>
    public sealed class Projectile
    {
        public int Id;
        public int TowerId;
        public TowerTypeData TowerType;
        public ProjectileKind Kind;
        public int TargetId;
        public Vec2 Start;
        public Vec2 Position;
        public Vec2 LastTargetPosition;
        public float Speed;
        public float Damage;
        public float AreaRadius;     // 0 = un solo objetivo
        public float FlightTime;     // Lob: segundos de vuelo
        public float Elapsed;
        public bool JustFired;       // Lob: el tick del disparo no cuenta como vuelo
        public bool Done;

        /// <summary>0..1 del vuelo (Lob). La altura la dibuja la presentación.</summary>
        public float Progress => Kind == ProjectileKind.Lob && FlightTime > 0f ? (Elapsed / FlightTime > 1f ? 1f : Elapsed / FlightTime) : 0f;
    }

    /// <summary>Fuego que queda en el piso después de una ráfaga del Lanzallamas (Doc 04 §6.6).</summary>
    public sealed class FireZone
    {
        public int Id;
        public int TowerId;
        public TowerTypeData TowerType;
        public Vec2 A, B;
        public float HalfWidth;
        public float TimeLeft;
        public float Duration;
        public float BurnDps;
    }
}
