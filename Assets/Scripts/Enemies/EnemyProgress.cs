// Assets/Scripts/Enemies/EnemyProgress.cs
using UnityEngine;

[RequireComponent(typeof(EnemyMovement))]
public class EnemyProgress : MonoBehaviour
{
    private EnemyMovement movement;
    private float totalDistance;
    public float progressValue { get; private set; }  // 0 = inicio, 1 = llegó

    void Start()
    {
        movement = GetComponent<EnemyMovement>();

        var route = movement.Route;
        if (route == null || route.Count < 2)
        {
            Debug.LogError($"[EnemyProgress] Ruta inválida en {name}");
            enabled = false;
            return;
        }

        // Distancia total desde el primer hasta el último punto de la ruta
        totalDistance = 0f;
        for (int i = 1; i < route.Count; i++)
        {
            if (route[i - 1] == null || route[i] == null) continue;
            totalDistance += Vector3.Distance(
                route[i - 1].position,
                route[i].position
            );
        }

        if (totalDistance <= 0f)
        {
            Debug.LogWarning($"[EnemyProgress] totalDistance=0 en {name}");
            totalDistance = 1f; // evitamos división por cero
        }
    }

    void Update()
    {
        var route = movement.Route;
        if (movement == null || route == null || route.Count == 0) return;

        // Calcula distancia recorrida según CurrentIndex + tramo parcial
        float covered = 0f;
        int idx = Mathf.Clamp(movement.CurrentIndex, 0, route.Count - 1);

        // tramos ya completados
        for (int i = 1; i < idx && i < route.Count; i++)
        {
            if (route[i - 1] == null || route[i] == null) continue;
            covered += Vector3.Distance(
                route[i - 1].position,
                route[i].position
            );
        }

        // tramo parcial hacia el siguiente punto
        if (idx < route.Count)
        {
            var next = route[idx];
            if (next != null)
                covered += Vector3.Distance(next.position, transform.position);
        }

        progressValue = Mathf.Clamp01(covered / totalDistance);

        // Actualiza posición en ABB si está activo
        EnemyPriorityABB.Instance?.UpdateProgress(this);
    }
}
