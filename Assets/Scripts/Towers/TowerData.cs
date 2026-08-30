using UnityEngine;

/// <summary>Override opcional de stats por nivel (puede quedar vacío).</summary>
[System.Serializable]
public class TowerLevelStats
{
    public int   damage;
    public float fireRate;
    public float range;
    public int   upgradeCost;
}

[CreateAssetMenu(menuName = "TD/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Identidad")]
    public TowerId id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public DefenseSource source;
    public DefenseKind kind = DefenseKind.Defense;

    [Header("Prefab")]
    public Tower prefab;

    [Header("Stats")]
    public int   damage   = 10;
    public float range    = 6f;
    // fireRate = SEGUNDOS ENTRE DISPAROS (convención del proyecto)
    public float fireRate = 0.6f;

    [Header("Targeting")]
    public TargetLayer targets   = TargetLayer.Ground;
    public AttackType attackType = AttackType.SingleTarget;
    public float minRange = 0f;

    [Header("Parámetros por tipo de ataque")]
    public float splashRadius     = 0f;    // Splash
    public int   multiTargetCount = 1;     // MultiTarget / Beam multi
    public int   burstCount       = 1;     // Burst
    public float burstInterval    = 0.1f;  // Burst: segundos entre disparos de la ráfaga
    public float beamRampSeconds  = 5f;    // Beam: tiempo hasta el multiplicador máximo
    public float beamMaxMultiplier = 3f;   // Beam: multiplicador de daño al final de la rampa
    public float pushDistance     = 0f;    // Push
    public float chainFalloff     = 0.5f;  // Chain: multiplicador acumulativo por salto
    public int   chainMaxJumps    = 3;     // Chain: saltos máximos después del primario

    [Header("Projectile")]
    public ProjectileId projectileId;

    [Header("Economía")]
    public int cost = 50;

    [Header("Referencia del catálogo (informativo)")]
    public int    hitpoints          = 500;
    public int    maxLevelReference  = 1;
    public float  dpsLevel1Reference;
    public float  dpsMaxReference;
    public int    unlockLevel        = 1;
    public int    referenceBuildCost;
    public string referenceCurrency;
    public string special;
    public bool   statsVerified;

    [Header("Overrides por nivel (opcional)")]
    public TowerLevelStats[] levels;

    // === Helpers ===
    public float Dps => fireRate > 0f ? damage / fireRate : 0f;
    public bool CanTargetAir    => (targets & TargetLayer.Air) != 0;
    public bool CanTargetGround => (targets & TargetLayer.Ground) != 0;
    public string DisplayName   => string.IsNullOrEmpty(displayName) ? id.ToString() : displayName;

#if UNITY_EDITOR
    void OnValidate()
    {
        damage           = Mathf.Max(0, damage);
        range            = Mathf.Max(0f, range);
        fireRate         = Mathf.Max(0f, fireRate);
        minRange         = Mathf.Clamp(minRange, 0f, range);
        splashRadius     = Mathf.Max(0f, splashRadius);
        multiTargetCount = Mathf.Max(1, multiTargetCount);
        burstCount       = Mathf.Max(1, burstCount);
        burstInterval    = Mathf.Max(0f, burstInterval);
        beamRampSeconds  = Mathf.Max(0.01f, beamRampSeconds);
        beamMaxMultiplier = Mathf.Max(1f, beamMaxMultiplier);
        pushDistance     = Mathf.Max(0f, pushDistance);
        chainFalloff     = Mathf.Clamp01(chainFalloff);
        chainMaxJumps    = Mathf.Max(0, chainMaxJumps);
        cost             = Mathf.Max(0, cost);
        hitpoints        = Mathf.Max(0, hitpoints);
        maxLevelReference = Mathf.Max(1, maxLevelReference);
        dpsLevel1Reference = Mathf.Max(0f, dpsLevel1Reference);
        dpsMaxReference  = Mathf.Max(0f, dpsMaxReference);
        unlockLevel      = Mathf.Max(1, unlockLevel);
        referenceBuildCost = Mathf.Max(0, referenceBuildCost);

        if (levels != null)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                var l = levels[i];
                if (l == null) continue;
                l.damage      = Mathf.Max(0, l.damage);
                l.fireRate    = Mathf.Max(0f, l.fireRate);
                l.range       = Mathf.Max(0f, l.range);
                l.upgradeCost = Mathf.Max(0, l.upgradeCost);
            }
        }
    }
#endif
}
