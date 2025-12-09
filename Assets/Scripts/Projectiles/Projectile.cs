using UnityEngine;
using System;

public class Projectile : MonoBehaviour
{
    public ProjectileData data;   

    public float speed = 14f;
    public int damage = 2;
    public float splashRadius = 0f;

    public Projectile SourcePrefab { get; set; }

    Transform target;
    Action<Projectile> onRelease;

    void Awake()
    {
        if (data != null)
            ApplyData(data);
    }

    public void ApplyData(ProjectileData d)
    {
        data = d;
        if (d == null) return;

        speed = d.speed;
        damage = d.damage;
        splashRadius = d.splashRadius;
    }

    public void FireAt(Transform targetTransform, Action<Projectile> releaseCallback)
    {
        target = targetTransform;
        onRelease = releaseCallback;
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (target == null)
        {
            onRelease?.Invoke(this);
            return;
        }

        Vector3 dir = target.position - transform.position;
        float step = speed * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            Hit();
        }
        else
        {
            transform.position += dir.normalized * step;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void Hit()
    {
        Vector3 hitPos = transform.position;

        // AOE
        if (splashRadius > 0f)
        {
            var colliders = Physics.OverlapSphere(hitPos, splashRadius);

            foreach (var c in colliders)
            {
                var enemy = c.GetComponentInParent<EnemyTD>();
                if (enemy != null)
                    enemy.TakeDamage(damage);
            }
        }
        else
        {
            // Mono-objetivo
            var enemy = target != null ? target.GetComponent<EnemyTD>() : null;
            if (enemy != null)
                enemy.TakeDamage(damage);
        }

        onRelease?.Invoke(this);
    }

    void OnDisable()
    {
        target = null;
        onRelease = null;
    }
}
