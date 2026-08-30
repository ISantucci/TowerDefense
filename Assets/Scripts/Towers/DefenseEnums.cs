// Assets/Scripts/Towers/DefenseEnums.cs
// Enums compartidos del catálogo de defensas (Supercell + originales).

/// <summary>De qué juego / modo proviene la defensa.</summary>
public enum DefenseSource
{
    Original,
    CoC_Home,
    CoC_Builder,
    CoC_Capital,
    CR,
    BoomBeach
}

/// <summary>Categoría general de la entrada del catálogo.</summary>
public enum DefenseKind
{
    Defense,
    Trap,
    HallWeapon,
    TowerTroop,
    Building
}

/// <summary>Capas que puede atacar una torre (flags).</summary>
[System.Flags]
public enum TargetLayer
{
    None   = 0,
    Ground = 1,
    Air    = 2,
    Both   = Ground | Air
}

/// <summary>Comportamiento de ataque; se mapea a una IShootStrategy en ShootStrategyFactory.</summary>
public enum AttackType
{
    SingleTarget,
    Splash,
    MultiTarget,
    Burst,
    Beam,
    Chain,
    Push,
    Pull,
    Spawner,
    Support,
    Trap
}
