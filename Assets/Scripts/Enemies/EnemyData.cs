using UnityEngine;

[CreateAssetMenu(menuName = "TD/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identidad")]
    public EnemyId id;
    public string displayName;
    [TextArea(1, 3)] public string description;

    [Header("Stats base (intrínsecos)")]
    public int maxHealth = 10;
    public float moveSpeed = 3f;
    public int bounty = 5;
    public int scoreReward = 1;
    public int damageToBase = 1;

    [Header("Targeting / defensa")]
    [Tooltip("Si es aéreo, sólo lo atacan torres con TargetLayer.Air")]
    public bool isFlying = false;
    [Tooltip("Fracción de reducción de daño (0 = nada, 0.9 = -90%)")]
    [Range(0f, 0.9f)] public float armor = 0f;

    [Header("Apariencia (se aplica al prefab en runtime)")]
    public Color tint = Color.white;
    [Tooltip("Escala relativa del prefab (1 = igual).")]
    public float scale = 1f;
    [Tooltip("Altura del centro sobre el suelo. Tierra ≈ 1, aire ≈ 2.4")]
    public float heightOffset = 1f;
    [Tooltip("Aéreos: amplitud del vaivén vertical.")]
    public float bobAmplitude = 0f;

    [Header("Prefab")]
    public EnemyTD prefab;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? EnemyNames.Of(id) : displayName;
}
