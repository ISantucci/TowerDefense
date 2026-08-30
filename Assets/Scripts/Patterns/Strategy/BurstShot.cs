using UnityEngine;

/// <summary>
/// Ráfaga: dispara data.burstCount proyectiles al primario en la misma llamada,
/// con un pequeño offset aleatorio del muzzle (±0.15). La torre puede espaciar
/// la ráfaga en el tiempo llamando a FireOne desde una corrutina.
/// </summary>
public class BurstShot : IShootStrategy
{
    const float Offset = 0.15f;

    public void Shoot(ShootContext ctx)
    {
        if (ctx == null || ctx.primaryTarget == null) return;

        int count = ctx.data != null ? ctx.data.burstCount : 1;
        if (count < 1) count = 1;

        for (int i = 0; i < count; i++)
        {
            if (ctx.primaryTarget == null) break;
            FireOne(ctx, ctx.primaryTarget);
        }
    }

    /// <summary>Un solo proyectil de la ráfaga, con offset aleatorio del muzzle.</summary>
    public static Projectile FireOne(ShootContext ctx, EnemyTD target)
    {
        if (ctx == null || target == null) return null;

        Vector3 origin = ctx.MuzzlePosition + new Vector3(
            Random.Range(-Offset, Offset),
            Random.Range(-Offset, Offset),
            Random.Range(-Offset, Offset));

        return ctx.FireProjectileAt(target, origin);
    }
}
