using UnityEngine;

[RequireComponent(typeof(Tower))]
public class TowerUpgradeComponent : MonoBehaviour
{
    [Header("Damage Upgrade  (bonus decimal por nivel: 0.1 = +10%)")]
    public TowerStatUpgrade damageUpgrade = new TowerStatUpgrade
    {
        maxLevel = 3,
        costsPerLevel = new int[] { 50, 100, 150 },
        growthPerLevel = new float[] { 0.1f, 0.2f, 0.3f }
    };

    [Header("Attack Speed Upgrade  (bonus decimal por nivel: 0.25 = +25% DPS)")]
    public TowerStatUpgrade speedUpgrade = new TowerStatUpgrade
    {
        maxLevel = 3,
        costsPerLevel = new int[] { 75, 150, 225 },
        growthPerLevel = new float[] { 0.25f, 0.5f, 1f }
    };

    [Header("Range Upgrade  (bonus decimal por nivel: 0.1 = +10%)")]
    public TowerStatUpgrade rangeUpgrade = new TowerStatUpgrade
    {
        maxLevel = 3,
        costsPerLevel = new int[] { 60, 120, 180 },
        growthPerLevel = new float[] { 0.1f, 0.2f, 0.3f }
    };

    public static event System.Action<TowerUpgradeComponent> Destroyed;
    public static event System.Action<TowerUpgradeComponent> LevelChanged;

    Tower tower;
    float baseFireRate;
    float baseRange;
    int   baseDamage;

    void Awake()
    {
        tower = GetComponent<Tower>();
    }

    void OnDestroy()
    {
        Destroyed?.Invoke(this);
    }

    void Start()
    {
        baseFireRate = tower.data != null ? tower.data.fireRate : tower.fireRate;
        baseRange    = tower.data != null ? tower.data.range    : tower.range;
        baseDamage   = tower.data != null ? tower.data.damage   : 0;
    }

    public bool CanUpgrade(UpgradeStat stat) => GetUpgrade(stat).CanUpgrade;
    public int GetCurrentLevel(UpgradeStat stat) => GetUpgrade(stat).currentLevel;
    public int GetMaxLevel(UpgradeStat stat) => GetUpgrade(stat).maxLevel;
    public int GetNextCost(UpgradeStat stat) => GetUpgrade(stat).NextCost;

    public bool ApplyNextLevel(UpgradeStat stat)
    {
        var upg = GetUpgrade(stat);
        if (!upg.CanUpgrade) return false;
        upg.currentLevel++;
        ApplyUpgrade(stat, upg);
        LevelChanged?.Invoke(this);
        CombatEvents.RaiseTowerUpgraded(tower, stat);
        return true;
    }

    public bool RevertLastLevel(UpgradeStat stat)
    {
        var upg = GetUpgrade(stat);
        if (upg.currentLevel <= 0) return false;
        upg.currentLevel--;
        ApplyUpgrade(stat, upg);
        LevelChanged?.Invoke(this);
        return true;
    }

    void ApplyUpgrade(UpgradeStat stat, TowerStatUpgrade upgrade)
    {
        float m = GetAccumulatedMultiplier(upgrade);
        switch (stat)
        {
            case UpgradeStat.Damage:
                tower.currentDamage = Mathf.RoundToInt(baseDamage * m);
                break;
            case UpgradeStat.AttackSpeed:
                tower.fireRate = baseFireRate / m;
                break;
            case UpgradeStat.Range:
                tower.range = baseRange * m;
                break;
        }
    }

    float GetAccumulatedMultiplier(TowerStatUpgrade upg)
    {
        if (upg.currentLevel == 0 || upg.growthPerLevel == null) return 1f;
        float result = 1f;
        for (int i = 0; i < upg.currentLevel && i < upg.growthPerLevel.Length; i++)
            result *= (1f + upg.growthPerLevel[i]);
        return result;
    }

    public int GetTotalSpentOnUpgrades()
    {
        return SumSpent(damageUpgrade) + SumSpent(speedUpgrade) + SumSpent(rangeUpgrade);
    }

    int SumSpent(TowerStatUpgrade upg)
    {
        int total = 0;
        for (int i = 0; i < upg.currentLevel && i < upg.costsPerLevel.Length; i++)
            total += upg.costsPerLevel[i];
        return total;
    }

    public void RestoreState(int damageLevel, int speedLevel, int rangeLevel)
    {
        damageUpgrade.currentLevel = Mathf.Clamp(damageLevel, 0, damageUpgrade.maxLevel);
        speedUpgrade.currentLevel  = Mathf.Clamp(speedLevel,  0, speedUpgrade.maxLevel);
        rangeUpgrade.currentLevel  = Mathf.Clamp(rangeLevel,  0, rangeUpgrade.maxLevel);

        // Si Start() no corrió todavía (llamado desde SellTowerCommand.Undo en el mismo frame
        // que factory.Create), inicializar desde tower.data que ya fue asignado por ApplyData.
        if (baseFireRate <= 0f) baseFireRate = tower.data != null ? tower.data.fireRate : tower.fireRate;
        if (baseRange   <= 0f) baseRange    = tower.data != null ? tower.data.range    : tower.range;
        if (baseDamage  <= 0)  baseDamage   = tower.data != null ? tower.data.damage   : 0;

        ApplyUpgrade(UpgradeStat.Damage,      damageUpgrade);
        ApplyUpgrade(UpgradeStat.AttackSpeed, speedUpgrade);
        ApplyUpgrade(UpgradeStat.Range,       rangeUpgrade);
    }

    TowerStatUpgrade GetUpgrade(UpgradeStat stat)
    {
        switch (stat)
        {
            case UpgradeStat.AttackSpeed: return speedUpgrade;
            case UpgradeStat.Range:       return rangeUpgrade;
            default:                      return damageUpgrade;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ValidateUpgrade(damageUpgrade, "Damage");
        ValidateUpgrade(speedUpgrade,  "AttackSpeed");
        ValidateUpgrade(rangeUpgrade,  "Range");
    }

    void ValidateUpgrade(TowerStatUpgrade upg, string statName)
    {
        if (upg == null) return;
        if (upg.costsPerLevel == null || upg.costsPerLevel.Length < upg.maxLevel)
            Debug.LogWarning($"[TowerUpgrade] {name} — {statName}: costsPerLevel tiene {(upg.costsPerLevel?.Length ?? 0)} entradas pero maxLevel es {upg.maxLevel}. El upgrade de nivel {(upg.costsPerLevel?.Length ?? 0) + 1} en adelante quedará bloqueado.", this);
        if (upg.growthPerLevel == null || upg.growthPerLevel.Length < upg.maxLevel)
            Debug.LogWarning($"[TowerUpgrade] {name} — {statName}: growthPerLevel tiene {(upg.growthPerLevel?.Length ?? 0)} entradas pero maxLevel es {upg.maxLevel}. El stat no cambiará desde el nivel {(upg.growthPerLevel?.Length ?? 0) + 1} en adelante.", this);
        if (upg.growthPerLevel != null)
            for (int i = 0; i < upg.growthPerLevel.Length; i++)
                if (upg.growthPerLevel[i] <= -1f)
                    Debug.LogWarning($"[TowerUpgrade] {name} — {statName}: growthPerLevel[{i}] = {upg.growthPerLevel[i]} es inválido (factor = 1 + valor ≤ 0). El stat quedaría en 0 o negativo.", this);
    }
#endif
}
