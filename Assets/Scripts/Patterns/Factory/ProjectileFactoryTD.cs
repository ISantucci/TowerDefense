using UnityEngine;

public enum ProjectileId { Basic  }

public class ProjectileFactoryTD : MonoBehaviour
{
    [Header("Prefabs")]
    public Projectile basicPrefab;

    public Projectile GetPrefab(ProjectileId id)
    {
        switch (id)
        {
            case ProjectileId.Basic: return basicPrefab;
            default:
                Debug.LogError($"ProjectileId sin prefab: {id}");
                return null;
        }
    }
}
