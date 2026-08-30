using UnityEngine;

/// <summary>
/// Rayo sin proyectil: daño directo que rampea mientras se mantiene el mismo objetivo.
/// Si data.multiTargetCount > 1, pega a varios objetivos con daño plano (sin rampa).
/// </summary>
public class BeamShot : IShootStrategy
{
    EnemyTD lockedTarget;
    float lockTime;

    public EnemyTD LockedTarget => lockedTarget;
    public float LockTime => lockTime;

    /// <summary>Reinicia la rampa (llamar cuando la torre pierde el objetivo).</summary>
    public void ResetLock()
    {
        lockedTarget = null;
        lockTime = 0f;
    }

    public void Shoot(ShootContext ctx)
    {
        if (ctx == null) return;

        int multi = ctx.data != null ? ctx.data.multiTargetCount : 1;

        // --- Modo multi: daño plano a varios objetivos ---
        if (multi > 1)
        {
            ResetLock();
            int hits = 0;
            int flat = Mathf.Max(1, ctx.damage);

            if (ctx.primaryTarget != null)
            {
                ctx.primaryTarget.TakeDamage(flat);
                CombatEvents.RaiseBeamTick(ctx.tower, ctx.primaryTarget, 0f);
                hits++;
            }

            if (ctx.targetsInRange != null)
            {
                for (int i = 0; i < ctx.targetsInRange.Count && hits < multi; i++)
                {
                    var t = ctx.targetsInRange[i];
                    if (t == null || t == ctx.primaryTarget) continue;
                    t.TakeDamage(flat);
                    CombatEvents.RaiseBeamTick(ctx.tower, t, 0f);
                    hits++;
                }
            }
            return;
        }

        // --- Modo rampa: un solo objetivo ---
        var target = ctx.primaryTarget;
        if (target == null)
        {
            ResetLock();
            return;
        }

        if (target != lockedTarget)
        {
            lockedTarget = target;
            lockTime = 0f;
        }
        else
        {
            lockTime += Mathf.Max(0f, ctx.deltaTime);
        }

        float ramp = ctx.data != null ? ctx.data.beamRampSeconds : 5f;
        float maxMul = ctx.data != null ? ctx.data.beamMaxMultiplier : 3f;
        float t01 = ramp > 0f ? Mathf.Clamp01(lockTime / ramp) : 1f;

        int dmg = Mathf.RoundToInt(ctx.damage * Mathf.Lerp(1f, maxMul, t01));
        target.TakeDamage(Mathf.Max(1, dmg));
        CombatEvents.RaiseBeamTick(ctx.tower, target, t01);
    }
}
