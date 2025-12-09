using System;
using System.Collections.Generic;
using UnityEngine;

public enum TowerId
{
    Archer,
    Bomber,
}

public class TowerFactoryTD : MonoBehaviour
{
    [Header("Catálogo de Torres (Type Object)")]
    public List<TowerData> towerCatalog = new();

    TowerData GetData(TowerId id)
    {
        foreach (var d in towerCatalog)
        {
            if (d != null && d.id == id)
                return d;
        }

        Debug.LogError($"[TowerFactoryTD] No se encontró TowerData para id={id}");
        return null;
    }

    public int GetCost(TowerId id)
    {
        var d = GetData(id);
        return d != null ? d.cost : 0;
    }

    public Tower Create(TowerId id, Vector3 position, Quaternion rotation)
    {
        var data = GetData(id);
        if (data == null)
        {
            Debug.LogError($"[TowerFactoryTD] TowerData nulo para id={id}");
            return null;
        }

        if (data.prefab == null)
        {
            Debug.LogError($"[TowerFactoryTD] TowerData {data.name} no tiene prefab asignado.");
            return null;
        }

        var tower = Instantiate(data.prefab, position, rotation);
        if (tower == null)
        {
            Debug.LogError($"[TowerFactoryTD] Falló Instantiate para {id}");
            return null;
        }

        // Aplico datos Type Object
        tower.ApplyData(data);

        // Encolar evento (ya lo hacías antes)
        EventQueueManager.Enqueue(
            new GameplayEvent(
                GameplayEventType.TowerBuilt,
                (int)id,
                data.cost
            )
        );

        Debug.Log($"[TowerFactoryTD] Torre {id} creada en {position}");
        return tower;
    }
}
