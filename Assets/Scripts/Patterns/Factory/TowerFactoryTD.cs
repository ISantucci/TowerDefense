using System.Diagnostics;
using UnityEngine;

public enum TowerId { Basic }

public class TowerFactoryTD : MonoBehaviour
{
    [Header("Prefabs")]
    public Tower basicTowerPrefab;

    public Tower GetPrefab(TowerId id)
    {
        switch (id)
        {
            case TowerId.Basic: return basicTowerPrefab;
            default:
                UnityEngine.Debug.LogError($"TowerId sin prefab: {id}");
                return null;
        }
    }

    public Tower Create(TowerId id, Vector3 pos, Quaternion rot)
    {
        var prefab = GetPrefab(id);
        if (prefab == null) return null;
        return Instantiate(prefab, pos, rot);
    }
}
