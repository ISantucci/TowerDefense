using UnityEngine;

public enum ProjectileId
{
    Arrow,
    Bomb
}

[CreateAssetMenu(menuName = "TD/ProjectileData")]
public class ProjectileData : ScriptableObject
{
    [Header("Identidad")]
    public ProjectileId id;
    public Projectile prefab;

    [Header("Stats")]
    public float speed = 10f;
    public int damage = 1;

    [Header("AOE (solo si > 0)")]
    public float splashRadius = 0f;          // 0 = single target, >0 = area
    public LayerMask enemyMask;             // qué layers cuentan como enemigos
}
