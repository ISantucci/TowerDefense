using UnityEngine;

[CreateAssetMenu(menuName = "TD/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Identidad")]
    public TowerId id;

    [Header("Prefab")]
    public Tower prefab;

    [Header("Stats")]
    public float range = 6f;
    public float fireRate = 0.6f;

    [Header("Projectile")]
    public ProjectileId projectileId;   

    [Header("Economía")]
    public int cost = 50;
}
