using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contexto que la torre construye una vez y reutiliza en cada disparo.
/// Las estrategias no conocen el pool ni la fábrica: sólo getProjectile / releaseProjectile.
/// </summary>
public class ShootContext
{
    public Tower tower;
    public TowerData data;
    public Transform muzzle;
    public EnemyTD primaryTarget;
    public List<EnemyTD> targetsInRange;
    public int damage;
    public float splashRadius;
    public Func<Vector3, Projectile> getProjectile;
    public Action<Projectile> releaseProjectile;
    public float deltaTime;

    /// <summary>Posición de la boca de fuego (o la de la torre si no hay muzzle).</summary>
    public Vector3 MuzzlePosition
    {
        get
        {
            if (muzzle != null) return muzzle.position;
            if (tower != null) return tower.transform.position;
            return Vector3.zero;
        }
    }

    /// <summary>Dispara un proyectil desde el muzzle hacia el objetivo. Devuelve null si no hay proveedor de proyectiles.</summary>
    public Projectile FireProjectileAt(EnemyTD target)
    {
        return FireProjectileAt(target, MuzzlePosition);
    }

    /// <summary>Igual que FireProjectileAt(target) pero con origen explícito (ráfagas con offset).</summary>
    public Projectile FireProjectileAt(EnemyTD target, Vector3 origin)
    {
        if (getProjectile == null || target == null) return null;

        var proj = getProjectile(origin);
        if (proj == null) return null;

        if (damage > 0) proj.damage = damage;
        if (splashRadius > 0f) proj.splashRadius = splashRadius;

        Vector3 dir = target.transform.position - origin;
        if (dir.sqrMagnitude > 0.0001f)
            proj.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        proj.FireAt(target.transform, releaseProjectile);
        return proj;
    }
}
