/// <summary>Un proyectil al objetivo primario.</summary>
public class SingleShot : IShootStrategy
{
    public void Shoot(ShootContext ctx)
    {
        if (ctx == null || ctx.primaryTarget == null) return;
        ctx.FireProjectileAt(ctx.primaryTarget);
    }
}
