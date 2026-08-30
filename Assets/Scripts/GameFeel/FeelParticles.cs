using UnityEngine;

/// <summary>
/// Pool compartido de cubitos "partícula" con integración manual (velocidad + gravedad, sin Rigidbody).
/// Lo usan DeathBurst (explosión con el tinte del enemigo) y BuildFeedback (polvo, monedas).
/// Color por instancia con MaterialPropertyBlock sobre un único material.
/// </summary>
public class FeelParticles : MonoBehaviour
{
    public const int PoolSize = 96;

    class P
    {
        public Transform tr;
        public MeshRenderer mr;
        public Vector3 vel;
        public Vector3 spinAxis;
        public float spinSpeed;
        public float age;
        public float life;
        public float size;
        public float gravity;
        public bool active;
    }

    P[] pool;
    int cursor;
    Material material;
    MaterialPropertyBlock block;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        material = GameFeelKit.MakeOpaque(Color.white, 0.5f);
        block = new MaterialPropertyBlock();
        pool = new P[PoolSize];
        var mesh = GameFeelKit.CubeMesh;
        for (int i = 0; i < PoolSize; i++)
        {
            var go = GameFeelKit.MakeMeshObject("FeelCube", mesh, material, transform);
            go.SetActive(false);
            var p = new P();
            p.tr = go.transform;
            p.mr = go.GetComponent<MeshRenderer>();
            pool[i] = p;
        }
    }

    void OnDestroy()
    {
        GameFeelKit.SafeDestroy(material);
    }

    /// <summary>
    /// Ráfaga de cubos desde 'origin'. Velocidad horizontal aleatoria en [minSpeed, maxSpeed],
    /// vertical en [minUp, maxUp]. Cada cubo vive 'life' segundos (±15 %) y se achica hasta desaparecer.
    /// </summary>
    public void Burst(Vector3 origin, Color color, int count, float minSize, float maxSize,
                      float minSpeed, float maxSpeed, float minUp, float maxUp, float life, float gravity)
    {
        if (pool == null) return;
        for (int i = 0; i < count; i++)
        {
            var p = Take();
            if (p == null) return;

            Vector2 dir2 = Random.insideUnitCircle;
            if (dir2.sqrMagnitude < 0.001f) dir2 = Vector2.right;
            dir2.Normalize();
            float speed = Random.Range(minSpeed, maxSpeed);

            p.vel = new Vector3(dir2.x * speed, Random.Range(minUp, maxUp), dir2.y * speed);
            p.size = Random.Range(minSize, maxSize);
            p.life = Mathf.Max(0.05f, life * Random.Range(0.85f, 1.15f));
            p.age = 0f;
            p.gravity = gravity;
            p.spinAxis = Random.onUnitSphere;
            p.spinSpeed = Random.Range(180f, 540f);
            p.active = true;

            p.tr.position = origin + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.1f), Random.Range(-0.15f, 0.15f));
            p.tr.rotation = Random.rotation;
            p.tr.localScale = Vector3.one * p.size;

            Color c = color;
            c.a = 1f;
            block.SetColor(ColorId, c);
            block.SetColor(EmissionId, c * 0.45f);
            p.mr.SetPropertyBlock(block);
            p.tr.gameObject.SetActive(true);
        }
    }

    /// <summary>Variante con color aleatorio entre dos (polvo blanco/gris, monedas de dos tonos).</summary>
    public void Burst(Vector3 origin, Color colorA, Color colorB, int count, float minSize, float maxSize,
                      float minSpeed, float maxSpeed, float minUp, float maxUp, float life, float gravity)
    {
        for (int i = 0; i < count; i++)
            Burst(origin, Color.Lerp(colorA, colorB, Random.value), 1, minSize, maxSize, minSpeed, maxSpeed, minUp, maxUp, life, gravity);
    }

    P Take()
    {
        // Primero un slot libre; si no hay, el más viejo (mayor fracción de vida consumida).
        for (int k = 0; k < PoolSize; k++)
        {
            int i = (cursor + k) % PoolSize;
            if (!pool[i].active)
            {
                cursor = (i + 1) % PoolSize;
                return pool[i];
            }
        }
        P oldest = null;
        float best = -1f;
        for (int i = 0; i < PoolSize; i++)
        {
            var p = pool[i];
            float frac = p.life > 0f ? p.age / p.life : 1f;
            if (frac > best) { best = frac; oldest = p; }
        }
        return oldest;
    }

    void Update()
    {
        if (pool == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        for (int i = 0; i < PoolSize; i++)
        {
            var p = pool[i];
            if (!p.active) continue;
            if (p.tr == null) { p.active = false; continue; }

            p.age += dt;
            if (p.age >= p.life)
            {
                p.active = false;
                p.tr.gameObject.SetActive(false);
                continue;
            }

            p.vel.y += p.gravity * dt;
            Vector3 pos = p.tr.position + p.vel * dt;

            float t = p.age / p.life;
            float scale = p.size * (1f - t * t);
            if (scale < 0.005f) scale = 0.005f;

            // Rebote blando contra el suelo (y = 0).
            float floor = scale * 0.5f;
            if (pos.y < floor)
            {
                pos.y = floor;
                if (p.vel.y < 0f) p.vel.y = -p.vel.y * 0.35f;
                p.vel.x *= 0.7f;
                p.vel.z *= 0.7f;
            }

            p.tr.position = pos;
            p.tr.localScale = new Vector3(scale, scale, scale);
            p.tr.Rotate(p.spinAxis, p.spinSpeed * dt, Space.World);
        }
    }
}
