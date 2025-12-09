using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{
    public static readonly List<Tower> Instances = new();

    [Header("Type Object")]
    public TowerData data;
    public TowerId towerType;

    [Header("Targeting")]
    public float range = 6f;
    public float fireRate = 0.6f;

    [Header("Shoot point")]
    public Transform front;

    [Header("Projectile")]
    public ProjectileFactoryTD projectileFactory;
    public ProjectilePoolManager projectilePool;
    public ProjectileId projectileType = ProjectileId.Arrow;

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
        projectileType = d.projectileId;    // ← usa projectileId del Type Object
    }

    void Awake()
    {
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

    void Shoot(Transform target)
    {
        if (projectileFactory == null || projectilePool == null)
        {
            Debug.LogError("[Tower] Falta projectileFactory o projectilePool.");
            return;
        }

        // 1) obtengo el Type Object del proyectil
        var projData = projectileFactory.GetData(projectileType);
        if (projData == null)
        {
            Debug.LogError($"[Tower] No ProjectileData para {projectileType}");
            return;
        }

        // 2) de ahí saco el prefab
        var prefab = projData.prefab;
        if (prefab == null)
        {
            Debug.LogError("[Tower] Prefab nulo en ProjectileData.");
            return;
        }

        Vector3 muzzlePos = front != null ? front.position : transform.position;
        Vector3 dir = (target.position - muzzlePos).normalized;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        // 3) pido una instancia al pool
        var proj = projectilePool.Get(prefab, muzzlePos);

        // 4) le aplico los datos del Type Object
        proj.ApplyData(projData);

        // 5) oriento y disparo
        proj.transform.rotation = rot;
        proj.FireAt(target, projectilePool.Release);
    }
}
