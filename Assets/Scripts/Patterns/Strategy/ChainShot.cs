using UnityEngine;

/// <summary>
/// Cadena: daño directo al primario y luego salta a hasta data.chainMaxJumps
/// objetivos más, multiplicando el daño por data.chainFalloff en cada salto.
/// </summary>
public class ChainShot : IShootStrategy
{
    public void Shoot(ShootContext ctx)
    {
        if (ctx == null || ctx.primaryTarget == null) return;

        int maxJumps = ctx.data != null ? ctx.data.chainMaxJumps : 3;
        float falloff = ctx.data != null ? ctx.data.chainFalloff : 0.5f;

        float current = Mathf.Max(1, ctx.damage);
        Vector3 from = ctx.MuzzlePosition;
        Vector3 to = ctx.primaryTarget.transform.position;
        CombatEvents.RaiseChainJump(from, to);
        ctx.primaryTarget.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(current)));

        if (ctx.targetsInRange == null || maxJumps <= 0) return;

        int jumps = 0;
        Vector3 last = to;
        for (int i = 0; i < ctx.targetsInRange.Count && jumps < maxJumps; i++)
        {
            var t = ctx.targetsInRange[i];
            if (t == null || t == ctx.primaryTarget) continue;

            current *= falloff;
            Vector3 next = t.transform.position;
            CombatEvents.RaiseChainJump(last, next);
            last = next;
            t.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(current)));
            jumps++;
        }
    }
}
