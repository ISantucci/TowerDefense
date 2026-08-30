using UnityEngine;

/// <summary>
/// Marcador de un lugar de construcción (prefab BuildSpot, instanciado en la escena del nivel).
/// Sabe su celda, si está ocupado y cómo iluminarse. La ocupación se decide mirando Tower.Instances
/// (no guarda estado que se pueda desincronizar con undo/sell).
/// </summary>
public class BuildSpot : MonoBehaviour
{
    [Header("Celda (se recalcula desde la posición al arrancar)")]
    public Vector2Int cell;
    public Vector3 worldPosition;

    [Header("Piezas (cableadas en el prefab)")]
    [SerializeField] MeshRenderer pad;
    [SerializeField] GameObject ring;
    [SerializeField] Material normalMaterial;
    [SerializeField] Material availableMaterial;

    void Awake()
    {
        worldPosition = transform.position;
        if (ring != null) ring.SetActive(false);
    }

    /// <summary>El controlador del nivel lo llama al arrancar: la celda sale de la posición real en la escena.</summary>
    public void RefreshFromTransform(LevelDefinition level)
    {
        worldPosition = transform.position;
        if (level != null) cell = level.WorldToCell(worldPosition);
    }

    /// <summary>Ocupado si hay una torre a menos de media celda del centro.</summary>
    public bool IsOccupied
    {
        get
        {
            var list = Tower.Instances;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null) continue;
                Vector3 d = t.transform.position - worldPosition;
                d.y = 0f;
                if (d.sqrMagnitude < 0.25f) return true;
            }
            return false;
        }
    }

    /// <summary>0 = normal, 1 = disponible (hay torre seleccionada), 2 = bajo el cursor.</summary>
    public void SetHighlight(int state)
    {
        bool occupied = IsOccupied;
        if (pad != null && normalMaterial != null && availableMaterial != null)
            pad.sharedMaterial = (state >= 1 && !occupied) ? availableMaterial : normalMaterial;
        if (ring != null) ring.SetActive(state == 2 && !occupied);
    }
}
