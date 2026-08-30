using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Barra de vida sobre cada enemigo: un holder (hijo del enemigo, así lo sigue solo) con dos cubos finos
/// sin collider, fondo oscuro y relleno verde→amarillo→rojo según la fracción de vida. El holder se
/// orienta a la cámara en LateUpdate. Oculta hasta el primer golpe; después queda visible.
/// </summary>
public class EnemyHealthBars : MonoBehaviour
{
    public const string HolderName = "HealthBar";
    public const float BarWidth = 0.9f;
    public const float BarHeight = 0.10f;
    public const float BarDepth = 0.03f;
    public const float HeightAboveCenter = 1.1f;

    class Bar
    {
        public EnemyTD enemy;
        public GameObject holder;
        public Transform fill;
        public MeshRenderer fillRenderer;
        public float lastFraction;
    }

    readonly Dictionary<EnemyTD, Bar> bars = new Dictionary<EnemyTD, Bar>();
    readonly List<Bar> list = new List<Bar>(64);

    Material bgMaterial;
    Material fillMaterial;
    MaterialPropertyBlock block;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        bgMaterial = GameFeelKit.MakeOpaque(new Color(0.07f, 0.07f, 0.09f, 1f), 0f);
        fillMaterial = GameFeelKit.MakeOpaque(Color.white, 0.6f);
        block = new MaterialPropertyBlock();
    }

    void OnDestroy()
    {
        GameFeelKit.SafeDestroy(bgMaterial);
        GameFeelKit.SafeDestroy(fillMaterial);
    }

    void OnEnable()
    {
        CombatEvents.EnemySpawned += OnEnemySpawned;
        CombatEvents.DamageDealt += OnDamageDealt;
        CombatEvents.EnemyDied += OnEnemyGone;
        CombatEvents.EnemyReachedEnd += OnEnemyGone;
    }

    void OnDisable()
    {
        CombatEvents.EnemySpawned -= OnEnemySpawned;
        CombatEvents.DamageDealt -= OnDamageDealt;
        CombatEvents.EnemyDied -= OnEnemyGone;
        CombatEvents.EnemyReachedEnd -= OnEnemyGone;

        // Al desactivarse el sistema, sacamos las barras que queden colgando de enemigos vivos.
        for (int i = 0; i < list.Count; i++)
            if (list[i].holder != null) Destroy(list[i].holder);
        list.Clear();
        bars.Clear();
    }

    void OnEnemySpawned(EnemyTD enemy)
    {
        if (enemy == null) return;
        GetOrCreate(enemy);
    }

    void OnDamageDealt(EnemyTD enemy, int amount, Vector3 pos)
    {
        if (enemy == null) return;
        var bar = GetOrCreate(enemy);
        if (bar == null || bar.holder == null) return;

        if (!bar.holder.activeSelf)
        {
            bar.holder.SetActive(true);
            Quaternion rot;
            if (GameFeelKit.TryGetCameraRotation(out rot)) bar.holder.transform.rotation = rot;
        }
        SetFill(bar, enemy.HealthFraction);
    }

    void OnEnemyGone(EnemyTD enemy)
    {
        if (enemy == null) return;
        Bar bar;
        if (!bars.TryGetValue(enemy, out bar)) return;
        Remove(bar);
    }

    Bar GetOrCreate(EnemyTD enemy)
    {
        Bar bar;
        if (bars.TryGetValue(enemy, out bar))
        {
            if (bar.holder != null) return bar;
            Remove(bar);   // el holder murió por afuera: recrear
        }

        bar = new Bar();
        bar.enemy = enemy;
        bar.lastFraction = -1f;

        var holder = new GameObject(HolderName);
        var ht = holder.transform;
        ht.SetParent(enemy.transform, false);

        // Compensar la escala del enemigo para que la barra mida lo mismo en el mundo,
        // y ubicarla a 1.1 * escala por encima del centro.
        Vector3 lossy = enemy.transform.lossyScale;
        float sx = Mathf.Abs(lossy.x) > 0.001f ? 1f / lossy.x : 1f;
        float sy = Mathf.Abs(lossy.y) > 0.001f ? 1f / lossy.y : 1f;
        float sz = Mathf.Abs(lossy.z) > 0.001f ? 1f / lossy.z : 1f;
        float enemyScale = enemy.data != null && enemy.data.scale > 0f ? enemy.data.scale : 1f;
        ht.localScale = new Vector3(sx, sy, sz);
        ht.localPosition = new Vector3(0f, HeightAboveCenter * enemyScale * sy, 0f);

        var mesh = GameFeelKit.CubeMesh;
        var bg = GameFeelKit.MakeMeshObject("HealthBarBg", mesh, bgMaterial, ht);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(BarWidth, BarHeight, BarDepth);

        var fill = GameFeelKit.MakeMeshObject("HealthBarFill", mesh, fillMaterial, ht);
        fill.transform.localPosition = new Vector3(0f, 0f, -BarDepth * 0.4f);   // hacia la cámara (local -Z)
        bar.fill = fill.transform;
        bar.fillRenderer = fill.GetComponent<MeshRenderer>();

        bar.holder = holder;
        holder.SetActive(false);   // oculta hasta el primer golpe

        bars[enemy] = bar;
        list.Add(bar);
        SetFill(bar, enemy.HealthFraction);
        return bar;
    }

    void SetFill(Bar bar, float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        if (bar.fill == null || bar.fillRenderer == null) return;
        if (Mathf.Abs(fraction - bar.lastFraction) < 0.0005f) return;
        bar.lastFraction = fraction;

        float innerW = BarWidth - 0.04f;
        float innerH = BarHeight - 0.03f;
        float w = innerW * fraction;
        bar.fill.localScale = new Vector3(Mathf.Max(0.0001f, w), innerH, BarDepth);
        bar.fill.localPosition = new Vector3(-innerW * 0.5f + w * 0.5f, 0f, -BarDepth * 0.4f);

        Color c = FractionColor(fraction);
        block.SetColor(ColorId, c);
        block.SetColor(EmissionId, c * 0.55f);
        bar.fillRenderer.SetPropertyBlock(block);
    }

    static Color FractionColor(float f)
    {
        Color green = new Color(0.25f, 0.9f, 0.3f);
        Color yellow = new Color(1f, 0.85f, 0.2f);
        Color red = new Color(0.95f, 0.2f, 0.15f);
        if (f > 0.5f) return Color.Lerp(yellow, green, (f - 0.5f) * 2f);
        return Color.Lerp(red, yellow, f * 2f);
    }

    void Remove(Bar bar)
    {
        if (bar == null) return;
        if (bar.holder != null) Destroy(bar.holder);
        if (bar.enemy != null) bars.Remove(bar.enemy);
        int idx = list.IndexOf(bar);
        if (idx >= 0)
        {
            int last = list.Count - 1;
            list[idx] = list[last];
            list.RemoveAt(last);
        }
    }

    void LateUpdate()
    {
        Quaternion camRot;
        bool hasCam = GameFeelKit.TryGetCameraRotation(out camRot);

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var bar = list[i];
            if (bar.enemy == null || bar.holder == null)
            {
                // El enemigo se destruyó por afuera (o el holder con él): limpiar la entrada.
                if (bar.holder != null) Destroy(bar.holder);
                if (bar.enemy != null) bars.Remove(bar.enemy);
                else PruneDeadKeys();
                int last = list.Count - 1;
                list[i] = list[last];
                list.RemoveAt(last);
                continue;
            }

            if (hasCam && bar.holder.activeSelf)
                bar.holder.transform.rotation = camRot;
        }
    }

    // Un EnemyTD destruido sigue siendo una clave válida del diccionario (hash por instancia):
    // cada tanto barremos las claves muertas para que no crezca.
    readonly List<EnemyTD> deadKeys = new List<EnemyTD>();
    void PruneDeadKeys()
    {
        deadKeys.Clear();
        foreach (var kv in bars)
            if (kv.Key == null) deadKeys.Add(kv.Key);
        for (int i = 0; i < deadKeys.Count; i++) bars.Remove(deadKeys[i]);
        deadKeys.Clear();
    }
}
