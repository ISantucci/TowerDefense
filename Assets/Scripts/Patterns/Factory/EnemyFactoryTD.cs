using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyEntry
{
    public EnemyData data;
}

public class EnemyFactoryTD : MonoBehaviour
{
    [Header("Catálogo de Enemigos (Flyweights)")]
    public List<EnemyEntry> enemies = new();

    [Header("Ruta por defecto")]
    [SerializeField] EnemyGraphPath defaultPath;  // 👈 arrastrás tu EnemyPath de la escena

    EnemyData GetData(EnemyId id)
    {
        foreach (var e in enemies)
        {
            if (e != null && e.data != null && e.data.id == id)
                return e.data;
        }

        return null;
    }

    public EnemyTD Spawn(EnemyId id, Vector3 position, Quaternion rotation)
    {
        var data = GetData(id);
        if (data == null)
        {
            return null;
        }

        if (data.prefab == null)
        {
            return null;
        }

        var enemy = Instantiate(data.prefab, position, rotation);
        enemy.data = data;

        // 🔹 Sincronizar velocidad con el Flyweight (opcional pero prolijo)
        var movement = enemy.GetComponent<EnemyMovement>();
        if (movement != null)
        {
            movement.moveSpeed = data.moveSpeed;

            // 🔹 Asignar ruta usando EnemyGraphPath
            if (defaultPath != null)
            {
                var route = defaultPath.ComputeAndGetPath();
                movement.SetRoute(route);
            }
            else
            {
            }
        }
        else
        {
        }

        return enemy;
    }
}
