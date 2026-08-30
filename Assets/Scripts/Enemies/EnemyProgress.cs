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

        var route = movement != null ? movement.Route : null;
        if (movement == null || route == null || route.Count < 2)
        {
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
            totalDistance = 1f;
        }
    }

    void Update()
    {
        Recalculate();
    }

    /// <summary>Recalcula progressValue y lo reubica en el ABB. Seguro de llamar fuera de Update (p.ej. tras un Knockback).</summary>
    public void Recalculate()
    {
        if (movement == null || prefixDistances == null) return;

        var route = movement.Route;
        if (route == null || route.Count == 0) return;

        int n = route.Count;
        int idx = Mathf.Clamp(movement.CurrentIndex, 0, n - 1);

        float covered = 0f;

        if (idx > 0)
            covered = prefixDistances[idx - 1];

        Vector3 prevPointPos = (idx == 0) ? route[0].position : route[idx - 1].position;
        covered += Vector3.Distance(prevPointPos, transform.position);

        float baseProgress = Mathf.Clamp01(covered / totalDistance);

        float epsilon = 0f;
        var enemy = movement.GetComponent<EnemyTD>();
        if (enemy != null)
            epsilon = enemy.uniqueId * 0.0001f;

        progressValue = baseProgress + epsilon;

        EnemyPriorityABB.Instance?.UpdateProgress(this);
    }
}
