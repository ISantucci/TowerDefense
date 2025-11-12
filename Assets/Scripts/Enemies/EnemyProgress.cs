using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyMovement))]
public class EnemyProgress : MonoBehaviour
{
    EnemyMovement movement;
    float totalDistance;
    public float progressValue { get; private set; }  // 0 = inicio, 1 = llegó

    IReadOnlyList<Transform> Route => movement.Route;

    void Start()
    {
        movement = GetComponent<EnemyMovement>();

        if (movement == null || Route == null || Route.Count < 2)
        {
            Debug.LogError($"[EnemyProgress] Ruta inválida en {name}");
            enabled = false; return;
        }

        // Distancia total de toda la ruta
        totalDistance = 0f;
        for (int i = 1; i < Route.Count; i++)
            totalDistance += Vector3.Distance(
                Route[i - 1].position,
                Route[i].position);

        // me registro en el ABB
        EnemyPriorityABB.Instance?.Insert(this);
    }

    void OnDestroy()
    {
        // me saco del ABB si muero / llego
        EnemyPriorityABB.Instance?.Remove(this);
    }

    void Update()
    {
        if (!enabled || movement == null || Route == null || Route.Count == 0)
            return;

        int idx = Mathf.Clamp(movement.CurrentIndex, 0, Route.Count - 1);

        // distancia recorrida hasta el waypoint actual
        float covered = 0f;
        for (int i = 1; i <= idx && i < Route.Count; i++)
            covered += Vector3.Distance(
                Route[i - 1].position,
                Route[i].position);

        // más la parcial hacia el siguiente waypoint
        if (idx < Route.Count)
        {
            var next = Route[idx];
            covered += Vector3.Distance(transform.position, next.position);
        }

        progressValue = Mathf.Clamp01(covered / totalDistance);

        // actualizo mi posición en el ABB
        EnemyPriorityABB.Instance?.UpdateProgress(this);
    }
}
