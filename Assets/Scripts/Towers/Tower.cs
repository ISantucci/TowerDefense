using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{
    public static readonly List<Tower> Instances = new();

    [Header("Type Object")]
    public TowerData data;       // 👈 Type Object
    public TowerId towerType;    // Id lógico (Basic, etc.)

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

    public void ApplyData(TowerData d)
    {
        data = d;
        if (d == null) return;

        towerType = d.id;
        range = d.range;
        fireRate = d.fireRate;
        projectileType = d.projectileType;
    }

    void Awake()
    {
        // Si el prefab ya viene con un TowerData asignado, aplico sus valores
        if (data != null)
            ApplyData(data);

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
        var abb = EnemyPriorityABB.Instance;
        if (abb != null)
        {
            var best = abb.GetMostAdvancedInRange(transform.position, range);
            if (best != null) return best.transform;
        }

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
        if (prefab == null)
        {
            Debug.LogError("[Tower] Prefab nulo en factory.");
            return;
        }

        Vector3 muzzlePos = front != null ? front.position : transform.position;
        Vector3 dir = (target.position - muzzlePos).normalized;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        var proj = projectilePool.Get(prefab, muzzlePos);
        proj.transform.rotation = rot;
        proj.FireAt(target, projectilePool.Release);
    }
}
