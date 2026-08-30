using System.Collections;
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
    // fireRate = SEGUNDOS ENTRE DISPAROS
    public float fireRate = 0.6f;
    [HideInInspector] public int currentDamage = 0;

    [Header("Shoot point")]
    public Transform front;

    [Header("Projectile")]
    public ProjectileFactoryTD projectileFactory;
    public ProjectilePoolManager projectilePool;
    public ProjectileId projectileType = ProjectileId.Arrow;

    float nextShootTime;
    float lastShootTime = -1f;

    IShootStrategy strategy;
    ShootContext ctx;
    EnemyTD currentTarget;
    Coroutine burstRoutine;

    // Buffer reutilizable de objetivos (lo llena el ABB, más avanzado primero)
    readonly List<EnemyTD> targetBuffer = new List<EnemyTD>(16);

    // === API pública nueva ===
    public IShootStrategy Strategy
    {
        get
        {
            if (strategy == null) strategy = ShootStrategyFactory.Create(data);
            return strategy;
        }
    }

    public EnemyTD CurrentTarget => currentTarget;

    /// <summary>DPS estimado con los stats actuales (upgrades incluidos).</summary>
    public float CurrentDps
    {
        get
        {
            if (fireRate <= 0f) return 0f;
            float dps = currentDamage / fireRate;
            if (data != null)
            {
                if (data.attackType == AttackType.Burst)       dps *= Mathf.Max(1, data.burstCount);
                if (data.attackType == AttackType.MultiTarget) dps *= Mathf.Max(1, data.multiTargetCount);
            }
            return dps;
        }
    }

    void OnEnable()
    {
        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    void OnDisable()
    {
        Instances.Remove(this);

        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
            burstRoutine = null;
        }
    }

    public void ApplyData(TowerData d)
    {
        data = d;
        strategy = ShootStrategyFactory.Create(d);   // null → SingleShot
        if (d == null) return;

        towerType      = d.id;
        currentDamage  = d.damage;
        range          = d.range;
        fireRate       = d.fireRate;
        projectileType = d.projectileId;    // usa projectileId del Type Object
    }

    void Awake()
    {
        if (data != null)
            ApplyData(data);

        if (!projectileFactory)
            projectileFactory = FindFirstObjectByType<ProjectileFactoryTD>();

        if (!projectilePool)
            projectilePool = FindFirstObjectByType<ProjectilePoolManager>();
    }

    void Update()
    {
        var abb = EnemyPriorityABB.Instance;
        if (abb == null) return;

        // 1) Adquisición de objetivos
        targetBuffer.Clear();
        EnemyTD primary = null;

        if (data != null)
        {
            abb.GetTargetsInRange(transform.position, range, data.minRange, data.targets, MaxTargetsNeeded(), targetBuffer);
            if (targetBuffer.Count > 0) primary = targetBuffer[0];
        }
        else
        {
            primary = abb.GetMostAdvancedInRange(transform.position, range);
            if (primary != null) targetBuffer.Add(primary);
        }

        currentTarget = primary;

        if (primary == null)
        {
            var beam = strategy as BeamShot;
            if (beam != null) beam.ResetLock();
            return;
        }

        // 2) Cadencia: fireRate = segundos entre disparos (o ticks del rayo)
        if (Time.time >= nextShootTime)
        {
            Fire(primary);
            float interval = fireRate > 0f ? fireRate : 0.1f;
            nextShootTime = Time.time + interval;
        }
    }

    int MaxTargetsNeeded()
    {
        if (data == null) return 1;

        switch (data.attackType)
        {
            case AttackType.MultiTarget: return Mathf.Max(1, data.multiTargetCount);
            case AttackType.Beam:        return Mathf.Max(1, data.multiTargetCount);
            case AttackType.Chain:       return Mathf.Max(0, data.chainMaxJumps) + 1;
            case AttackType.Push:        return 0;   // 0 = sin límite
            default:                     return 1;
        }
    }

    void Fire(EnemyTD primary)
    {
        var s = Strategy;
        BuildContext(primary);

        // Ráfaga espaciada en el tiempo con corrutina
        if (data != null && data.attackType == AttackType.Burst && data.burstCount > 1)
        {
            if (burstRoutine != null) StopCoroutine(burstRoutine);
            burstRoutine = StartCoroutine(BurstRoutine(primary, data.burstCount, data.burstInterval));
        }
        else
        {
            s.Shoot(ctx);
        }

        if (data == null || data.attackType != AttackType.Beam)
            CombatEvents.RaiseTowerFired(this, primary);

        lastShootTime = Time.time;
    }

    IEnumerator BurstRoutine(EnemyTD target, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            if (!isActiveAndEnabled) yield break;
            if (target == null) yield break;

            ctx.primaryTarget = target;
            BurstShot.FireOne(ctx, target);

            if (i < count - 1 && interval > 0f)
                yield return new WaitForSeconds(interval);
        }
        burstRoutine = null;
    }

    void BuildContext(EnemyTD primary)
    {
        if (ctx == null)
        {
            ctx = new ShootContext();
            ctx.tower = this;
            ctx.getProjectile = GetProjectile;
            ctx.releaseProjectile = ReleaseProjectile;
            ctx.targetsInRange = targetBuffer;
        }

        ctx.data          = data;
        ctx.muzzle        = front != null ? front : transform;
        ctx.primaryTarget = primary;
        ctx.targetsInRange = targetBuffer;
        ctx.damage        = currentDamage;
        ctx.splashRadius  = data != null ? data.splashRadius : 0f;
        ctx.deltaTime     = lastShootTime < 0f ? 0f : Time.time - lastShootTime;
    }

    // === Camino del proyectil (igual que antes: factory → prefab → pool → ApplyData → daño) ===
    Projectile GetProjectile(Vector3 position)
    {
        if (projectileFactory == null || projectilePool == null)
            return null;

        // 1) Type Object del proyectil
        var projData = projectileFactory.GetData(projectileType);
        if (projData == null)
            return null;

        // 2) prefab
        var prefab = projData.prefab;
        if (prefab == null)
            return null;

        // 3) instancia del pool
        var proj = projectilePool.Get(prefab, position);

        // 4) datos del Type Object + override de daño
        proj.ApplyData(projData);
        if (currentDamage > 0) proj.damage = currentDamage;

        return proj;
    }

    void ReleaseProjectile(Projectile proj)
    {
        if (proj == null) return;

        if (projectilePool != null)
            projectilePool.Release(proj);
        else
            Destroy(proj.gameObject);
    }
}
