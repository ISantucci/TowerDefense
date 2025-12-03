using UnityEngine;

[CreateAssetMenu(menuName = "TD/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identidad")]
    public EnemyId id;

    [Header("Stats base (intrínsecos)")]
    public int maxHealth = 10;      
    public float moveSpeed = 3f;
    public int bounty = 5;          
    public int scoreReward = 1;     
    public int damageToBase = 1;    

    [Header("Prefab")]
    public EnemyTD prefab;
}
