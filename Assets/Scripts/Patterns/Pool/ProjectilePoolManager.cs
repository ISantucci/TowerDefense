using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ProjectilePoolManager : MonoBehaviour
{
    [System.Serializable]
    public class PoolConfig { public Projectile prefab; public int defaultCapacity = 20; public int maxSize = 200; }

    public PoolConfig[] preload;

    readonly Dictionary<Projectile, ObjectPool<Projectile>> pools = new();

    void Awake()
    {
        if (preload != null)
        {
            foreach (var cfg in preload)
                EnsurePool(cfg.prefab, cfg.defaultCapacity, cfg.maxSize);
        }
    }

    ObjectPool<Projectile> EnsurePool(Projectile prefab, int defCap = 20, int max = 200)
    {
        if (prefab == null) { Debug.LogError("Prefab nulo en EnsurePool"); return null; }
        if (pools.TryGetValue(prefab, out var pool)) return pool;

        pool = new ObjectPool<Projectile>(
            createFunc: () => {
                var obj = Instantiate(prefab, transform);
                obj.SourcePrefab = prefab; 
                return obj;
            },
            actionOnGet: p => { p.gameObject.SetActive(true); },
            actionOnRelease: p => { p.gameObject.SetActive(false); p.transform.localPosition = Vector3.zero; },
            actionOnDestroy: p => Destroy(p.gameObject),
            collectionCheck: false,
            defaultCapacity: defCap,
            maxSize: max
        );
        pools[prefab] = pool;
        return pool;
    }

    public Projectile Get(Projectile prefab, Vector3 worldPos)
    {
        var pool = EnsurePool(prefab);
        var p = pool.Get();
        p.transform.position = worldPos;
        return p;
    }

    public void Release(Projectile instance)
    {
        var prefabRef = instance.SourcePrefab;
        if (prefabRef == null || !pools.TryGetValue(prefabRef, out var pool))
        {
            instance.gameObject.SetActive(false);
            Destroy(instance.gameObject);
            return;
        }
        pool.Release(instance);
    }
}
