using UnityEngine;

[DefaultExecutionOrder(-10)]
public class BuildGrid : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Tamaño de celda para el snap")]
    public float cellSize = 1f;

    [Header("Bloqueos")]
    [Tooltip("Layers donde NO se puede construir (EnemyPath, Torres, Obstáculos)")]
    public LayerMask blockedMask;

    [Tooltip("Radio de chequeo para validar construcción")]
    public float checkRadius = 0.45f;

    /// Snap X/Z al tamaño de celda. Mantiene Y tal como viene.
    public Vector3 Snap(Vector3 worldPos)
    {
        float x = Mathf.Round(worldPos.x / cellSize) * cellSize;
        float z = Mathf.Round(worldPos.z / cellSize) * cellSize;
        return new Vector3(x, worldPos.y, z);
    }

    /// True si NO colisiona con layers bloqueados alrededor del punto
    public bool CanBuildAt(Vector3 worldPos)
    {
        return !Physics.CheckSphere(
            worldPos,
            checkRadius,
            blockedMask.value,                  // Unity 2021.3 friendly
            QueryTriggerInteraction.Ignore
        );
    }

    /// Opcional: ajustar Y al suelo si tu piso no está plano
    public Vector3 SnapToGroundY(Vector3 snappedXZ, float maxDowncast = 10f, LayerMask groundMask = default)
    {
        Vector3 p = snappedXZ + Vector3.up * maxDowncast;
        if (Physics.Raycast(p, Vector3.down, out var hit, maxDowncast * 2f, groundMask.value, QueryTriggerInteraction.Ignore))
            return new Vector3(snappedXZ.x, hit.point.y, snappedXZ.z);

        return snappedXZ;
    }
}
