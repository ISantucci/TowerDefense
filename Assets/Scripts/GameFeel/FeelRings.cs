using UnityEngine;

/// <summary>
/// Pool compartido de "anillos" planos (cilindro aplastado, material Fade) que se expanden y se
/// desvanecen: fuga de enemigo (rojo), empuje (celeste), mejora de torre (amarillo).
/// </summary>
public class FeelRings : MonoBehaviour
{
    public const int PoolSize = 12;

    class R
    {
        public Transform tr;
        public MeshRenderer mr;
        public Color color;
        public float startRadius;
        public float endRadius;
        public float age;
        public float duration;
        public bool active;
    }

    R[] pool;
    int cursor;
    Material material;
    MaterialPropertyBlock block;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        material = GameFeelKit.MakeTransparent(new Color(1f, 1f, 1f, 0.6f));
        block = new MaterialPropertyBlock();
        pool = new R[PoolSize];
        var mesh = GameFeelKit.CylinderMesh;
        for (int i = 0; i < PoolSize; i++)
        {
            var go = GameFeelKit.MakeMeshObject("FeelRing", mesh, material, transform);
            go.SetActive(false);
            var r = new R();
            r.tr = go.transform;
            r.mr = go.GetComponent<MeshRenderer>();
            pool[i] = r;
        }
    }

    void OnDestroy()
    {
        GameFeelKit.SafeDestroy(material);
    }

    /// <summary>Anillo en 'center' (usar y ≈ 0.05 para el suelo) que crece de startRadius a endRadius en 'duration' segundos.</summary>
    public void Spawn(Vector3 center, Color color, float startRadius, float endRadius, float duration)
    {
        if (pool == null) return;
        var r = Take();
        if (r == null) return;

        r.color = color;
        r.startRadius = Mathf.Max(0.01f, startRadius);
        r.endRadius = Mathf.Max(r.startRadius, endRadius);
        r.duration = Mathf.Max(0.05f, duration);
        r.age = 0f;
        r.active = true;

        r.tr.position = center;
        r.tr.rotation = Quaternion.identity;
        Apply(r, 0f);
        r.tr.gameObject.SetActive(true);
    }

    R Take()
    {
        for (int k = 0; k < PoolSize; k++)
        {
            int i = (cursor + k) % PoolSize;
            if (!pool[i].active)
            {
                cursor = (i + 1) % PoolSize;
                return pool[i];
            }
        }
        R oldest = null;
        float best = -1f;
        for (int i = 0; i < PoolSize; i++)
        {
            var r = pool[i];
            float frac = r.duration > 0f ? r.age / r.duration : 1f;
            if (frac > best) { best = frac; oldest = r; }
        }
        return oldest;
    }

    void Apply(R r, float t)
    {
        float radius = Mathf.Lerp(r.startRadius, r.endRadius, GameFeelKit.EaseOutCubic(t));
        // El cilindro primitivo tiene radio 0.5 y altura 2: escala x/z = diámetro, y muy chato.
        r.tr.localScale = new Vector3(radius * 2f, 0.015f, radius * 2f);

        Color c = r.color;
        c.a = r.color.a * (1f - GameFeelKit.EaseInQuad(t));
        block.SetColor(ColorId, c);
        block.SetColor(EmissionId, new Color(c.r, c.g, c.b, 1f) * 0.5f * (1f - t));
        r.mr.SetPropertyBlock(block);
    }

    void Update()
    {
        if (pool == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        for (int i = 0; i < PoolSize; i++)
        {
            var r = pool[i];
            if (!r.active) continue;
            if (r.tr == null) { r.active = false; continue; }

            r.age += dt;
            float t = r.age / r.duration;
            if (t >= 1f)
            {
                r.active = false;
                r.tr.gameObject.SetActive(false);
                continue;
            }
            Apply(r, t);
        }
    }
}
