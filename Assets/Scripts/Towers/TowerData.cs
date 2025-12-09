using UnityEngine;

[CreateAssetMenu(menuName = "TD/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Identidad")]
    public TowerId id;              // Basic, FastTower, etc.

    [Header("Prefab")]
    public Tower prefab;            // Prefab base de la torre

    [Header("Stats")]
    public float range = 6f;
    public float fireRate = 0.6f;
    public ProjectileId projectileType = ProjectileId.Basic;

    [Header("Economía")]
    public int cost = 50;
}
