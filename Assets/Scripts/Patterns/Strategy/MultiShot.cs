using System.Collections.Generic;

/// <summary>Dispara a hasta data.multiTargetCount objetivos distintos (primario primero).</summary>
public class MultiShot : IShootStrategy
{
    readonly List<EnemyTD> fired = new List<EnemyTD>(8);

    public void Shoot(ShootContext ctx)
    {
        if (ctx == null) return;

        int max = ctx.data != null ? ctx.data.multiTargetCount : 1;
        if (max < 1) max = 1;

        fired.Clear();

        if (ctx.primaryTarget != null)
        {
            ctx.FireProjectileAt(ctx.primaryTarget);
            fired.Add(ctx.primaryTarget);
        }

        if (ctx.targetsInRange == null) return;

        for (int i = 0; i < ctx.targetsInRange.Count && fired.Count < max; i++)
        {
            var t = ctx.targetsInRange[i];
            if (t == null || fired.Contains(t)) continue;
            ctx.FireProjectileAt(t);
            fired.Add(t);
        }
    }
}
