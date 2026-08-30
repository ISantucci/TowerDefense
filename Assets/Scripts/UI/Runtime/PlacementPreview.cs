using UnityEngine;

/// <summary>
/// Vista previa en el mundo mientras hay una torre seleccionada: círculo de alcance (LineRenderer)
/// sobre el spot bajo el cursor (o el suelo en modo libre); verde si se puede pagar y el lugar está libre,
/// rojo si no. Segundo círculo punteado y más fino para el alcance mínimo.
/// </summary>
public class PlacementPreview : MonoBehaviour
{
    const int Segments = 48;
    const float RangeWidth = 0.06f;
    const float MinRangeWidth = 0.035f;
    const float YOffset = 0.07f;

    static readonly Color GoodColor = new Color(0.3f, 1f, 0.4f, 0.8f);
    static readonly Color BadColor = new Color(1f, 0.3f, 0.25f, 0.8f);

    LevelController rt;
    LineRenderer rangeLine;
    LineRenderer minLine;
    readonly Vector3[] points = new Vector3[Segments];
    Texture2D dashTexture;
    bool visible;

    /// <summary>LevelHUD lo llama al arrancar; los LineRenderer se crean como hijos de este objeto de la escena.</summary>
    public void Bind(LevelController controller)
    {
        rt = controller;
        if (rangeLine == null) Build();
    }

    void Build()
    {
        rangeLine = MakeLine("RangeCircle", RangeWidth, null);
        dashTexture = MakeDashTexture();
        minLine = MakeLine("MinRangeCircle", MinRangeWidth, dashTexture);
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (dashTexture != null) Destroy(dashTexture);
    }

    LineRenderer MakeLine(string name, float width, Texture2D tex)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = Segments;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        if (tex != null)
        {
            mat.mainTexture = tex;
            lr.textureMode = LineTextureMode.Tile;   // una repetición por unidad de mundo → guiones
        }
        else
        {
            lr.textureMode = LineTextureMode.Stretch;
        }
        lr.material = mat;
        lr.startColor = GoodColor;
        lr.endColor = GoodColor;
        return lr;
    }

    /// <summary>Textura 16x1 con dos ciclos on/off: en modo Tile da 4 guiones por unidad de mundo.</summary>
    static Texture2D MakeDashTexture()
    {
        const int W = 16;
        var t = new Texture2D(W, 1, TextureFormat.RGBA32, false);
        t.name = "PreviewDash";
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Point;
        var px = new Color[W];
        for (int i = 0; i < W; i++)
        {
            bool on = (i % 8) < 5;
            px[i] = on ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
        t.SetPixels(px);
        t.Apply(false, false);
        return t;
    }

    // ───────────────────────── update ─────────────────────────

    void Update()
    {
        if (rt == null || rt.Placer == null || !rt.Placer.HasSelection || rt.IsFinished)
        {
            SetVisible(false);
            return;
        }

        var data = rt.Placer.SelectedData;
        if (data == null || (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
        {
            SetVisible(false);
            return;
        }

        Vector3 pos;
        bool free;
        if (!ResolveSpot(out pos, out free))
        {
            SetVisible(false);
            return;
        }

        int cost = rt.Placer.SelectedCost;
        if (cost <= 0) cost = data.cost;
        bool affordable = GameManager.I == null || GameManager.I.Money >= cost;
        Color c = (affordable && free) ? GoodColor : BadColor;

        SetVisible(true);
        Draw(rangeLine, pos, Mathf.Max(0.1f, data.range), c);

        if (data.minRange > 0.01f)
        {
            minLine.enabled = true;
            Draw(minLine, pos, data.minRange, new Color(c.r, c.g, c.b, 0.65f));
        }
        else
        {
            minLine.enabled = false;
        }
    }

    bool ResolveSpot(out Vector3 pos, out bool free)
    {
        pos = Vector3.zero;
        free = false;

        if (rt.UsesSpots)
        {
            var spot = rt.HoveredSpot;
            if (spot != null)
            {
                pos = spot.worldPosition;
                free = !spot.IsOccupied;
                return true;
            }
        }

        var cam = rt.Cam != null ? rt.Cam : Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        int mask = 1 << GameLayers.Ground;
        if (!Physics.Raycast(ray, out hit, 300f, mask, QueryTriggerInteraction.Ignore)) return false;

        Vector3 p = hit.point;
        if (rt.Grid != null)
        {
            p = rt.Grid.Snap(p);
            p = rt.Grid.SnapToGroundY(p, 10f, mask);
        }
        pos = p;

        if (rt.UsesSpots)
        {
            var spot = rt.SpotAt(p);
            free = spot != null && !spot.IsOccupied;
            if (spot != null) pos = spot.worldPosition;
        }
        else
        {
            free = rt.Grid == null || rt.Grid.CanBuildAt(p);
        }
        return true;
    }

    void Draw(LineRenderer lr, Vector3 center, float radius, Color color)
    {
        if (lr == null) return;
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            points[i] = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + YOffset, center.z + Mathf.Sin(a) * radius);
        }
        lr.positionCount = Segments;
        lr.SetPositions(points);
        lr.startColor = color;
        lr.endColor = color;
    }

    void SetVisible(bool on)
    {
        if (visible == on) return;
        visible = on;
        if (rangeLine != null) rangeLine.enabled = on;
        if (minLine != null) minLine.enabled = on;
    }
}
