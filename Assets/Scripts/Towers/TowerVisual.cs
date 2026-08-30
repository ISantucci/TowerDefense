using System.Collections.Generic;
using UnityEngine;

/// <summary>Paleta compartida por torres, íconos y HUD: color por familia (juego de origen) y por tipo de ataque.</summary>
public static class DefensePalette
{
    public static Color Family(DefenseSource s)
    {
        switch (s)
        {
            case DefenseSource.CoC_Home: return new Color(0.96f, 0.76f, 0.22f);
            case DefenseSource.CoC_Builder: return new Color(0.88f, 0.48f, 0.20f);
            case DefenseSource.CoC_Capital: return new Color(0.62f, 0.38f, 0.90f);
            case DefenseSource.CR: return new Color(0.30f, 0.58f, 0.98f);
            case DefenseSource.BoomBeach: return new Color(0.25f, 0.78f, 0.48f);
            default: return new Color(0.25f, 0.72f, 0.72f);
        }
    }

    public static Color Attack(AttackType a)
    {
        switch (a)
        {
            case AttackType.Splash: return new Color(1.0f, 0.55f, 0.15f);
            case AttackType.MultiTarget: return new Color(0.35f, 0.95f, 0.95f);
            case AttackType.Burst: return new Color(1.0f, 0.92f, 0.30f);
            case AttackType.Beam: return new Color(1.0f, 0.25f, 0.25f);
            case AttackType.Chain: return new Color(0.45f, 0.65f, 1.0f);
            case AttackType.Push: return new Color(0.70f, 0.90f, 1.0f);
            case AttackType.SingleTarget: return new Color(0.95f, 0.95f, 0.95f);
            default: return new Color(0.6f, 0.6f, 0.6f);
        }
    }

    public static string FamilyName(DefenseSource s)
    {
        switch (s)
        {
            case DefenseSource.CoC_Home: return "Clash of Clans · Aldea";
            case DefenseSource.CoC_Builder: return "Clash of Clans · Taller";
            case DefenseSource.CoC_Capital: return "Clash of Clans · Capital";
            case DefenseSource.CR: return "Clash Royale";
            case DefenseSource.BoomBeach: return "Boom Beach";
            default: return "Original";
        }
    }

    public static string AttackName(AttackType a)
    {
        switch (a)
        {
            case AttackType.SingleTarget: return "Un objetivo";
            case AttackType.Splash: return "Área";
            case AttackType.MultiTarget: return "Multiobjetivo";
            case AttackType.Burst: return "Ráfaga";
            case AttackType.Beam: return "Rayo (rampa)";
            case AttackType.Chain: return "Cadena";
            case AttackType.Push: return "Empuje";
            case AttackType.Pull: return "Atracción";
            case AttackType.Spawner: return "Genera tropas";
            case AttackType.Support: return "Soporte";
            case AttackType.Trap: return "Trampa";
        }
        return a.ToString();
    }

    public static string TargetsName(TargetLayer t)
    {
        if (t == TargetLayer.Both) return "Tierra y aire";
        if (t == TargetLayer.Air) return "Sólo aire";
        if (t == TargetLayer.Ground) return "Sólo tierra";
        return "—";
    }
}

/// <summary>
/// Apariencia de una torre construida sobre el prefab Tower_Box (cajas grises): escala por costo y
/// emblema con el color del tipo de ataque. El cuerpo queda gris a propósito (pedido del owner);
/// la familia se lee en el ícono del HUD y en el tooltip.
/// </summary>
public class TowerVisual : MonoBehaviour
{
    public Color familyColor;
    public Color attackColor;

    [Header("Piezas (cableadas en el prefab; si faltan se buscan por nombre)")]
    public Renderer emblemRenderer;
    public Renderer bandRenderer;
    [Tooltip("Escala mínima y máxima según el costo (40..600 oro).")]
    public float minScale = 0.9f;
    public float maxScale = 1.25f;

    Material emblemMat;
    Material bandMat;

    public static void Apply(Tower tower, TowerData data)
    {
        if (tower == null || data == null) return;
        var v = tower.GetComponent<TowerVisual>();
        if (v == null) v = tower.gameObject.AddComponent<TowerVisual>();
        v.Configure(tower, data);
    }

    void Configure(Tower tower, TowerData data)
    {
        familyColor = DefensePalette.Family(data.source);
        attackColor = DefensePalette.Attack(data.attackType);

        // 1) escala por tier de costo
        float tier = Mathf.InverseLerp(40f, 600f, data.cost);
        transform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, tier);

        // 2) emblema (cubo chico arriba) con el color del tipo de ataque
        if (emblemRenderer == null) emblemRenderer = FindChildRenderer("Emblem");
        if (emblemRenderer != null)
        {
            if (emblemMat == null) emblemMat = emblemRenderer.material;   // instancia por torre
            emblemMat.color = attackColor;
            emblemMat.EnableKeyword("_EMISSION");
            if (emblemMat.HasProperty("_EmissionColor")) emblemMat.SetColor("_EmissionColor", attackColor * 0.8f);
        }

        // 3) banda fina con el color de la familia (opcional: si el prefab la trae)
        if (bandRenderer == null) bandRenderer = FindChildRenderer("Band");
        if (bandRenderer != null)
        {
            if (bandMat == null) bandMat = bandRenderer.material;
            bandMat.color = familyColor;
        }
    }

    Renderer FindChildRenderer(string childName)
    {
        var t = transform.Find(childName);
        return t != null ? t.GetComponent<Renderer>() : null;
    }

    void OnDestroy()
    {
        if (emblemMat != null) Destroy(emblemMat);
        if (bandMat != null) Destroy(bandMat);
    }
}
