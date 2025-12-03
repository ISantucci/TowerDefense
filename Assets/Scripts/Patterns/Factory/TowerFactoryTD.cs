using System;
using System.Collections.Generic;
using UnityEngine;

public enum TowerId
{
    Basic
}

[Serializable]
public class TowerPrefabEntry
{
    public TowerId id;
    public Tower prefab;   // 👈 tipo Tower, NO GameObject
}

public class TowerFactoryTD : MonoBehaviour
{
    [Header("Prefabs de Torres")]
    public List<TowerPrefabEntry> towerPrefabs = new();

    public Tower Create(TowerId id, Vector3 position, Quaternion rotation)
    {
        // Buscar el prefab correspondiente
        var entry = towerPrefabs.Find(e => e.id == id);
        if (entry == null || entry.prefab == null)
        {
            Debug.LogError($"[TowerFactoryTD] No hay prefab asignado para TowerId={id}");
            return null;
        }

        // VALIDACIÓN fuerte: el prefab debe tener Tower
        if (entry.prefab.GetComponent<Tower>() == null)
        {
            Debug.LogError($"[TowerFactoryTD] El prefab asignado a {id} NO tiene componente Tower. Revisá el prefab.");
            return null;
        }

        // Instanciar
        var instance = Instantiate(entry.prefab, position, rotation);

        // Setear tipo
        instance.towerType = id;

        Debug.Log($"[TowerFactoryTD] Torre {id} creada en {position}");
        return instance;
    }
}
