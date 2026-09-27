using System.Collections.Generic;

namespace ClashDefense.Core
{
    public enum Layer { Ground, Air }

    /// <summary>Enemigo en la partida (GDS-001.1, GDS-001.4).</summary>
    public sealed class Enemy
    {
        public int Id;               // orden de aparición: desempate de objetivo (S1)
        public int Wave;             // oleada a la que pertenece (1..N)
        public EnemyTypeData Type;
        public Layer Layer;
        public float Hp;
        public float Armor;
        public float Distance;       // d: distancia recorrida
        public float Speed;          // u/s
        public bool Alive = true;    // falso al morir o llegar
        public bool Arrived;
        public Vec2 Position;

        public bool Armored => Armor > 0f;
    }

    /// <summary>Torre en la partida (GDS-001.2, GDS-001.3).</summary>
    public sealed class Tower
    {
        public int Id;
        public TowerTypeData Type;
        public int Level = 1;        // 1 o 2
        public Vec2 Position;
        public float Cooldown;       // <= 0: lista para atacar
        public int Invested;         // costo + mejoras pagadas
        public bool Sold;
        /// <summary>Enemigos que ESTA torre ya descubrió inmunes (S2: por torre individual).</summary>
        public readonly HashSet<int> KnownImmune = new HashSet<int>();

        public TowerLevelData Stats => Type.levels[Level - 1];
        public bool CanUpgrade => Level < Type.levels.Length;
    }

    /// <summary>Proyectil dirigido (GDS-001.2, supuesto S3).</summary>
    public sealed class Projectile
    {
        public int Id;
        public int TowerId;
        public TowerTypeData TowerType;
        public int TargetId;
        public Vec2 Position;
        public Vec2 LastTargetPosition;
        public float Speed;
        public int Damage;
        public float AreaRadius;     // 0 = un solo objetivo
        public bool Done;
    }
}
