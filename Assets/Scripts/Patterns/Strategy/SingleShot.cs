using System;
using UnityEngine;

public class SingleShot : IShootStrategy
{
    public void Shoot(Transform muzzle, Transform target,
                      Func<Vector3, Projectile> getProjectile,
                      Action<Projectile> releaseProjectile)
    {
        var proj = getProjectile(muzzle.position);
        proj.FireAt(target, releaseProjectile);
    }
}
