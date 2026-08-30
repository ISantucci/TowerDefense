using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Íconos procedurales de torre (64x64): fondo cuadrado redondeado en el color de la familia
/// (bordes oscurecidos) y un glifo en el color del tipo de ataque, dibujados por píxel con
/// campos de distancia. Cache por TowerId.
/// </summary>
public static class TowerIconFactory
{
    public const int Size = 64;

    static readonly Dictionary<TowerId, Sprite> cache = new Dictionary<TowerId, Sprite>();
    static readonly Color HaloColor = new Color(0.03f, 0.03f, 0.06f, 1f);
    static readonly Color DetailColor = new Color(0.08f, 0.08f, 0.12f, 1f);

    public static Sprite Get(TowerData data)
    {
        if (data == null) return null;
        Sprite s;
        if (cache.TryGetValue(data.id, out s) && s != null) return s;
        s = Build(data);
        cache[data.id] = s;
        return s;
    }

    public static void ClearCache()
    {
        cache.Clear();
    }

    static Sprite Build(TowerData data)
    {
        Color family = DefensePalette.Family(data.source);
        Color attack = DefensePalette.Attack(data.attackType);

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.name = "TowerIcon_" + data.id;
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color[Size * Size];
        float half = Size * 0.5f;
        Color edge = Mul(family, 0.42f);

        for (int y = 0; y < Size; y++)
        {
            float shade = 0.86f + 0.14f * (y / (float)(Size - 1)); // más claro arriba
            for (int x = 0; x < Size; x++)
            {
                float fx = x + 0.5f - half;
                float fy = y + 0.5f - half;

                // fondo: cuadrado redondeado con bordes oscurecidos
                float dBg = RoundedBox(fx, fy, half - 2f, half - 2f, 12f);
                float aBg = Mathf.Clamp01(0.5f - dBg);
                float inner = Mathf.Clamp01(-dBg / 11f);
                Color c = Color.Lerp(edge, family, Mathf.Sqrt(inner));
                c = Mul(c, shade);
                c.a = aBg;

                // glifo con halo oscuro para contraste
                float dG = Glyph(data.attackType, fx, fy);
                float halo = Mathf.Clamp01(0.5f - (dG - 2.2f)) * 0.6f;
                float glyph = Mathf.Clamp01(0.5f - dG);
                c = Blend(c, HaloColor, halo * aBg);
                c = Blend(c, attack, glyph * aBg);

                // detalles oscuros sobre el glifo (punto central, puerta...)
                float dD = Detail(data.attackType, fx, fy);
                float det = Mathf.Clamp01(0.5f - dD);
                c = Blend(c, DetailColor, det * aBg);

                px[y * Size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        var sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = "TowerIcon_" + data.id;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // ───────────────────────── glifos ─────────────────────────

    static float Glyph(AttackType t, float x, float y)
    {
        switch (t)
        {
            case AttackType.SingleTarget:
                return Circle(x, y, 0f, 0f, 14f);

            case AttackType.Splash:
                return Mathf.Min(Ring(x, y, 17f, 3.5f), Circle(x, y, 0f, 0f, 7f));

            case AttackType.MultiTarget:
                return Mathf.Min(Circle(x, y, -13f, -7f, 6.5f),
                       Mathf.Min(Circle(x, y, 13f, -7f, 6.5f), Circle(x, y, 0f, 11f, 6.5f)));

            case AttackType.Burst:
                return Mathf.Min(Box(x, y, -13f, 0f, 3.2f, 10f),
                       Mathf.Min(Box(x, y, 0f, 0f, 3.2f, 15f), Box(x, y, 13f, 0f, 3.2f, 10f)));

            case AttackType.Beam:
                return Mathf.Min(Box(x, y, 0f, -5f, 3.5f, 17f),
                       Mathf.Min(Circle(x, y, 0f, 15f, 7.5f), Segment(x, y, -14f, 15f, 14f, 15f, 3f)));

            case AttackType.Chain:
                return Mathf.Min(Segment(x, y, -20f, -12f, -7f, 10f, 4.5f),
                       Mathf.Min(Segment(x, y, -7f, 10f, 5f, -10f, 4.5f), Segment(x, y, 5f, -10f, 20f, 12f, 4.5f)));

            case AttackType.Push:
            {
                float d = float.MaxValue;
                for (int i = 0; i < 2; i++)
                {
                    float k = i == 0 ? -9f : 7f;
                    d = Mathf.Min(d, Segment(x, y, k - 6f, -12f, k + 6f, 0f, 4.5f));
                    d = Mathf.Min(d, Segment(x, y, k + 6f, 0f, k - 6f, 12f, 4.5f));
                }
                return d;
            }

            case AttackType.Trap:
                return Mathf.Min(Segment(x, y, -15f, -15f, 15f, 15f, 5f), Segment(x, y, -15f, 15f, 15f, -15f, 5f));

            case AttackType.Spawner:
                return Mathf.Min(Box(x, y, 0f, -7f, 12f, 9f), Triangle(x, y, -16f, 3f, 16f, 3f, 0f, 19f));

            case AttackType.Support:
                return Mathf.Min(Box(x, y, 0f, 0f, 16f, 4.5f), Box(x, y, 0f, 0f, 4.5f, 16f));

            default:
                return Diamond(x, y, 16f);
        }
    }

    static float Detail(AttackType t, float x, float y)
    {
        switch (t)
        {
            case AttackType.SingleTarget: return Circle(x, y, 0f, 0f, 4.5f);
            case AttackType.Spawner: return Box(x, y, 0f, -11f, 3.2f, 5f);
            default: return float.MaxValue;
        }
    }

    // ───────────────────────── campos de distancia ─────────────────────────

    static float Circle(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    static float Ring(float x, float y, float r, float thickness)
    {
        return Mathf.Abs(Circle(x, y, 0f, 0f, r)) - thickness * 0.5f;
    }

    static float Box(float x, float y, float cx, float cy, float halfW, float halfH)
    {
        float qx = Mathf.Abs(x - cx) - halfW;
        float qy = Mathf.Abs(y - cy) - halfH;
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f);
    }

    static float RoundedBox(float x, float y, float halfW, float halfH, float radius)
    {
        return Box(x, y, 0f, 0f, halfW - radius, halfH - radius) - radius;
    }

    static float Diamond(float x, float y, float r)
    {
        return (Mathf.Abs(x) + Mathf.Abs(y) - r) * 0.7071f;
    }

    static float Segment(float px, float py, float ax, float ay, float bx, float by, float thickness)
    {
        float ex = bx - ax, ey = by - ay;
        float pax = px - ax, pay = py - ay;
        float len2 = ex * ex + ey * ey;
        float h = len2 > 1e-6f ? Mathf.Clamp01((pax * ex + pay * ey) / len2) : 0f;
        float dx = pax - ex * h, dy = pay - ey * h;
        return Mathf.Sqrt(dx * dx + dy * dy) - thickness * 0.5f;
    }

    /// <summary>Triángulo convexo: distancia con signo (exacta adentro, aproximada afuera).</summary>
    static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        if (area < 0f)
        {
            float tx = bx, ty = by;
            bx = cx; by = cy;
            cx = tx; cy = ty;
        }
        float d1 = EdgeDistance(x, y, ax, ay, bx, by);
        float d2 = EdgeDistance(x, y, bx, by, cx, cy);
        float d3 = EdgeDistance(x, y, cx, cy, ax, ay);
        return Mathf.Max(d1, Mathf.Max(d2, d3));
    }

    /// <summary>Positivo a la derecha de la arista (afuera de un polígono CCW), negativo adentro.</summary>
    static float EdgeDistance(float px, float py, float ax, float ay, float bx, float by)
    {
        float ex = bx - ax, ey = by - ay;
        float len = Mathf.Sqrt(ex * ex + ey * ey);
        if (len < 1e-5f) return 0f;
        float cross = ex * (py - ay) - ey * (px - ax);
        return -cross / len;
    }

    // ───────────────────────── color ─────────────────────────

    static Color Mul(Color c, float k)
    {
        return new Color(c.r * k, c.g * k, c.b * k, 1f);
    }

    static Color Blend(Color under, Color over, float a)
    {
        if (a <= 0f) return under;
        return new Color(Mathf.Lerp(under.r, over.r, a), Mathf.Lerp(under.g, over.g, a), Mathf.Lerp(under.b, over.b, a),
                         Mathf.Max(under.a, a));
    }
}
