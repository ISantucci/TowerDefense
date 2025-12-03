using UnityEngine;

[CreateAssetMenu(menuName = "TD/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identidad")]
    public EnemyId id;

    [Header("Stats base (intrínsecos)")]
    public int maxHealth = 10;      // 👈 ojo: int, no float, para matchear con EnemyTD
    public float moveSpeed = 3f;
    public int bounty = 5;          // dinero que da al morir
    public int scoreReward = 1;     // puntos que da al morir
    public int damageToBase = 1;    // cuánto le resta a las vidas si llega al final

    [Header("Prefab")]
    public EnemyTD prefab;
}
