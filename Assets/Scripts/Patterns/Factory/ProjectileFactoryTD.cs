using UnityEngine;

public class ProjectileFactoryTD : MonoBehaviour
{
    [Header("Projectile Data")]
    public ProjectileData[] projectileDatas;   

    public ProjectileData GetData(ProjectileId id)
    {
        if (projectileDatas == null) return null;

        foreach (var d in projectileDatas)
        {
            if (d != null && d.id == id)
                return d;
        }

        return null;
    }

    public Projectile GetPrefab(ProjectileId id)
    {
        var data = GetData(id);
        return data != null ? data.prefab : null;
    }
}
