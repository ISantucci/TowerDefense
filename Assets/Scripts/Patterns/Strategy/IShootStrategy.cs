using System;
using UnityEngine;

public interface IShootStrategy
{
    void Shoot(Transform muzzle, Transform target,
               Func<Vector3, Projectile> getProjectile,
               Action<Projectile> releaseProjectile);
}
