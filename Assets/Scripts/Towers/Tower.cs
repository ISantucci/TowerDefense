// Tower.cs (fragmento clave)
using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Shoot points")]
    public Transform front;                 // <-- Asigná el child "Front"
    public float range = 6f;
    public float fireRate = 0.6f;

    [Header("Proj/Factory")]
    public ProjectileFactoryTD projectileFactory;
    public ProjectileId projectileType = ProjectileId.Basic;

    float nextShootTime;

    void Update()
    {
        var target = FindNearestEnemyInRange();
        if (target == null) return;

        if (Time.time >= nextShootTime)
        {
            Shoot(target);
            nextShootTime = Time.time + fireRate;
        }
    }

    void Shoot(Transform target)
    {
        // Punto y dirección
        Vector3 muzzlePos = (front != null) ? front.position : transform.position;
        Vector3 dir = (target.position - muzzlePos).normalized;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        var proj = projectileFactory.Create(projectileType, muzzlePos, rot);
        if (proj != null) proj.Launch(dir);   // tu proyectil debe tener un método Launch(dir/vel)
    }

    Transform FindNearestEnemyInRange()
    {
        // Tu implementación actual (tag/layer). Asegurate que Enemy tenga Tag/Layer correcto.
        // …
        return null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Rango
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);

        // Muzzle dir
        if (front != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(front.position, front.position + front.forward * 1.0f);
        }
    }
#endif
}
