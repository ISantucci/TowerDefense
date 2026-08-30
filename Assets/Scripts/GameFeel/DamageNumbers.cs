using UnityEngine;

/// <summary>
/// Números de daño flotantes (TextMesh legacy, pooleados): suben 1.2 unidades y se desvanecen en 0.7 s,
/// siempre mirando a la cámara. Blanco; daño ≥ 25 amarillo y más grande; al morir un enemigo, "+bounty" en dorado.
/// Tiempo escalado (se congelan con la pausa, como el resto del juego).
/// </summary>
public class DamageNumbers : MonoBehaviour
{
    public const int PoolSize = 40;
    public const float RiseDistance = 1.2f;
    public const float Life = 0.7f;
    public const float BountyLife = 0.9f;
    public const int BigDamageThreshold = 25;
    public const float BaseCharacterSize = 0.085f;

    static readonly Color NormalColor = Color.white;
    static readonly Color BigColor = new Color(1f, 0.9f, 0.25f);
    static readonly Color BountyColor = new Color(1f, 0.8f, 0.15f);

    class Entry
    {
        public Transform tr;
        public TextMesh tm;
        public Vector3 start;
        public Color color;
        public float size;
        public float age;
        public float life;
        public bool active;
    }

    Entry[] pool;
    int cursor;
    Font font;
    bool warnedNoFont;

    // Cache de strings para no generar basura por golpe.
    static string[] numberCache;
    static string[] bountyCache;

    void Awake()
    {
        font = LoadFont();
        if (font == null && !warnedNoFont)
        {
            warnedNoFont = true;
            Debug.LogWarning("[DamageNumbers] No se encontró la fuente builtin (LegacyRuntime.ttf / Arial.ttf): los números no se verán.");
        }

        pool = new Entry[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("DamageText");
            go.transform.SetParent(transform, false);
            var mr = go.AddComponent<MeshRenderer>();   // TextMesh dibuja con el MeshRenderer del objeto
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = BaseCharacterSize;
            tm.richText = false;
            tm.text = "";
            if (font != null)
            {
                tm.font = font;
                mr.sharedMaterial = font.material;   // el material de la fuente trae el atlas de glifos
            }
            go.SetActive(false);

            var e = new Entry();
            e.tr = go.transform;
            e.tm = tm;
            pool[i] = e;
        }
    }

    static Font LoadFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch (System.Exception) { f = null; }
        if (f != null) return f;
        try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        catch (System.Exception) { f = null; }
        return f;
    }

    void OnEnable()
    {
        CombatEvents.DamageDealt += OnDamageDealt;
        CombatEvents.EnemyDied += OnEnemyDied;
    }

    void OnDisable()
    {
        CombatEvents.DamageDealt -= OnDamageDealt;
        CombatEvents.EnemyDied -= OnEnemyDied;
    }

    void OnDamageDealt(EnemyTD enemy, int amount, Vector3 pos)
    {
        if (enemy != null) pos = enemy.transform.position;
        bool big = amount >= BigDamageThreshold;
        Vector3 spawn = pos + new Vector3(Random.Range(-0.3f, 0.3f), 0.55f + Random.Range(-0.1f, 0.15f), Random.Range(-0.2f, 0.2f));
        Spawn(NumberString(amount), spawn, big ? BigColor : NormalColor, big ? 1.4f : 1f, Life);
    }

    void OnEnemyDied(EnemyTD enemy)
    {
        if (enemy == null) return;
        int bounty = enemy.data != null ? enemy.data.bounty : 0;
        if (bounty <= 0) return;
        Vector3 spawn = enemy.transform.position + new Vector3(0f, 0.9f, 0f);
        Spawn(BountyString(bounty), spawn, BountyColor, 1.25f, BountyLife);
    }

    void Spawn(string text, Vector3 position, Color color, float sizeMul, float life)
    {
        if (pool == null) return;
        var e = Take();
        if (e == null || e.tr == null) return;

        e.start = position;
        e.color = color;
        e.size = BaseCharacterSize * sizeMul;
        e.age = 0f;
        e.life = life;
        e.active = true;

        e.tm.text = text;
        e.tm.characterSize = e.size;
        e.tm.color = color;
        e.tr.position = position;
        e.tr.localScale = Vector3.one * 1.35f;   // pop inicial

        Quaternion rot;
        if (GameFeelKit.TryGetCameraRotation(out rot)) e.tr.rotation = rot;

        e.tr.gameObject.SetActive(true);
    }

    Entry Take()
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
        // Todos ocupados: reutilizar el más viejo.
        Entry oldest = null;
        float best = -1f;
        for (int i = 0; i < PoolSize; i++)
        {
            var e = pool[i];
            float frac = e.life > 0f ? e.age / e.life : 1f;
            if (frac > best) { best = frac; oldest = e; }
        }
        return oldest;
    }

    void Update()
    {
        if (pool == null) return;
        float dt = Time.deltaTime;

        Quaternion camRot;
        bool hasCam = GameFeelKit.TryGetCameraRotation(out camRot);

        for (int i = 0; i < PoolSize; i++)
        {
            var e = pool[i];
            if (!e.active) continue;
            if (e.tr == null) { e.active = false; continue; }

            e.age += dt;
            float t = e.age / e.life;
            if (t >= 1f)
            {
                e.active = false;
                e.tr.gameObject.SetActive(false);
                continue;
            }

            float rise = RiseDistance * GameFeelKit.EaseOutQuad(t);
            e.tr.position = e.start + new Vector3(0f, rise, 0f);
            if (hasCam) e.tr.rotation = camRot;

            // Pop de entrada (1.35 → 1.0 en los primeros 120 ms).
            float pop = 1f + 0.35f * Mathf.Clamp01(1f - e.age / 0.12f);
            e.tr.localScale = new Vector3(pop, pop, pop);

            // Se desvanece sobre todo en la segunda mitad.
            Color c = e.color;
            c.a = 1f - GameFeelKit.EaseInQuad(t);
            e.tm.color = c;
        }
    }

    static string NumberString(int n)
    {
        if (n < 0) n = 0;
        if (numberCache == null) numberCache = new string[400];
        if (n < numberCache.Length)
        {
            if (numberCache[n] == null) numberCache[n] = n.ToString();
            return numberCache[n];
        }
        return n.ToString();
    }

    static string BountyString(int n)
    {
        if (bountyCache == null) bountyCache = new string[200];
        if (n >= 0 && n < bountyCache.Length)
        {
            if (bountyCache[n] == null) bountyCache[n] = "+" + n;
            return bountyCache[n];
        }
        return "+" + n;
    }
}
