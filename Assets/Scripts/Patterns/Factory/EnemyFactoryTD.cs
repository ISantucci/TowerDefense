using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyEntry
{
    public EnemyData data;
}

/// <summary>
/// Fábrica de enemigos (Type Object / Flyweight). El catálogo de la escena se fusiona con Resources/Enemies.
/// La ruta la puede dar el nivel (route provider) o el EnemyGraphPath legacy de la escena.
/// </summary>
public class EnemyFactoryTD : MonoBehaviour
{
    const string ResourcesFolder = "Enemies";

    [Header("Catálogo de Enemigos (Flyweights)")]
    public List<EnemyEntry> enemies = new List<EnemyEntry>();

    [Header("Ruta por defecto (legacy)")]
    [SerializeField] EnemyGraphPath defaultPath;

    Func<int, IReadOnlyList<Transform>> routeProvider;

    void Awake()
    {
        MergeResources();
    }

    void MergeResources()
    {
        if (enemies == null) enemies = new List<EnemyEntry>();
        var loaded = Resources.LoadAll<EnemyData>(ResourcesFolder);
        if (loaded == null) return;
        foreach (var d in loaded)
        {
            if (d == null || GetData(d.id) != null) continue;
            var e = new EnemyEntry();
            e.data = d;
            enemies.Add(e);
        }
    }

    /// <summary>El nivel registra de dónde salen las rutas (por índice de camino).</summary>
    public void SetRouteProvider(Func<int, IReadOnlyList<Transform>> provider)
    {
        routeProvider = provider;
    }

    public EnemyData GetData(EnemyId id)
    {
        if (enemies == null) return null;
        foreach (var e in enemies)
        {
            if (e != null && e.data != null && e.data.id == id)
                return e.data;
        }
        return null;
    }

    public IReadOnlyList<EnemyEntry> Catalog => enemies;

    public EnemyTD Spawn(EnemyId id, Vector3 position, Quaternion rotation)
    {
        IReadOnlyList<Transform> route = null;
        if (routeProvider != null) route = routeProvider(0);
        return Spawn(id, position, rotation, route);
    }

    public EnemyTD Spawn(EnemyId id, Vector3 position, Quaternion rotation, IReadOnlyList<Transform> route)
    {
        var data = GetData(id);
        if (data == null)
        {
            Debug.LogWarning("[EnemyFactory] No hay EnemyData para " + id);
            return null;
        }
        if (data.prefab == null)
        {
            Debug.LogWarning("[EnemyFactory] EnemyData " + data.name + " sin prefab.");
            return null;
        }

        var enemy = Instantiate(data.prefab, position, rotation);
        enemy.data = data;
        enemy.name = "Enemy_" + data.DisplayName;
        enemy.InitHealth();

        var movement = enemy.GetComponent<EnemyMovement>();
        if (movement != null)
        {
            movement.moveSpeed = data.moveSpeed;
            movement.heightOffset = data.heightOffset > 0f ? data.heightOffset : 1f;

            if (route == null && defaultPath != null)
                route = defaultPath.ComputeAndGetPath();

            if (route != null)
                movement.SetRoute(route);
        }

        EnemyVisual.Apply(enemy, data);
        return enemy;
    }
}
