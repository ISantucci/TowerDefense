using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Targeting")]
    public float range = 6f;
    public float fireRate = 0.6f;

    [Header("Shoot point")]
    public Transform front;                 // hijo "Front" (muzzle)

    [Header("Projectile")]
    public ProjectileFactoryTD projectileFactory;   // tu factory con GetPrefab(...)
    public ProjectilePoolManager projectilePool;    // tu pool (UnityEngine.Pool)
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
        if (projectileFactory == null || projectilePool == null)
        {
            Debug.LogError("[Tower] Falta projectileFactory o projectilePool.");
            return;
        }

        var prefab = projectileFactory.GetPrefab(projectileType);
        if (prefab == null) { Debug.LogError("[Tower] Prefab nulo en factory."); return; }

        Vector3 muzzlePos = front != null ? front.position : transform.position;
        Vector3 dir = (target.position - muzzlePos).normalized;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        // Get del pool y disparo con tu API
        var proj = projectilePool.Get(prefab, muzzlePos);
        proj.transform.rotation = rot;
        proj.FireAt(target, projectilePool.Release);   // <- clave: Release del pool
    }

    Transform FindNearestEnemyInRange()
    {
        EnemyTD best = null;
        float bestD = float.MaxValue;

        var enemies = FindObjectsOfType<EnemyTD>();
        foreach (var e in enemies)
        {
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d <= range && d < bestD)
            {
                bestD = d; best = e;
            }
        }
        return best ? best.transform : null;
    }
}
