// Assets/Scripts/Enemies/EnemyMovement.cs
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 2.5f;
    [Tooltip("Altura del centro del enemigo sobre el suelo (la ruta se recorre sólo en XZ).")]
    public float heightOffset = 1f;

    // ruta inyectada por el spawner (Dijkstra / nivel)
    List<Transform> route;   // puntos en orden
    int currentIndex = 0;
    Transform target;

    // Slow temporal (multiplicador sobre moveSpeed)
    float currentMultiplier = 1f;
    float slowTimer = 0f;

    // Stun (no se mueve)
    float stunTimer = 0f;

    // === PROPIEDADES SOLO LECTURA PARA OTRAS CLASES (EnemyProgress, etc.) ===
    public IReadOnlyList<Transform> Route => route;
    public int CurrentIndex => currentIndex;
    public float CurrentSpeedMultiplier => currentMultiplier;
    public bool IsSlowed => slowTimer > 0f;
    public bool IsStunned => stunTimer > 0f;
    public Vector3 MoveDirection { get; private set; }

    // === API: el spawner te setea la ruta antes de empezar a mover ===
    public void SetRoute(IReadOnlyList<Transform> points)
    {
        if (points == null || points.Count == 0)
        {
            enabled = false;
            return;
        }

        route = new List<Transform>(points);
        currentIndex = 0;
        target = route[currentIndex];
        enabled = true;

        var p = transform.position;
        transform.position = new Vector3(p.x, heightOffset, p.z);
    }

    /// <summary>Aplica un multiplicador de velocidad (0.5 = mitad) durante seconds. Un slow nuevo reemplaza al anterior.</summary>
    public void ApplySlow(float multiplier, float seconds)
    {
        if (seconds <= 0f) return;
        currentMultiplier = Mathf.Clamp(multiplier, 0f, 1f);
        slowTimer = seconds;
    }

    /// <summary>Congela al enemigo durante seconds (Tesla eléctrica, trampa de shock...).</summary>
    public void ApplyStun(float seconds)
    {
        if (seconds <= 0f) return;
        stunTimer = Mathf.Max(stunTimer, seconds);
    }

    /// <summary>
    /// Empuja al enemigo hacia atrás por su ruta (hacia el waypoint anterior) una distancia dada.
    /// Si pasa el waypoint anterior, retrocede el índice y re-apunta; nunca baja del índice 0.
    /// </summary>
    public void Knockback(float distance)
    {
        if (route == null || route.Count == 0 || distance <= 0f) return;

        float remaining = distance;

        while (remaining > 0f && currentIndex > 0)
        {
            Transform prev = route[currentIndex - 1];
            if (prev == null) break;

            Vector3 toPrev = Flat(prev.position - transform.position);
            float dist = toPrev.magnitude;

            if (remaining < dist)
            {
                transform.position += toPrev.normalized * remaining;
                remaining = 0f;
            }
            else
            {
                transform.position = new Vector3(prev.position.x, transform.position.y, prev.position.z);
                remaining -= dist;
                currentIndex--;
                target = route[currentIndex];
            }
        }

        var prog = GetComponent<EnemyProgress>();
        if (prog != null) prog.Recalculate();
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    void Update()
    {
        if (route == null || target == null) return;

        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                slowTimer = 0f;
                currentMultiplier = 1f;
            }
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            return;
        }

        Vector3 dir = Flat(target.position - transform.position);
        float speed = moveSpeed * currentMultiplier;
        float step = speed * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            transform.position = new Vector3(target.position.x, transform.position.y, target.position.z);
            currentIndex++;

            if (currentIndex >= route.Count)
            {
                var e = GetComponent<EnemyTD>();
                if (e != null) e.ReachEnd();
                else Destroy(gameObject);

                enabled = false;
                return;
            }

            target = route[currentIndex];
        }
        else
        {
            Vector3 n = dir.normalized;
            MoveDirection = n;
            transform.position += n * step;
        }
    }
}
