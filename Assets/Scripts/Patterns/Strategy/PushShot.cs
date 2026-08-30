/// <summary>Empuje sin daño: retrocede a todos los objetivos en rango data.pushDistance por su ruta.</summary>
public class PushShot : IShootStrategy
{
    public void Shoot(ShootContext ctx)
    {
        if (ctx == null) return;

        float distance = ctx.data != null ? ctx.data.pushDistance : 0f;
        if (distance <= 0f) return;

        if (ctx.targetsInRange != null && ctx.targetsInRange.Count > 0)
        {
            for (int i = 0; i < ctx.targetsInRange.Count; i++)
                Push(ctx.tower, ctx.targetsInRange[i], distance);
        }
        else if (ctx.primaryTarget != null)
        {
            Push(ctx.tower, ctx.primaryTarget, distance);
        }
    }

    static void Push(Tower tower, EnemyTD enemy, float distance)
    {
        if (enemy == null) return;
        var mv = enemy.GetComponent<EnemyMovement>();
        if (mv != null)
        {
            mv.Knockback(distance);
            CombatEvents.RaisePushHit(tower, enemy);
        }
    }
}
