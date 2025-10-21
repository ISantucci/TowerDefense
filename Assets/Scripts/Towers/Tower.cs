using System;
using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Stats")]
    public float range = 7f;
    public float fireRate = 1.0f; 
    public ProjectileId projectileType = ProjectileId.Basic;

    [Header("Refs")]
    public Transform front;                        
    public ProjectileFactoryTD projectileFactory;
    public ProjectilePoolManager projectilePools;

    IShootStrategy strategy;
    float fireTimer;

    void Awake() { strategy = new SingleShot(); }

    void Update()
    {
        fireTimer += Time.deltaTime;
        if (fireTimer < 1f / fireRate) return;

        var target = FindNearestEnemyInRange();
        if (target == null) return;

        var prefab = projectileFactory.GetPrefab(projectileType);
        if (prefab == null) return;

        fireTimer = 0f;
        strategy.Shoot(
            front,
            target,
            (Vector3 pos) => projectilePools.Get(prefab, pos),
            (Projectile p) => projectilePools.Release(p)
        );
    }

    Transform FindNearestEnemyInRange()
    {
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform best = null; float bestD = float.MaxValue;
        Vector3 p = transform.position;
        foreach (var go in enemies)
        {
            float d = Vector3.Distance(p, go.transform.position);
            if (d <= range && d < bestD) { bestD = d; best = go.transform; }
        }
        return best;
    }

    public void SetStrategy(IShootStrategy s) => strategy = s; 
}
