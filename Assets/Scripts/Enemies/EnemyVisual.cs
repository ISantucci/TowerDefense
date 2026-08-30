using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Apariencia por tipo de enemigo sobre un prefab compartido: tinte, escala, vaivén aéreo y sombra.
/// También centraliza el "resaltado de líder" que usa el ABB (antes pintaba el material de rojo/blanco a mano).
/// </summary>
public class EnemyVisual : MonoBehaviour
{
    public Color baseColor = Color.white;
    public float bobAmplitude = 0f;
    public float baseHeight = 1f;

    readonly List<Renderer> renderers = new List<Renderer>();
    static Material shadowMaterial;
    Transform shadow;
    float phase;
    bool leader;

    public static void Apply(EnemyTD enemy, EnemyData data)
    {
        if (enemy == null) return;
        var v = enemy.GetComponent<EnemyVisual>();
        if (v == null) v = enemy.gameObject.AddComponent<EnemyVisual>();
        v.Configure(data);
    }

    public static void SetLeader(GameObject go, bool isLeader)
    {
        if (go == null) return;
        var v = go.GetComponent<EnemyVisual>();
        if (v == null) v = go.AddComponent<EnemyVisual>();
        v.leader = isLeader;
        v.ApplyColor();
    }

    void Configure(EnemyData data)
    {
        renderers.Clear();
        GetComponentsInChildren<Renderer>(true, renderers);
        // la sombra no se tiñe
        for (int i = renderers.Count - 1; i >= 0; i--)
            if (renderers[i] != null && renderers[i].name == "Shadow") renderers.RemoveAt(i);

        if (data != null)
        {
            baseColor = data.tint;
            bobAmplitude = data.isFlying ? Mathf.Max(0.05f, data.bobAmplitude) : 0f;
            baseHeight = data.heightOffset > 0f ? data.heightOffset : 1f;
            if (data.scale > 0f && Mathf.Abs(data.scale - 1f) > 0.001f)
                transform.localScale = transform.localScale * data.scale;
        }

        phase = Random.value * 6.28f;
        ApplyColor();
        EnsureShadow();
    }

    void ApplyColor()
    {
        Color c = leader ? Color.Lerp(baseColor, new Color(1f, 0.25f, 0.2f), 0.55f) : baseColor;
        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var m = r.material;   // instancia por enemigo (el prefab comparte el material)
            if (m != null && m.HasProperty("_Color")) m.color = c;
        }
    }

    void EnsureShadow()
    {
        if (shadow != null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Shadow";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(transform, false);
        var mr = go.GetComponent<MeshRenderer>();
        if (shadowMaterial == null)
        {
            var sh = Shader.Find("Standard");
            shadowMaterial = sh != null ? new Material(sh) : new Material(Shader.Find("Diffuse"));
            shadowMaterial.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            if (shadowMaterial.HasProperty("_Glossiness")) shadowMaterial.SetFloat("_Glossiness", 0f);
        }
        mr.sharedMaterial = shadowMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        shadow = go.transform;
        UpdateShadow();
    }

    void UpdateShadow()
    {
        if (shadow == null) return;
        float s = 1f / Mathf.Max(0.01f, transform.localScale.x);
        float size = bobAmplitude > 0f ? 0.55f : 0.7f;
        shadow.localScale = new Vector3(size * s, 0.015f * s, size * s);
        shadow.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
    }

    void OnDestroy()
    {
        // Los materiales instanciados por tinte (r.material) se liberan con el enemigo.
        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var m = r.sharedMaterial;
            if (m != null && m != shadowMaterial && m.name.EndsWith("(Instance)")) Destroy(m);
        }
    }

    void Update()
    {
        if (bobAmplitude > 0f)
        {
            var p = transform.position;
            p.y = baseHeight + Mathf.Sin(Time.time * 2.6f + phase) * bobAmplitude;
            transform.position = p;
        }
        UpdateShadow();
    }
}
