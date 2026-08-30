// IMPORTANTE: se serializa por número. Goblin y FastGoblin quedan primero; lo nuevo va SIEMPRE al final.
public enum EnemyId
{
    Goblin = 0,
    FastGoblin = 1,

    // Tierra (Clash of Clans / Boom Beach)
    Barbarian,
    Archer,
    Giant,
    WallBreaker,
    HogRider,
    Pekka,
    Golem,
    Rifleman,
    Heavy,
    Scorcher,

    // Aire
    Minion,
    Balloon,
    BabyDragon,
    Dragon,
    LavaHound,
    Healer
}

/// <summary>Nombres para HUD (en español, cortos) y color de identidad por enemigo.</summary>
public static class EnemyNames
{
    public static string Of(EnemyId id)
    {
        switch (id)
        {
            case EnemyId.Goblin: return "Goblin";
            case EnemyId.FastGoblin: return "Goblin veloz";
            case EnemyId.Barbarian: return "Bárbaro";
            case EnemyId.Archer: return "Arquera";
            case EnemyId.Giant: return "Gigante";
            case EnemyId.WallBreaker: return "Rompemuros";
            case EnemyId.HogRider: return "Montapuercos";
            case EnemyId.Pekka: return "P.E.K.K.A";
            case EnemyId.Golem: return "Gólem";
            case EnemyId.Rifleman: return "Fusilero";
            case EnemyId.Heavy: return "Pesado";
            case EnemyId.Scorcher: return "Calcinador";
            case EnemyId.Minion: return "Esbirro";
            case EnemyId.Balloon: return "Globo";
            case EnemyId.BabyDragon: return "Dragón bebé";
            case EnemyId.Dragon: return "Dragón";
            case EnemyId.LavaHound: return "Sabueso de lava";
            case EnemyId.Healer: return "Sanadora";
        }
        return id.ToString();
    }
}
