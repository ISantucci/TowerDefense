using UnityEngine;

public enum EnemyId { Goblin }

public class EnemyFactoryTD : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject goblinPrefab;

    public GameObject Create(EnemyId id, Vector3 pos, Quaternion rot, WaypointsPath path)
    {
        GameObject prefab = id switch
        {
            EnemyId.Goblin => goblinPrefab,
            _ => null
        };
        if (prefab == null) { Debug.LogError($"EnemyId sin prefab: {id}"); return null; }

        var go = Instantiate(prefab, pos, rot);
        var mover = go.GetComponent<EnemyMovement>();
        if (mover != null) mover.path = path;

        
        GameEvents.RaiseEnemySpawned();
        return go;
    }
}
