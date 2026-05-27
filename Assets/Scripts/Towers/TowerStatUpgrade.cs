using UnityEngine;
using UnityEngine.Serialization;

public enum UpgradeStat { Damage, AttackSpeed, Range }

[System.Serializable]
public class TowerStatUpgrade
{
    public int maxLevel = 3;
    public int[] costsPerLevel = { 50, 100, 150 };

    [FormerlySerializedAs("valuesPerLevel")]
    [FormerlySerializedAs("percentPerLevel")]
    [FormerlySerializedAs("multiplierPerLevel")]
    public float[] growthPerLevel = { 0.1f, 0.2f, 0.3f };

    [HideInInspector] public int currentLevel;

    public bool IsMaxed => maxLevel <= 0 || currentLevel >= maxLevel;

    bool HasConfigForNextLevel =>
        costsPerLevel  != null && currentLevel < costsPerLevel.Length &&
        growthPerLevel != null && currentLevel < growthPerLevel.Length;

    public bool CanUpgrade => !IsMaxed && HasConfigForNextLevel;

    public int NextCost
    {
        get
        {
            if (IsMaxed || costsPerLevel == null || currentLevel >= costsPerLevel.Length) return 0;
            return costsPerLevel[currentLevel];
        }
    }
}
