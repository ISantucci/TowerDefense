using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Ruta")]
    public WaypointsPath path;
    public int currentIndex = 0;

    [Header("Movimiento")]
    public float moveSpeed = 2.5f;

    Transform target;

    void Start()
    {
        if (path == null || path.points == null || path.points.Length == 0)
        {
            Debug.LogError("[EnemyMovement] WaypointsPath no asignado en " + name);
            enabled = false;
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, path.points.Length - 1);
        target = path.points[currentIndex];

        
        var p = transform.position;
        transform.position = new Vector3(p.x, 0.5f, p.z);
    }

    void Update()
    {
        if (target == null) return;

        Vector3 dir = (target.position - transform.position);
        float step = moveSpeed * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            
            transform.position = target.position;
            currentIndex++;

            if (currentIndex >= path.points.Length)
            {
                
                var e = GetComponent<EnemyTD>();
                if (e != null) e.ReachEnd(); else Destroy(gameObject);
                if (e != null) e.ReachEnd();
                else Destroy(gameObject);
                enabled = false; 
                return;
            }

            target = path.points[currentIndex];
        }
        else
        {
            transform.position += dir.normalized * step;
        }
    }
}
