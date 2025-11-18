using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{

    public static readonly List<Tower> Instances = new();

    public TowerId towerType;   


    [Header("Targeting")]
    public float range = 6f;
    public float fireRate = 0.6f;

    [Header("Shoot point")]
    public Transform front;

    [Header("Projectile")]
    public ProjectileFactoryTD projectileFactory;
    public ProjectilePoolManager projectilePool;
    public ProjectileId projectileType = ProjectileId.Basic;

    float nextShootTime;

    void OnEnable()
    {
        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    void OnDisable()
    {
        Instances.Remove(this);
    }


    void Awake()
    {
        // Si no están seteados por inspector, los busco en la escena.
        if (!projectileFactory)
            projectileFactory = FindObjectOfType<ProjectileFactoryTD>();

        if (!projectilePool)
            projectilePool = FindObjectOfType<ProjectilePoolManager>();
    }

    void Update()
    {
        if (EnemyPriorityABB.Instance == null) return;

        var targetEnemy = EnemyPriorityABB.Instance.GetMostAdvancedInRange(transform.position, range);
        if (targetEnemy == null) return;

        var target = targetEnemy.transform;

        if (Time.time >= nextShootTime)
        {
            Shoot(target);
            nextShootTime = Time.time + fireRate;
        }
    }


    Transform GetTarget()
    {
        // 1) Intentar ABB
        var abb = EnemyPriorityABB.Instance;
        if (abb != null)
        {
            var best = abb.GetMostAdvancedInRange(transform.position, range);
            if (best != null) return best.transform;
        }

        // 2) Fallback: lógica vieja (distancia)
        EnemyTD bestEnemy = null;
        float bestD = float.MaxValue;
        var enemies = FindObjectsOfType<EnemyTD>();
        foreach (var e in enemies)
        {
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d <= range && d < bestD)
            {
                bestD = d; bestEnemy = e;
            }
        }
        return bestEnemy ? bestEnemy.transform : null;
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

        var proj = projectilePool.Get(prefab, muzzlePos);
        proj.transform.rotation = rot;
        proj.FireAt(target, projectilePool.Release);
    }
}
