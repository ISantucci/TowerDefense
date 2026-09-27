namespace ClashDefense.Core
{
    /// <summary>Contrato de eventos del núcleo (GDS-001.0). El núcleo emite; presentación, sonido y métricas escuchan.</summary>
    public enum SimEventType
    {
        StateChanged,       // Text = estado nuevo, Text2 = estado anterior
        Countdown,          // Text = "3" | "2" | "1" | "DEFENSE"
        TutorialStep,       // Int1 = paso (1..7), 0 = terminó
        WaveStarted,        // Int1 = n, Int2 = total
        WaveCleared,        // Int1 = n, Int2 = total
        EnemySpawned,       // EnemyId, Text = tipo, Int1 = oleada
        EnemyDamaged,       // EnemyId, TowerId, Float1 = daño efectivo a la vida, Float2 = daño efectivo a la armadura
        AttackImmune,       // EnemyId, TowerId, Int1 = 1 si es el descubrimiento de esa torre
        ArmorBroken,        // EnemyId, TowerId
        EnemyKilled,        // EnemyId, TowerId, Text = tipo, Int1 = oro
        EnemyReachedBase,   // EnemyId, Text = tipo, Int1 = daño
        BaseDamaged,        // Int1 = vida antes, Int2 = vida después, Text = tipo
        GoldChanged,        // Int1 = delta, Int2 = total, Text = motivo (inicial|baja|construccion|mejora|venta)
        TowerBuilt,         // TowerId, Text = tipo, Int1 = costo, Pos
        TowerUpgraded,      // TowerId, Text = tipo, Int1 = costo, Int2 = nivel nuevo
        TowerSold,          // TowerId, Text = tipo, Int1 = reembolso, Int2 = nivel
        ActionRejected,     // Text = motivo, Text2 = acción
        Shot,               // TowerId, EnemyId, Int1 = proyectil
        ProjectileImpact,   // Int1 = proyectil, TowerId, Pos, Float1 = radio de área
        MatchEnded,         // Text = resultado (victoria|derrota|abandono), Int1 = estrellas, Int2 = vida
    }

    public struct SimEvent
    {
        public SimEventType Type;
        public float Time;          // tiempo activo de partida (sin tutorial ni pausa)
        public int EnemyId;
        public int TowerId;
        public int Int1;
        public int Int2;
        public float Float1;
        public float Float2;
        public string Text;
        public string Text2;
        public Vec2 Pos;

        public override string ToString() =>
            $"{Time:0.000} {Type} e{EnemyId} t{TowerId} {Int1} {Int2} {Float1:0.##} {Float2:0.##} {Text} {Text2} {Pos}";
    }
}
