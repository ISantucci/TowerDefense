using UnityEngine;

[RequireComponent(typeof(Tower))]
public class TowerRangeVisualizer : MonoBehaviour
{
    [SerializeField] LineRenderer lineRenderer;
    [SerializeField] int segments = 96;
    [SerializeField] float yOffset = 0.05f;
    [SerializeField] float lineWidth = 0.05f;
    [SerializeField] Color rangeColor = new Color(0f, 1f, 1f, 0.65f);

    Tower tower;

    void Awake()
    {
        tower = GetComponent<Tower>();
        if (lineRenderer == null)
            lineRenderer = CreateLineRenderer();
        lineRenderer.enabled = false;
    }

    LineRenderer CreateLineRenderer()
    {
        var go = new GameObject("RangeCircle");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = rangeColor;
        lr.endColor = rangeColor;
        lr.positionCount = segments + 1;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    public void Show()
    {
        if (lineRenderer == null) return;
        lineRenderer.enabled = true;
        Refresh();
    }

    public void Hide()
    {
        if (lineRenderer == null) return;
        lineRenderer.enabled = false;
    }

    public void Refresh()
    {
        if (tower == null || lineRenderer == null) return;
        DrawCircle(tower.range);
    }

    void DrawCircle(float radius)
    {
        // El LineRenderer es local: si TowerVisual escaló la torre, el círculo se compensa.
        float s = transform.lossyScale.x;
        if (Mathf.Abs(s) > 0.0001f) radius /= s;
        lineRenderer.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            lineRenderer.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                yOffset,
                Mathf.Sin(angle) * radius
            ));
        }
    }
}
