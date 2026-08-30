/// <summary>Un proyectil al primario, asegurando el radio de splash del TowerData.</summary>
public class SplashShot : IShootStrategy
{
    public void Shoot(ShootContext ctx)
    {
        if (ctx == null || ctx.primaryTarget == null) return;

        if (ctx.data != null && ctx.data.splashRadius > 0f && ctx.splashRadius <= 0f)
            ctx.splashRadius = ctx.data.splashRadius;

        ctx.FireProjectileAt(ctx.primaryTarget);
    }
}
