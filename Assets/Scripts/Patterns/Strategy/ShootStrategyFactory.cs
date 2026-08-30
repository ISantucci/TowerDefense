/// <summary>Mapea AttackType → IShootStrategy. Cada torre recibe su propia instancia (BeamShot tiene estado).</summary>
public static class ShootStrategyFactory
{
    public static IShootStrategy Create(TowerData d)
    {
        if (d == null) return new SingleShot();

        switch (d.attackType)
        {
            case AttackType.Splash:      return new SplashShot();
            case AttackType.MultiTarget: return new MultiShot();
            case AttackType.Burst:       return new BurstShot();
            case AttackType.Beam:        return new BeamShot();
            case AttackType.Chain:       return new ChainShot();
            case AttackType.Push:        return new PushShot();
            case AttackType.Pull:
            case AttackType.Spawner:
            case AttackType.Support:
            case AttackType.Trap:        return new NoShot();
            default:                     return new SingleShot();
        }
    }
}
