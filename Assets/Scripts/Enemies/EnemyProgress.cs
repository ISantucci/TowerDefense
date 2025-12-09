using UnityEngine;

[RequireComponent(typeof(EnemyMovement))]
public class EnemyProgress : MonoBehaviour
{
    EnemyMovement movement;

    float totalDistance;
    float[] prefixDistances;   

    public float progressValue { get; private set; }  // 0 = inicio, 1 = fin

    void Start()
    {
        movement = GetComponent<EnemyMovement>();

        var route = movement.Route;
        if (movement == null || route == null || route.Count < 2)
        {
            Debug.LogError($"[EnemyProgress] Ruta inválida en {name}");
            enabled = false;
            return;
        }

        int n = route.Count;
        prefixDistances = new float[n];
        prefixDistances[0] = 0f;

        for (int i = 1; i < n; i++)
        {
            if (route[i - 1] == null || route[i] == null)
            {
                prefixDistances[i] = prefixDistances[i - 1];
                continue;
            }

            float seg = Vector3.Distance(route[i - 1].position, route[i].position);
            prefixDistances[i] = prefixDistances[i - 1] + seg;
        }

        totalDistance = prefixDistances[n - 1];
        if (totalDistance <= 0f)
        {
            Debug.LogWarning($"[EnemyProgress] totalDistance=0 en {name}");
            totalDistance = 1f;
        }
    }

    void Update()
    {
        if (movement == null) return;

        var route = movement.Route;
        if (route == null || route.Count == 0) return;

        int n = route.Count;

        // currentIndex en EnemyMovement apunta al "target" actual.
        int idx = Mathf.Clamp(movement.CurrentIndex, 0, n - 1);

        float covered = 0f;

        // Distancia de todos los segmentos completamente recorridos
        if (idx > 0)
            covered = prefixDistances[idx - 1];

        // Punto de referencia del tramo parcial
        Vector3 prevPointPos;
        if (idx == 0)
            prevPointPos = route[0].position;
        else
            prevPointPos = route[idx - 1].position;

        // Sumamos cuánto avanzó desde prevPoint hasta la posición actual
        covered += Vector3.Distance(prevPointPos, transform.position);

        // Normalizamos
        progressValue = Mathf.Clamp01(covered / totalDistance);

        // Avisamos al ABB
        EnemyPriorityABB.Instance?.UpdateProgress(this);

        var enemy = movement.GetComponent<EnemyTD>();
        if (enemy != null)
            progressValue += enemy.uniqueId * 0.0001f;
    }
}
