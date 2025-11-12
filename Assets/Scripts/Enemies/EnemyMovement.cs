// EnemyMovement.cs
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 2.5f;

    List<Transform> route;
    int currentIndex = 0;
    Transform target;

    // --- NUEVO: propiedades para ABB / progreso ---
    public IReadOnlyList<Transform> Route => route;
    public int CurrentIndex => currentIndex;
    // ----------------------------------------------

    public void SetRoute(IReadOnlyList<Transform> points)
    {
        if (points == null || points.Count == 0)
        {
            Debug.LogError("[EnemyMovement] Ruta nula o vacía.");
            enabled = false; return;
        }

        route = new List<Transform>(points);
        currentIndex = 0;
        target = route[currentIndex];

        var p = transform.position;
        transform.position = new Vector3(p.x, 0.5f, p.z);
    }

    void Update()
    {
        if (route == null || target == null) return;

        Vector3 dir = (target.position - transform.position);
        float step = moveSpeed * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            transform.position = target.position;
            currentIndex++;

            if (currentIndex >= route.Count)
            {
                var e = GetComponent<EnemyTD>();
                if (e != null) e.ReachEnd(); else Destroy(gameObject);
                enabled = false;
                return;
            }

            target = route[currentIndex];
        }
        else
        {
            transform.position += dir.normalized * step;
        }
    }
}
