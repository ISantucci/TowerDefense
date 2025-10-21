using UnityEngine;
using System;

public class Projectile : MonoBehaviour
{
    public float speed = 14f;
    public int damage = 2;

    
    public Projectile SourcePrefab { get; set; }

    Transform target;
    Action<Projectile> onRelease;

    public void FireAt(Transform targetTransform, Action<Projectile> releaseCallback)
    {
        target = targetTransform;
        onRelease = releaseCallback;
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (target == null) { onRelease?.Invoke(this); return; }

        Vector3 dir = target.position - transform.position;
        float step = speed * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            var enemy = target.GetComponent<EnemyTD>();
            if (enemy != null) enemy.TakeDamage(damage);
            onRelease?.Invoke(this);
        }
        else
        {
            transform.position += dir.normalized * step;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void OnDisable()
    {
        target = null;
        onRelease = null;
    }
}
